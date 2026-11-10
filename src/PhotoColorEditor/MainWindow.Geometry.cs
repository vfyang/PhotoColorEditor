using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PhotoColorEditor;

public partial class MainWindow
{
    [Flags]
    private enum CropPart { New = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, Move = 16 }

    private const double CropHitRadius = 18;
    private const double MinCropFraction = .005;
    private bool cropping;
    private Rect cropSelection;
    private (Point Start, Rect Selection, CropPart Part)? cropDrag;
    private bool fineRotating;
    private PhotoGeometry DisplayedGeometry => presentedEdit.Geometry;

    private void FineRotateClicked(object sender, RoutedEventArgs e)
    {
        if (source is null || busy) return;
        StopPicking();
        StopCrop();
        EndPan();
        AngleInput.Text = FormatAngle(history.Current.Geometry.Angle);
        fineRotating = true;
        RefreshUi();
        RenderFineRotation();
        AngleInput.Focus();
        AngleInput.SelectAll();
    }

    private static string FormatAngle(double angle) => angle.ToString("0.##", CultureInfo.CurrentCulture);

    private bool TryReadAngle(out double angle)
    {
        string text = AngleInput.Text.Replace('−', '-').Replace('，', '.').Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out angle) || !double.IsFinite(angle) || Math.Abs(angle) > PhotoGeometry.AngleLimit) return false;
        angle = Math.Round(angle, 2);
        return true;
    }

    private void AngleChanged(object sender, TextChangedEventArgs e)
    {
        if (fineRotating) RenderFineRotation();
    }

    private void AngleStepClicked(object sender, RoutedEventArgs e)
    {
        if (!TryReadAngle(out double angle)) angle = history.Current.Geometry.Angle;
        double step = ((Button)sender).Tag as string == "left" ? -.5 : .5;
        AngleInput.Text = FormatAngle(Math.Clamp(Math.Round(angle + step, 2), -PhotoGeometry.AngleLimit, PhotoGeometry.AngleLimit));
    }

    private void RenderFineRotation()
    {
        bool valid = TryReadAngle(out double angle);
        ApplyAngleButton.IsEnabled = valid;
        AngleLeftButton.IsEnabled = !valid || angle > -PhotoGeometry.AngleLimit;
        AngleRightButton.IsEnabled = !valid || angle < PhotoGeometry.AngleLimit;
        AngleInput.SetResourceReference(Control.BorderBrushProperty, valid ? "ButtonEdge" : "ErrorInk");
        AngleHint.SetResourceReference(TextBlock.TextProperty, valid ? "AngleHelp" : "AngleInvalid");
        if (!valid) return;
        var edit = history.Current;
        RenderPreview(edit with { Geometry = edit.Geometry.WithAngle(angle) });
    }

    private void ApplyAngleClicked(object sender, RoutedEventArgs e) => ApplyFineRotation();

    private void ApplyFineRotation()
    {
        if (!fineRotating || !TryReadAngle(out double angle)) return;
        fineRotating = false;
        var edit = history.Current;
        Commit(edit with { Geometry = edit.Geometry.WithAngle(angle) });
        FineRotateButton.Focus();
    }

    private void CancelAngleClicked(object sender, RoutedEventArgs e) => CancelFineRotation();

    private void CancelFineRotation()
    {
        if (!fineRotating) return;
        fineRotating = false;
        RefreshUi();
        RenderPreview(history.Current);
        FineRotateButton.Focus();
    }

    private void AngleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && AngleInput.IsKeyboardFocusWithin) ApplyFineRotation();
        else if (e.Key == Key.Escape) CancelFineRotation();
        else return;
        e.Handled = true;
    }

    private void RotateClicked(object sender, RoutedEventArgs e)
    {
        if (source is null || busy) return;
        StopPicking();
        var edit = history.Current;
        Commit(edit with { Geometry = edit.Geometry.Rotate(((Button)sender).Tag as string == "left" ? -1 : 1) });
        ResetViewport();
    }

    private void CropClicked(object sender, RoutedEventArgs e)
    {
        if (source is null || busy) return;
        if (cropping) { ApplyCrop(); return; }
        EndPan();
        StopPicking();
        cropping = true;
        cropSelection = new Rect(.1, .1, .8, .8);
        ResetViewport();
        RefreshUi();
        PhotoSurface.Focus();
        RenderPreview(history.Current);
    }

    private void CancelCropClicked(object sender, RoutedEventArgs e) { StopCrop(); CropButton.Focus(); }

    private void ApplyCrop()
    {
        if (source is not { } image || !cropping) return;
        var edit = history.Current;
        var crop = cropSelection;
        Commit(edit with { Geometry = edit.Geometry.CropTo(crop.Left, crop.Top, crop.Right, crop.Bottom, image.Width, image.Height) });
        ResetViewport();
        CropButton.Focus();
    }

    private void StopCrop()
    {
        if (!cropping) return;
        cropping = false;
        EndCropDrag();
        DrawCrop();
    }

    private Point CropPoint(Point p) => new((p.X - photoBounds.X) / photoBounds.Width,
        (p.Y - photoBounds.Y) / photoBounds.Height);

    private Rect CropFrameBounds() => new(photoBounds.X + cropSelection.X * photoBounds.Width,
        photoBounds.Y + cropSelection.Y * photoBounds.Height, cropSelection.Width * photoBounds.Width,
        cropSelection.Height * photoBounds.Height);

    private CropPart CropPartAt(Point position)
    {
        var frame = CropFrameBounds();
        double xHit = Math.Min(CropHitRadius, frame.Width / 3);
        double yHit = Math.Min(CropHitRadius, frame.Height / 3);
        if (!new Rect(frame.X - xHit, frame.Y - yHit, frame.Width + 2 * xHit, frame.Height + 2 * yHit).Contains(position))
            return CropPart.New;

        var part = CropPart.New;
        if (Math.Abs(position.X - frame.Left) <= xHit) part |= CropPart.Left;
        else if (Math.Abs(position.X - frame.Right) <= xHit) part |= CropPart.Right;
        if (Math.Abs(position.Y - frame.Top) <= yHit) part |= CropPart.Top;
        else if (Math.Abs(position.Y - frame.Bottom) <= yHit) part |= CropPart.Bottom;
        return part == CropPart.New ? CropPart.Move : part;
    }

    private Cursor CropCursor(Point position) => (cropDrag?.Part ?? CropPartAt(position)) switch
    {
        CropPart.Left or CropPart.Right => Cursors.SizeWE,
        CropPart.Top or CropPart.Bottom => Cursors.SizeNS,
        (CropPart.Top | CropPart.Left) or (CropPart.Bottom | CropPart.Right) => Cursors.SizeNWSE,
        (CropPart.Top | CropPart.Right) or (CropPart.Bottom | CropPart.Left) => Cursors.SizeNESW,
        CropPart.Move => Cursors.SizeAll,
        _ => Cursors.Cross
    };

    internal bool BeginCropDrag(Point position)
    {
        if (!cropping || photoBounds.IsEmpty || DisplayedGeometry != history.Current.Geometry) return false;
        var part = CropPartAt(position);
        if (part == CropPart.New && !photoBounds.Contains(position)) return false;
        cropDrag = (CropPoint(position), cropSelection, part);
        return true;
    }

    private void CropMouseDown(MouseButtonEventArgs e)
    {
        if (!BeginCropDrag(e.GetPosition(PhotoSurface))) return;
        if (!PhotoSurface.CaptureMouse()) { cropDrag = null; return; }
        UpdatePhotoCursor();
        e.Handled = true;
    }

    private bool CropMouseMove(MouseEventArgs e)
    {
        if (!cropping || photoBounds.IsEmpty) return false;
        if (cropDrag is not null && e.LeftButton == MouseButtonState.Pressed) UpdateCropDrag(e.GetPosition(PhotoSurface));
        else UpdatePhotoCursor();
        return true;
    }

    internal void UpdateCropDrag(Point position)
    {
        if (cropDrag is not { } drag || photoBounds.IsEmpty) return;
        Point point = CropPoint(position);
        Vector delta = point - drag.Start;
        var frame = drag.Selection;
        if (drag.Part == CropPart.New)
        {
            var selection = new Rect(drag.Start, new Point(Math.Clamp(point.X, 0, 1), Math.Clamp(point.Y, 0, 1)));
            if (selection.Width >= MinCropFraction && selection.Height >= MinCropFraction) cropSelection = selection;
        }
        else if (drag.Part == CropPart.Move)
        {
            frame.X = Math.Clamp(frame.X + delta.X, 0, 1 - frame.Width);
            frame.Y = Math.Clamp(frame.Y + delta.Y, 0, 1 - frame.Height);
            cropSelection = frame;
        }
        else
        {
            double left = drag.Part.HasFlag(CropPart.Left) ? Math.Clamp(frame.Left + delta.X, 0, frame.Right - MinCropFraction) : frame.Left;
            double top = drag.Part.HasFlag(CropPart.Top) ? Math.Clamp(frame.Top + delta.Y, 0, frame.Bottom - MinCropFraction) : frame.Top;
            double right = drag.Part.HasFlag(CropPart.Right) ? Math.Clamp(frame.Right + delta.X, frame.Left + MinCropFraction, 1) : frame.Right;
            double bottom = drag.Part.HasFlag(CropPart.Bottom) ? Math.Clamp(frame.Bottom + delta.Y, frame.Top + MinCropFraction, 1) : frame.Bottom;
            cropSelection = new Rect(new Point(left, top), new Point(right, bottom));
        }
        DrawCrop();
    }

    internal void EndCropDrag()
    {
        cropDrag = null;
        if (PhotoSurface.IsMouseCaptured) PhotoSurface.ReleaseMouseCapture();
        UpdatePhotoCursor();
    }

    private void DrawCrop()
    {
        if (CropFrame is null) return;
        CropButton.Content = T(cropping ? "ApplyCrop" : "Crop");
        CancelCropButton.Visibility = Visible(cropping);
        CropHint.Visibility = Visible(cropping);
        CropFrame.Visibility = CropFrameOutline.Visibility = CropHandles.Visibility = Visible(cropping && !photoBounds.IsEmpty);
        if (cropping && !photoBounds.IsEmpty)
        {
            var frame = CropFrameBounds();
            foreach (var border in new[] { CropFrame, CropFrameOutline })
            {
                Canvas.SetLeft(border, frame.X);
                Canvas.SetTop(border, frame.Y);
                border.Width = frame.Width;
                border.Height = frame.Height;
            }
            double middleX = frame.X + frame.Width / 2, middleY = frame.Y + frame.Height / 2;
            Point[] anchors = [frame.TopLeft, new Point(middleX, frame.Top), frame.TopRight,
                new Point(frame.Left, middleY), new Point(frame.Right, middleY),
                frame.BottomLeft, new Point(middleX, frame.Bottom), frame.BottomRight];
            for (int index = 0; index < anchors.Length; index++)
            {
                var handle = (Border)CropHandles.Children[index];
                Canvas.SetLeft(handle, Math.Clamp(anchors[index].X - handle.Width / 2, 0, Math.Max(0, PhotoSurface.Width - handle.Width)));
                Canvas.SetTop(handle, Math.Clamp(anchors[index].Y - handle.Height / 2, 0, Math.Max(0, PhotoSurface.Height - handle.Height)));
            }
        }
        PhotoSurface.Focusable = cropping || picking;
        UpdatePhotoCursor();
    }

    private Point PickViewPoint(int x, int y)
    {
        var point = DisplayedGeometry.ViewPoint(x, y, source!.Width, source.Height);
        return new Point(point.X, point.Y);
    }
}
