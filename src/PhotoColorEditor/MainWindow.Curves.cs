using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PhotoColorEditor;

public partial class MainWindow
{
    private void LightHistogramResized(object sender, SizeChangedEventArgs e) => DrawLightHistogram();

    private void DrawLightHistogram()
    {
        if (LightHistogram is not { ActualWidth: > 2, ActualHeight: > 2 } canvas ||
            originalLightHistogram is not { } baseline || currentLightHistogram is not { } current) return;
        canvas.Children.Clear();
        int peak = Math.Max(1, Math.Max(baseline.Max(), current.Max()));
        AddLightTrace(current, "Accent", 2.5, dashed: false);
        AddLightTrace(baseline, "MutedInk", 1.3, dashed: true);

        void AddLightTrace(int[] bins, string brush, double thickness, bool dashed)
        {
            var trace = new Polyline { StrokeThickness = thickness, IsHitTestVisible = false };
            trace.SetResourceReference(Shape.StrokeProperty, brush);
            if (dashed) trace.StrokeDashArray = [3, 2];
            for (int i = 0; i < bins.Length; i++)
            {
                double x = 1 + i * (canvas.ActualWidth - 2) / (bins.Length - 1);
                double y = 1 + (1 - Math.Sqrt(bins[i] / (double)peak)) * (canvas.ActualHeight - 2);
                trace.Points.Add(new Point(x, y));
            }
            canvas.Children.Add(trace);
        }
    }

    private int curvePoint = -1;
    private List<CurvePoint>? curveDraft;
    private int Channel => Math.Max(0, CurveChannel.SelectedIndex);

    private void AdvancedExpanded(object sender, RoutedEventArgs e) { DrawCurve(); DrawHistogram(); }
    private void HistogramResized(object sender, SizeChangedEventArgs e) => DrawHistogram();
    private void CurveResized(object sender, SizeChangedEventArgs e) => DrawCurve();

    private void CurveChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        curveDraft = null;
        curvePoint = -1;
        DrawCurve();
    }

    private void DrawHistogram()
    {
        if (lastPreview is not { } image || Histogram is null || Advanced?.IsExpanded != true || Histogram.ActualWidth <= 0) return;
        const int bins = 64;
        var counts = new int[3, bins];
        var pixels = image.Pixels;
        int peak = 1;
        for (int i = 0; i < pixels.Length; i += 12)
        {
            if (pixels[i + 3] < 128) continue;
            for (int channel = 0; channel < 3; channel++)
            {
                int bin = pixels[i + 2 - channel] / 4;
                peak = Math.Max(peak, ++counts[channel, bin]);
            }
        }
        Histogram.Children.Clear();
        Brush[] brushes = preferences.DarkTheme ? [Brushes.LightCoral, Brushes.LightGreen, Brushes.LightSkyBlue] :
            [Brushes.IndianRed, Brushes.SeaGreen, Brushes.SteelBlue];
        for (int channel = 0; channel < 3; channel++)
        {
            var line = new Polyline { Stroke = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : brushes[channel], StrokeThickness = 2, IsHitTestVisible = false };
            if (channel > 0) line.StrokeDashArray = channel == 1 ? [3, 1] : [1, 2];
            for (int bin = 0; bin < bins; bin++)
            {
                double level = Math.Sqrt((double)counts[channel, bin] / peak);
                line.Points.Add(new Point(bin * (Histogram.ActualWidth - 2) / (bins - 1), (1 - level) * (Histogram.ActualHeight - 3) + 1));
            }
            Histogram.Children.Add(line);
        }
    }

    private const double CurvePadding = 9;

    private Point ToCanvas(CurvePoint point) => new(CurvePadding + point.X * (CurveCanvas.ActualWidth - 2 * CurvePadding),
        CurvePadding + (1 - point.Y) * (CurveCanvas.ActualHeight - 2 * CurvePadding));

    private CurvePoint ToCurve(Point point) => new(Math.Clamp((point.X - CurvePadding) / (CurveCanvas.ActualWidth - 2 * CurvePadding), 0, 1),
        Math.Clamp(1 - (point.Y - CurvePadding) / (CurveCanvas.ActualHeight - 2 * CurvePadding), 0, 1));

    private void DrawCurve()
    {
        if (CurveCanvas is null || CurveChannel is null || CurveCanvas.ActualWidth <= 2 * CurvePadding || CurveCanvas.ActualHeight <= 2 * CurvePadding) return;
        IReadOnlyList<CurvePoint> points = curveDraft ?? history.Current.Curve(Channel).Points;
        var canvas = CurveCanvas;
        canvas.Children.Clear();
        for (int division = 0; division <= 4; division++)
        {
            double value = division / 4.0;
            AddGridLine(ToCanvas(new(value, 0)), ToCanvas(new(value, 1)));
            AddGridLine(ToCanvas(new(0, value)), ToCanvas(new(1, value)));
        }
        var curve = new Polyline { StrokeThickness = 2, IsHitTestVisible = false };
        curve.SetResourceReference(Shape.StrokeProperty, "Ink");
        foreach (var point in points) curve.Points.Add(ToCanvas(point));
        canvas.Children.Add(curve);
        for (int index = 0; index < points.Count; index++)
        {
            var p = ToCanvas(points[index]);
            bool selected = index == curvePoint;
            var handle = new Ellipse
            {
                Width = selected ? 13 : 10, Height = selected ? 13 : 10,
                StrokeThickness = 2, IsHitTestVisible = false
            };
            handle.SetResourceReference(Shape.FillProperty, selected ? "Accent" : "Panel");
            handle.SetResourceReference(Shape.StrokeProperty, "Ink");
            Canvas.SetLeft(handle, p.X - handle.Width / 2);
            Canvas.SetTop(handle, p.Y - handle.Height / 2);
            canvas.Children.Add(handle);
        }
    }

    private void AddGridLine(Point start, Point end)
    {
        var line = new Line { X1 = start.X, X2 = end.X, Y1 = start.Y, Y2 = end.Y, Opacity = .6, IsHitTestVisible = false };
        line.SetResourceReference(Shape.StrokeProperty, "ButtonEdge");
        CurveCanvas.Children.Add(line);
    }

    private void CurvePressed(object sender, MouseButtonEventArgs e)
    {
        if (source is null) return;
        StopPicking();
        CurveCanvas.Focus();
        var points = history.Current.Curve(Channel).Points.ToList();
        var mouse = e.GetPosition(CurveCanvas);
        curvePoint = points.FindIndex(p => (ToCanvas(p) - mouse).Length <= 11);
        if (curvePoint < 0)
        {
            if (points.Count >= 12) return;
            var point = ToCurve(mouse);
            curvePoint = points.FindIndex(p => p.X > point.X);
            if (curvePoint <= 0 || point.X - points[curvePoint - 1].X < .015 || points[curvePoint].X - point.X < .015) return;
            points.Insert(curvePoint, point);
        }
        if (curvePoint <= 0 || curvePoint >= points.Count - 1) { DrawCurve(); return; }
        curveDraft = points;
        CurveCanvas.CaptureMouse();
        DrawCurve();
    }

    private void CurveMoved(object sender, MouseEventArgs e)
    {
        if (curveDraft is not { } points || e.LeftButton != MouseButtonState.Pressed) return;
        MoveCurvePoint(points, ToCurve(e.GetPosition(CurveCanvas)));
        DrawCurve();
        RenderPreview(history.Current.WithCurve(Channel, new ToneCurve(points)));
    }

    private void MoveCurvePoint(List<CurvePoint> points, CurvePoint target)
    {
        double x = Math.Clamp(target.X, points[curvePoint - 1].X + .01, points[curvePoint + 1].X - .01);
        points[curvePoint] = new CurvePoint(x, Math.Clamp(target.Y, 0, 1));
    }

    private void CurveReleased(object sender, MouseButtonEventArgs e)
    {
        FinishCurve();
        if (CurveCanvas.IsMouseCaptured) CurveCanvas.ReleaseMouseCapture();
    }

    private void CurveCaptureLost(object sender, MouseEventArgs e) => FinishCurve();

    private void FinishCurve()
    {
        if (curveDraft is not { } points) return;
        curveDraft = null;
        if (!points.SequenceEqual(history.Current.Curve(Channel).Points))
            Commit(history.Current.WithCurve(Channel, points.Count == 2 ? null : new ToneCurve(points)));
        else DrawCurve();
    }

    private void CurveKeyDown(object sender, KeyEventArgs e)
    {
        if (source is null || curveDraft is not null) return;
        var current = history.Current.Curve(Channel);
        var points = current.Points.ToList();
        if (e.Key == Key.Enter && points.Count < 12)
        {
            int gap = Enumerable.Range(1, points.Count - 1).MaxBy(i => points[i].X - points[i - 1].X);
            double x = (points[gap].X + points[gap - 1].X) / 2;
            points.Insert(gap, new CurvePoint(x, current.Apply(x)));
            curvePoint = gap;
        }
        else if (e.Key == Key.Space && points.Count > 2)
        {
            curvePoint = curvePoint <= 0 || curvePoint >= points.Count - 2 ? 1 : curvePoint + 1;
            DrawCurve();
            e.Handled = true;
            return;
        }
        else if (curvePoint > 0 && curvePoint < points.Count - 1)
        {
            var point = points[curvePoint];
            switch (e.Key)
            {
                case Key.Delete: points.RemoveAt(curvePoint); curvePoint = -1; break;
                case Key.Left: MoveCurvePoint(points, point with { X = point.X - .02 }); break;
                case Key.Right: MoveCurvePoint(points, point with { X = point.X + .02 }); break;
                case Key.Up: MoveCurvePoint(points, point with { Y = point.Y + .02 }); break;
                case Key.Down: MoveCurvePoint(points, point with { Y = point.Y - .02 }); break;
                default: return;
            }
        }
        else return;
        e.Handled = true;
        if (!points.SequenceEqual(current.Points)) Commit(history.Current.WithCurve(Channel, points.Count == 2 ? null : new ToneCurve(points)));
    }

    private void ResetCurveClicked(object sender, RoutedEventArgs e)
    {
        curvePoint = -1;
        Commit(history.Current.WithCurve(Channel, null));
    }
}
