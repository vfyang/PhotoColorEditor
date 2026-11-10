using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PhotoColorEditor;

public partial class MainWindow
{
    private const int MaxZoomStep = 9;
    private int zoomStep;
    private Rect photoBounds = Rect.Empty;
    private Point? pendingAnchor;
    private Point? panStart;
    private Point panOffset;
    private bool CanPan => source is not null && zoomStep > 0 && (PhotoViewport.ScrollableWidth > 0 || PhotoViewport.ScrollableHeight > 0);

    private void ApplyPalette()
    {
        bool contrast = SystemParameters.HighContrast;
        Resources["Surface"] = PaletteBrush(0xEDF2F0, 0x172322, SystemColors.ControlBrush);
        Resources["Panel"] = PaletteBrush(0xFFFFFF, 0x223130, SystemColors.WindowBrush);
        Resources["Ink"] = PaletteBrush(0x1D2E2D, 0xF4F8F7, SystemColors.WindowTextBrush);
        Resources["MutedInk"] = PaletteBrush(0x4C625E, 0xBED0CC, SystemColors.WindowTextBrush);
        Resources["Edge"] = PaletteBrush(0xB8C8C3, 0x4D6460, SystemColors.WindowTextBrush);
        Resources["ButtonEdge"] = PaletteBrush(0x6E827B, 0x8CA8A0, SystemColors.WindowTextBrush);
        Resources["Accent"] = PaletteBrush(0x205D51, 0xA4E1D0, SystemColors.HighlightBrush);
        Resources["AccentInk"] = PaletteBrush(0xFFFFFF, 0x133A32, SystemColors.HighlightTextBrush);
        Resources["Stage"] = PaletteBrush(0x313636, 0x111717, SystemColors.WindowBrush);
        Resources["ErrorInk"] = PaletteBrush(0xA12D2D, 0xFFB2AC, SystemColors.WindowTextBrush);
        DrawLightHistogram();
        DrawHistogram();
        DrawCurve();

        Brush PaletteBrush(uint light, uint dark, Brush highContrast)
        {
            if (contrast) return highContrast;
            uint color = preferences.DarkTheme ? dark : light;
            return new SolidColorBrush(Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color));
        }
    }

    private void SystemPaletteChanged(object? sender, PropertyChangedEventArgs e) => ApplyPalette();

    private Rect ImageRect() => photoBounds;

    private void ResetViewport() => SetZoom(0, new Point(.5, .5));

    private void ZoomClicked(object sender, RoutedEventArgs e) =>
        SetZoom(zoomStep + (sender == ZoomIn ? 1 : -1), ViewportAnchor());

    private void FitClicked(object sender, RoutedEventArgs e) => ResetViewport();

    private void CenterClicked(object sender, RoutedEventArgs e) => ScrollToAnchor(new Point(.5, .5));

    private Point ViewportAnchor() => photoBounds.IsEmpty ? new Point(.5, .5) : new Point(
        Math.Clamp((PhotoViewport.HorizontalOffset + PhotoViewport.ViewportWidth / 2 - photoBounds.X) / photoBounds.Width, 0, 1),
        Math.Clamp((PhotoViewport.VerticalOffset + PhotoViewport.ViewportHeight / 2 - photoBounds.Y) / photoBounds.Height, 0, 1));

    private void SetZoom(int step, Point anchor)
    {
        EndPan();
        zoomStep = Math.Clamp(step, 0, MaxZoomStep);
        PhotoViewport.HorizontalScrollBarVisibility = PhotoViewport.VerticalScrollBarVisibility =
            zoomStep == 0 ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Visible;
        RefreshZoomButtons();
        LayoutPhoto();
        if (pendingAnchor is null)
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (pendingAnchor is not { } target) return;
                pendingAnchor = null;
                ScrollToAnchor(target);
            }, DispatcherPriority.Loaded);
        pendingAnchor = anchor;
    }

    private void ScrollToAnchor(Point anchor)
    {
        if (photoBounds.IsEmpty) return;
        PhotoViewport.UpdateLayout();
        PhotoViewport.ScrollToHorizontalOffset(photoBounds.X + photoBounds.Width * anchor.X - PhotoViewport.ViewportWidth / 2);
        PhotoViewport.ScrollToVerticalOffset(photoBounds.Y + photoBounds.Height * anchor.Y - PhotoViewport.ViewportHeight / 2);
    }

    private void RefreshZoomButtons()
    {
        ZoomOut.IsEnabled = zoomStep > 0;
        ZoomIn.IsEnabled = zoomStep < MaxZoomStep;
        FitPhoto.IsEnabled = CenterPhoto.IsEnabled = zoomStep > 0;
        PanHint.Visibility = Visible(zoomStep > 0 && !cropping);
        ZoomReadout.Text = $"{Math.Round(Math.Pow(1.25, zoomStep) * 100):0}%";
    }

    private void ViewportResized(object sender, SizeChangedEventArgs e) => LayoutPhoto();

    private void ViewportScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) LayoutPhoto();
        UpdatePhotoCursor();
    }

    private void UpdatePhotoCursor()
    {
        if (PhotoSurface is null) return;
        PhotoSurface.Cursor = cropping && !photoBounds.IsEmpty ? CropCursor(Mouse.GetPosition(PhotoSurface)) :
            picking ? Cursors.Cross : panStart.HasValue ? Cursors.SizeAll : CanPan ? Cursors.Hand : Cursors.Arrow;
    }

    private void BeginPan(MouseButtonEventArgs e)
    {
        if (busy || !CanPan || !photoBounds.Contains(e.GetPosition(PhotoSurface)) || !PhotoSurface.CaptureMouse()) return;
        panStart = e.GetPosition(PhotoViewport);
        panOffset = new Point(PhotoViewport.HorizontalOffset, PhotoViewport.VerticalOffset);
        UpdatePhotoCursor();
        e.Handled = true;
    }

    private void PhotoPanMoved(object sender, MouseEventArgs e)
    {
        if (CropMouseMove(e)) { e.Handled = true; return; }
        if (panStart is not { } start) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndPan(); return; }
        PanFrom(panOffset, e.GetPosition(PhotoViewport) - start);
        e.Handled = true;
    }

    internal void PanFrom(Point offsets, Vector movement)
    {
        PhotoViewport.ScrollToHorizontalOffset(Math.Clamp(offsets.X - movement.X, 0, PhotoViewport.ScrollableWidth));
        PhotoViewport.ScrollToVerticalOffset(Math.Clamp(offsets.Y - movement.Y, 0, PhotoViewport.ScrollableHeight));
    }

    private void PhotoPanReleased(object sender, MouseButtonEventArgs e)
    {
        if (cropDrag is not null)
        {
            UpdateCropDrag(e.GetPosition(PhotoSurface));
            EndCropDrag();
            e.Handled = true;
            return;
        }
        if (panStart is null) return;
        EndPan();
        e.Handled = true;
    }

    private void PhotoPanCaptureLost(object sender, MouseEventArgs e)
    {
        if (cropDrag is not null) EndCropDrag();
        EndPan();
    }

    private void EndPan()
    {
        if (panStart is null) return;
        panStart = null;
        if (PhotoSurface.IsMouseCaptured) PhotoSurface.ReleaseMouseCapture();
        UpdatePhotoCursor();
    }

    private void LayoutPhoto()
    {
        if (source is null || PhotoSurface is null || PhotoViewport.ActualWidth <= 1 || PhotoViewport.ActualHeight <= 1)
        {
            photoBounds = Rect.Empty;
            return;
        }

        double availableWidth = Math.Floor(PhotoViewport.ActualWidth);
        double availableHeight = Math.Floor(PhotoViewport.ActualHeight);
        double viewWidth = Math.Floor(Math.Min(availableWidth, PhotoViewport.ViewportWidth > 0 ? PhotoViewport.ViewportWidth : availableWidth));
        double viewHeight = Math.Floor(Math.Min(availableHeight, PhotoViewport.ViewportHeight > 0 ? PhotoViewport.ViewportHeight : availableHeight));
        var (imageWidth, imageHeight) = DisplayedGeometry.Size(source.Width, source.Height);
        double fit = Math.Min(availableWidth / imageWidth, availableHeight / imageHeight);
        double scale = fit * Math.Pow(1.25, zoomStep);
        double width = imageWidth * scale, height = imageHeight * scale;
        if (zoomStep == 0)
        {
            width = Math.Min(width, availableWidth);
            height = Math.Min(height, availableHeight);
        }
        PhotoSurface.Width = Math.Max(viewWidth, width);
        PhotoSurface.Height = Math.Max(viewHeight, height);
        photoBounds = new Rect((PhotoSurface.Width - width) / 2, (PhotoSurface.Height - height) / 2, width, height);
        PreviewImage.Width = width;
        PreviewImage.Height = height;
        Canvas.SetLeft(PreviewImage, photoBounds.X);
        Canvas.SetTop(PreviewImage, photoBounds.Y);
        AngleGuides.Visibility = Visible(fineRotating);
        AngleGuides.Width = width;
        AngleGuides.Height = height;
        Canvas.SetLeft(AngleGuides, photoBounds.X);
        Canvas.SetTop(AngleGuides, photoBounds.Y);
        DrawMarker();
        DrawCrop();
    }

    private void ViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (source is null) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            SetZoom(zoomStep + Math.Sign(e.Delta), ViewportAnchor());
        else if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && CanScroll(PhotoViewport.VerticalOffset, PhotoViewport.ScrollableHeight))
            PhotoViewport.ScrollToVerticalOffset(PhotoViewport.VerticalOffset - e.Delta / 2d);
        else if ((PhotoViewport.ScrollableHeight == 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) &&
            CanScroll(PhotoViewport.HorizontalOffset, PhotoViewport.ScrollableWidth))
            PhotoViewport.ScrollToHorizontalOffset(PhotoViewport.HorizontalOffset - e.Delta / 2d);
        else if (WorkspaceScroll.ScrollableHeight > 0)
            WorkspaceScroll.ScrollToVerticalOffset(WorkspaceScroll.VerticalOffset - e.Delta / 2d);
        else return;
        e.Handled = true;

        bool CanScroll(double offset, double maximum) => e.Delta > 0 ? offset > 0 : offset < maximum;
    }

    private void ScrollPointIntoView()
    {
        if (source is null || photoBounds.IsEmpty) return;
        var point = new Point(pickViewX, pickViewY);
        double x = photoBounds.X + point.X * photoBounds.Width;
        double y = photoBounds.Y + point.Y * photoBounds.Height;
        if (x < PhotoViewport.HorizontalOffset || x > PhotoViewport.HorizontalOffset + PhotoViewport.ViewportWidth ||
            y < PhotoViewport.VerticalOffset || y > PhotoViewport.VerticalOffset + PhotoViewport.ViewportHeight)
            ScrollToAnchor(point);
    }
}
