using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PhotoColorEditor;

internal static class WindowSmoke
{
    public static void Run(string first, string second)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Exception? failure = null;
        app.Dispatcher.InvokeAsync(async () =>
        {
            var settings = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                new Preferences(Language: "zh-Hans").Save(settings);
                var window = new MainWindow(settings) { ShowInTaskbar = false, ShowActivated = false, Opacity = 0 };
                window.Show();
                var workArea = SystemParameters.WorkArea;
                if (Math.Abs(window.ActualWidth - Math.Clamp(workArea.Width, window.MinWidth, 1240)) > 1 ||
                    Math.Abs(window.ActualHeight - Math.Clamp(workArea.Height, window.MinHeight, 900)) > 1)
                    throw new InvalidOperationException("The initial window should use the larger size when it fits in the desktop work area.");
                if (window.FindName("TopMenu") is not Menu menuBar || window.FindName("OpenToolbarButton") is not Button openButton ||
                    !ReferenceEquals(menuBar.Parent, openButton.Parent) || window.FindName("Filename") is not null)
                    throw new InvalidOperationException("The menu and common actions must share the top bar, leaving the title to show the filename.");
                foreach (var (menuName, tag, title, open) in new[]
                {
                    ("TraditionalChineseMenu", "zh-Hant", "照片顏色校正器", "開啟照片"),
                    ("EnglishMenu", "en", "Photo Color Editor", "Open photo"),
                    ("ChineseMenu", "zh-Hans", "照片颜色纠正器", "打开照片")
                })
                {
                    var menu = (MenuItem)window.FindName(menuName);
                    menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menu));
                    if (window.Title != title || !Equals(window.FindResource("Open"), open) || Preferences.Load(settings).Language != tag)
                        throw new InvalidOperationException("The selected language must update labels, title and stored settings.");
                    var about = window.CreateAboutWindow();
                    var lines = ((StackPanel)about.Content).Children.OfType<TextBlock>().ToArray();
                    var links = lines.SelectMany(line => line.Inlines.OfType<System.Windows.Documents.Hyperlink>()).Select(link => link.NavigateUri.AbsoluteUri);
                    if (!about.Title.Contains(title, StringComparison.Ordinal) || !lines.Any(line => line.Text.Contains("0.7 Beta")) ||
                        !lines.Any(line => line.Text == "SyncPort.ai") || !links.SequenceEqual(new[] { "https://github.com/vfyang/PhotoColorEditor", "mailto:contact@syncport.ai" }))
                        throw new InvalidOperationException("About must show the localized title, release version, publisher, repository and contact.");
                    about.Close();
                }
                CaptureReview(window, "empty");
                if (!window.AllowDrop) throw new InvalidOperationException("The editor must accept files from Explorer.");
                var fileDrop = new DataObject(DataFormats.FileDrop, new[] { first });
                if (MainWindow.DroppedPhoto(fileDrop) != first || MainWindow.DroppedPhoto(new DataObject(DataFormats.FileDrop, new[] { first, second })) is not null)
                    throw new InvalidOperationException("File drop must accept one supported photo at a time.");
                await CheckSampleWorkspace(window);
                await CheckWindow(window, first, .12, .05);
                window.Width = 700;
                await CheckWindow(window, second, .95, .60, drop: true);
                var saved = Preferences.Load(settings).RecentPhotos;
                if (saved is null || !saved.SequenceEqual(new[] { Path.GetFullPath(second), Path.GetFullPath(first) }))
                    throw new InvalidOperationException("Recent photos must persist newest first across sessions.");
                var recent = (MenuItem)window.FindName("RecentMenu");
                var firstPhoto = (MenuItem)recent.Items[1];
                firstPhoto.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, firstPhoto));
                await Until(window, () => window.Title.Contains(Path.GetFileName(first), StringComparison.Ordinal));
                window.Close();
            }
            catch (Exception error) { failure = error; }
            finally { File.Delete(settings); app.Shutdown(); }
        });
        app.Run();
        if (failure is not null) throw new InvalidOperationException("Desktop workflow failed.", failure);
        Console.WriteLine("PASS: WPF sample, sliders, stacked effects, crop, rotate, undo and full-size export, plus the existing photo workflow.");
    }

    private static async Task CheckSampleWorkspace(MainWindow window)
    {
        var card = (FrameworkElement)window.FindName("AdjustCard");
        var preview = (Image)window.FindName("PreviewImage");
        if (card.Visibility != Visibility.Collapsed) throw new InvalidOperationException("The empty workspace must focus on opening a photo or sample.");
        Click((Button)window.FindName("TrySampleButton"));
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 960, PixelHeight: 660 } && card.IsVisible);
        if (window.FindName("FineRotateButton") is not Button { IsVisible: true } fineRotate)
            throw new InvalidOperationException("The photo toolbar must provide precise rotation.");
        var original = Snapshot((BitmapSource)preview.Source);
        var darkTheme = (MenuItem)window.FindName("DarkThemeMenu");
        darkTheme.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, darkTheme));
        if (!darkTheme.IsChecked || ((SolidColorBrush)window.FindResource("Panel")).Color.R >= 100 && !SystemParameters.HighContrast)
            throw new InvalidOperationException("Dark theme must update the workspace palette.");
        Click(fineRotate);
        var angle = (TextBox)window.FindName("AngleInput");
        var applyAngle = (Button)window.FindName("ApplyAngleButton");
        Click((Button)window.FindName("AngleRightButton"));
        if (angle.Text != .5.ToString(System.Globalization.CultureInfo.CurrentCulture))
            throw new InvalidOperationException("The right nudge must rotate by exactly half a degree.");
        Click((Button)window.FindName("AngleLeftButton"));
        if (angle.Text != "0") throw new InvalidOperationException("The left nudge must rotate by exactly half a degree.");
        angle.Text = "NaN";
        if (applyAngle.IsEnabled) throw new InvalidOperationException("A non-finite angle must be rejected.");
        angle.Text = "15.01";
        if (applyAngle.IsEnabled) throw new InvalidOperationException("An out-of-range angle must be rejected.");
        angle.Text = "-2,35";
        await Until(window, () => applyAngle.IsEnabled && !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        CaptureReview(window, "straighten-dark");
        double normalWidth = window.Width;
        var english = (MenuItem)window.FindName("EnglishMenu");
        var chinese = (MenuItem)window.FindName("ChineseMenu");
        var largeText = (MenuItem)window.FindName("LargeTextMenu");
        english.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, english));
        largeText.IsChecked = true;
        largeText.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, largeText));
        window.Width = 520;
        window.UpdateLayout();
        var stage = (FrameworkElement)window.FindName("PreviewStage");
        var applyPosition = applyAngle.TranslatePoint(new Point(applyAngle.ActualWidth, applyAngle.ActualHeight), window);
        if (stage.ActualHeight < 210 || applyPosition.X > window.ActualWidth || applyPosition.Y >= stage.TranslatePoint(new Point(), window).Y)
            throw new InvalidOperationException("Large-text rotation controls must wrap above a usable preview in a narrow window.");
        CaptureReview(window, "straighten-narrow");
        window.Width = normalWidth;
        largeText.IsChecked = false;
        largeText.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, largeText));
        chinese.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, chinese));
        Click((Button)window.FindName("CancelAngleButton"));
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        if (ApplicationCommands.Undo.CanExecute(null, window))
            throw new InvalidOperationException("Canceling an angle must not add an undo step.");
        var lightTheme = (MenuItem)window.FindName("LightThemeMenu");
        lightTheme.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, lightTheme));
        Click(fineRotate);
        angle.Text = "2.35";
        Click(applyAngle);
        await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        var straightened = Snapshot((BitmapSource)preview.Source);
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        ApplicationCommands.Redo.Execute(null, window);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(straightened));
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        var expand = (Expander)window.FindName("FineTuneExpander");
        var slider = (Slider)window.FindName("ExposureSlider");
        expand.IsExpanded = true;
        await Until(window, () => slider.Template.FindName("PART_Track", slider) is Track { Thumb.ActualWidth: >= 28 } && slider.IsVisible);
        slider.Value = .25;
        await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        var track = (Track)slider.Template.FindName("PART_Track", slider);
        if (Math.Abs(track.Value - .25) > .0001) throw new InvalidOperationException("The visible slider must follow its adjustment value.");
        CaptureReview(window, "sample-sliders");
        expand.IsExpanded = false;
        var soft = (Button)window.FindName("SoftLightButton");
        var sharp = (Button)window.FindName("SharpenButton");
        Click(soft);
        Click(sharp);
        Click((Button)window.FindName("RotateLeftButton"));
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 660, PixelHeight: 960 });
        var crop = (Button)window.FindName("CropButton");
        Click(crop);
        if (window.FindName("CropHandles") is not Canvas { IsVisible: true, Children.Count: 8 } handles)
            throw new InvalidOperationException("The crop frame must display handles at all four corners and along all four edges.");
        var frame = (Border)window.FindName("CropFrame");
        DragCrop(.9, .5, .05, .5);
        CheckCropFrame(.1, .1, .105, .9);
        DragCrop(.02, .02, .62, .52);
        CheckCropFrame(.02, .02, .62, .52);
        Click((Button)window.FindName("CancelCropButton"));
        if (frame.IsVisible || preview.Source is not BitmapSource { PixelWidth: 660, PixelHeight: 960 })
            throw new InvalidOperationException("Canceling crop must discard the selection without changing the image.");

        Click(crop);
        DragCrop(.9, .9, .75, .7);
        CheckCropFrame(.1, .1, .75, .7);
        DragCrop(.1, .4, .2, .55);
        CheckCropFrame(.2, .1, .75, .7);
        DragCrop(.4, .4, 1.2, 1.3);
        CheckCropFrame(.45, .4, 1, 1);
        var surface = (FrameworkElement)window.FindName("PhotoSurface");
        if (handles.Children.Cast<FrameworkElement>().Any(handle => Canvas.GetLeft(handle) < 0 || Canvas.GetTop(handle) < 0 ||
            Canvas.GetLeft(handle) + handle.Width > surface.Width || Canvas.GetTop(handle) + handle.Height > surface.Height))
            throw new InvalidOperationException("Crop handles must stay visible when the frame touches the edge of the photo surface.");
        CaptureReview(window, "sample-crop");
        Click(crop);
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 363, PixelHeight: 576 });
        if (!soft.Content.ToString()!.EndsWith("1/5") || !sharp.Content.ToString()!.EndsWith("1/5"))
            throw new InvalidOperationException("Crop and rotation must retain both independent effects.");
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 660, PixelHeight: 960 });
        ApplicationCommands.Redo.Execute(null, window);
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 363, PixelHeight: 576 });
        var croppedBitmap = (BitmapSource)preview.Source;
        Click(fineRotate);
        angle.Text = "-3.75";
        Click(applyAngle);
        await Until(window, () => preview.Source != croppedBitmap && !Snapshot((BitmapSource)preview.Source).SequenceEqual(Snapshot(croppedBitmap)));

        var viewport = (ScrollViewer)window.FindName("PhotoViewport");
        var zoom = (Button)window.FindName("ZoomIn");
        var zoomReadout = (TextBlock)window.FindName("ZoomReadout");
        var reset = (Button)window.FindName("ResetOriginalButton");
        var editedPixels = Snapshot((BitmapSource)preview.Source);
        for (int click = 0; click < 5; click++) Click(zoom);
        await Until(window, () => viewport.ScrollableWidth > 0 && viewport.ScrollableHeight > 0);
        viewport.ScrollToHorizontalOffset(viewport.ScrollableWidth * .2);
        viewport.ScrollToVerticalOffset(viewport.ScrollableHeight * .7);
        await Until(window, () => Math.Abs(viewport.HorizontalOffset - viewport.ScrollableWidth * .2) < 1 &&
            Math.Abs(viewport.VerticalOffset - viewport.ScrollableHeight * .7) < 1);
        Click(reset);
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 960, PixelHeight: 660 });
        if (!Snapshot((BitmapSource)preview.Source).SequenceEqual(original) || zoomReadout.Text != "100%" ||
            viewport.ScrollableWidth != 0 || viewport.ScrollableHeight != 0 || viewport.HorizontalOffset != 0 || viewport.VerticalOffset != 0 ||
            slider.Value != 0 || !soft.Content.ToString()!.EndsWith("0/5") || !sharp.Content.ToString()!.EndsWith("0/5"))
            throw new InvalidOperationException("Reset to original must restore the full photo, neutral settings, and fit zoom.");
        if (!soft.IsEnabled || !sharp.IsEnabled || !crop.IsEnabled || !fineRotate.IsEnabled || reset.IsEnabled)
            throw new InvalidOperationException("After returning to the original, users must be able to start editing again immediately.");
        Click(soft);
        var newEdit = ColorEngine.Render(SamplePhoto.Create(warm: true), EditSettings.Default with { SoftLightLevel = 1 });
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 960, PixelHeight: 660 } bitmap &&
            Snapshot(bitmap).SequenceEqual(newEdit.Pixels));
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => preview.Source is BitmapSource { PixelWidth: 363, PixelHeight: 576 } bitmap &&
            Snapshot(bitmap).SequenceEqual(editedPixels));
        if (slider.Value != .25 ||
            !soft.Content.ToString()!.EndsWith("1/5") || !sharp.Content.ToString()!.EndsWith("1/5"))
            throw new InvalidOperationException("Undoing the reset must recover the settings and pixels from before the reset.");

        var folder = Directory.CreateTempSubdirectory("photo-sample-export-");
        try
        {
            string destination = Path.Combine(folder.FullName, "sample export.png");
            await window.SaveToAsync(destination, false, null);
            if (PhotoFiles.Open(destination).Image is not { Width: 363, Height: 576 } exported ||
                !exported.Pixels.SequenceEqual(Snapshot((BitmapSource)preview.Source)))
                throw new InvalidOperationException("Export must match the cropped, straightened preview.");
        }
        finally { folder.Delete(recursive: true); }

        Point PhotoPoint(double x, double y) => new(Canvas.GetLeft(preview) + x * preview.Width,
            Canvas.GetTop(preview) + y * preview.Height);

        void DragCrop(double fromX, double fromY, double toX, double toY)
        {
            window.UpdateLayout();
            if (!window.BeginCropDrag(PhotoPoint(fromX, fromY)))
                throw new InvalidOperationException("The crop frame should respond to a drag in the preview.");
            window.UpdateCropDrag(PhotoPoint(toX, toY));
            window.EndCropDrag();
        }

        void CheckCropFrame(double left, double top, double right, double bottom)
        {
            var start = PhotoPoint(left, top);
            var end = PhotoPoint(right, bottom);
            if (Math.Abs(Canvas.GetLeft(frame) - start.X) > .01 || Math.Abs(Canvas.GetTop(frame) - start.Y) > .01 ||
                Math.Abs(frame.Width - (end.X - start.X)) > .01 || Math.Abs(frame.Height - (end.Y - start.Y)) > .01)
                throw new InvalidOperationException("The crop frame moved or resized to the wrong bounds.");
        }
    }

    private static async Task CheckWindow(MainWindow window, string path, double fx, double fy, bool drop = false)
    {
        var preview = (Image)window.FindName("PreviewImage");
        var picker = (Button)window.FindName("PickButton");
        var reset = (Button)window.FindName("ResetOriginalButton");
        var surface = (FrameworkElement)window.FindName("PhotoSurface");
        var curve = (FrameworkElement)window.FindName("CurveCanvas");
        var marker = (FrameworkElement)window.FindName("Marker");
        var previousPreview = preview.Source;
        if (drop)
        {
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [new DataObject(DataFormats.FileDrop, new[] { Path.GetFullPath(path) }), DragDropKeyStates.None,
                    DragDropEffects.Copy, window, new Point(20, 20)], null)!;
            args.RoutedEvent = DragDrop.PreviewDropEvent;
            window.RaiseEvent(args);
            if (args.Effects != DragDropEffects.Copy) throw new InvalidOperationException("The editor did not accept the routed file drop.");
        }
        else await window.LoadPhotoAsync(path);
        await Until(window, () => preview.Source is BitmapSource && preview.Source != previousPreview && picker.IsEnabled);
        if (!window.Title.StartsWith(Path.GetFileName(path) + " — ", StringComparison.Ordinal))
            throw new InvalidOperationException("The filename must appear first in the window title.");
        var recent = (MenuItem)window.FindName("RecentMenu");
        if (recent.Items[0] is not MenuItem newest || !Equals(newest.Tag, Path.GetFullPath(path)))
            throw new InvalidOperationException("The most recently opened photo must be first in the menu.");
        var viewport = (ScrollViewer)window.FindName("PhotoViewport");
        var zoom = (Button)window.FindName("ZoomIn");
        var center = (Button)window.FindName("CenterPhoto");
        var fit = (Button)window.FindName("FitPhoto");
        if (!drop)
        {
            var histogram = (Canvas)window.FindName("LightHistogram");
            await Until(window, () => histogram.Children.Count == 2);
            var initial = Snapshot((BitmapSource)preview.Source);
            var initialTrace = ((System.Windows.Shapes.Polyline)histogram.Children[0]).Points.ToArray();
            var autoImage = PhotoFiles.Open(path).Image;
            var autoSettings = MainWindow.AutoEnhance(autoImage, EditSettings.Default);
            var autoPixels = ColorEngine.Render(autoImage, autoSettings).Pixels;
            var exposureSlider = (Slider)window.FindName("ExposureSlider");
            var autoValues = (FrameworkElement)window.FindName("AutoValues");
            var exposureReadout = (TextBlock)window.FindName("ExposureReadout");
            var balanceReadout = (TextBlock)window.FindName("BalanceReadout");
            var autoEnhance = (Button)window.FindName("AutoEnhanceButton");
            var menu = (Menu)window.FindName("TopMenu");
            var menuColor = ((SolidColorBrush)menu.Background).Color;
            var menuText = ((SolidColorBrush)menu.Foreground).Color;
            var buttonColor = ((SolidColorBrush)autoEnhance.Background).Color;
            Click(autoEnhance);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(autoPixels));
            var balance = autoSettings.Balance!.Value.Gains;
            if (!autoValues.IsVisible || exposureSlider.Value != autoSettings.Exposure ||
                exposureReadout.Text != Strings.Get("ExposureValue", "zh-Hans", autoSettings.Exposure) ||
                balanceReadout.Text != Strings.Get("AutoBalanceValue", "zh-Hans", balance.R, balance.G, balance.B))
                throw new InvalidOperationException("Auto Enhance must expose the exact exposure and white balance used by the preview.");
            Click(reset);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
            var buttonFrame = (Border)autoEnhance.Template.FindName("ButtonFrame", autoEnhance);
            var resetFrame = (Border)reset.Template.FindName("ButtonFrame", reset);
            if (!autoEnhance.IsEnabled || reset.IsEnabled || buttonFrame.Opacity != 1 || resetFrame.Opacity != 1 ||
                ((SolidColorBrush)buttonFrame.Background).Color != buttonColor ||
                ((SolidColorBrush)menu.Background).Color != menuColor || ((SolidColorBrush)menu.Foreground).Color != menuText)
                throw new InvalidOperationException("Applying an edit or restoring the original must keep button fills and the menu palette stable.");
            ApplicationCommands.Undo.Execute(null, window);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(autoPixels));
            ApplicationCommands.Undo.Execute(null, window);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial) && !autoValues.IsVisible);
            ApplicationCommands.Redo.Execute(null, window);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(autoPixels) && autoValues.IsVisible);
            ApplicationCommands.Undo.Execute(null, window);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
            Click((Button)window.FindName("AutoLevelsButton"));
            await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(initial) &&
                !((System.Windows.Shapes.Polyline)histogram.Children[0]).Points.SequenceEqual(initialTrace));
            ApplicationCommands.Undo.Execute(null, window);
            await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
            foreach (var (name, value) in new[] { ("ShadowsSlider", .7), ("HighlightsSlider", -.7) })
            {
                ((Slider)window.FindName(name)).Value = value;
                await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(initial) &&
                    !((System.Windows.Shapes.Polyline)histogram.Children[0]).Points.SequenceEqual(initialTrace));
                ApplicationCommands.Undo.Execute(null, window);
                await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
            }
            foreach (string effect in new[] { "SoftLightButton", "SharpenButton" })
            {
                var button = (Button)window.FindName(effect);
                Click(button);
                await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
                var firstPass = (BitmapSource)preview.Source;
                var firstPixels = Snapshot(firstPass);
                Click(button);
                await Until(window, () => preview.Source != firstPass);
                var secondPixels = Snapshot((BitmapSource)preview.Source);
                if (secondPixels.SequenceEqual(initial) || secondPixels.SequenceEqual(firstPixels))
                    throw new InvalidOperationException($"{effect} must increase the effect on the second click.");
                ApplicationCommands.Undo.Execute(null, window);
                await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(firstPixels));
                ApplicationCommands.Redo.Execute(null, window);
                await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(secondPixels));
                ApplicationCommands.Undo.Execute(null, window);
                ApplicationCommands.Undo.Execute(null, window);
                await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(initial));
            }
            var warmer = (Button)window.FindName("WarmerButton");
            var cooler = (Button)window.FindName("CoolerButton");
            var warmth = (Slider)window.FindName("WarmthSlider");
            Click(warmer);
            Click(warmer);
            if (warmth.Value != .4) throw new InvalidOperationException("Two warmer clicks must add two temperature steps.");
            Click(cooler);
            Click(cooler);
            Click(cooler);
            if (warmth.Value != -.2) throw new InvalidOperationException("Cooler must use the same step size in the opposite direction.");
            ApplicationCommands.Undo.Execute(null, window);
            if (warmth.Value != 0) throw new InvalidOperationException("Undo must restore neutral temperature after a single cooling step.");
            foreach (var (buttonName, sliderName, initialValue) in new[] { ("BrightenButton", "ExposureSlider", 0d), ("VividButton", "SaturationSlider", 1d) })
            {
                var stepButton = (Button)window.FindName(buttonName);
                var slider = (Slider)window.FindName(sliderName);
                for (int click = 0; click < 5; click++) Click(stepButton);
                if (slider.Value != initialValue + 1 || stepButton.IsEnabled)
                    throw new InvalidOperationException($"{buttonName} must accumulate and stop at its maximum.");
                ApplicationCommands.Undo.Execute(null, window);
                if (!stepButton.IsEnabled || Math.Abs(slider.Value - (initialValue + .8)) > .0001)
                    throw new InvalidOperationException($"Undo must restore the previous {buttonName} step.");
            }
            // Reset the session so the rest of the test can assert its own undo history.
            var beforeReset = preview.Source;
            await window.LoadPhotoAsync(path);
            await Until(window, () => preview.Source is BitmapSource && preview.Source != beforeReset);
        }
        double fitWidth = preview.ActualWidth;
        Click(zoom);
        await Until(window, () => viewport.ComputedVerticalScrollBarVisibility == Visibility.Visible &&
            viewport.ComputedHorizontalScrollBarVisibility == Visibility.Visible);
        for (int i = 0; i < 3; i++) Click(zoom);
        await Until(window, () => preview.ActualWidth > fitWidth && viewport.ScrollableHeight > 0 && viewport.ScrollableWidth > 0);
        viewport.ScrollToTop();
        viewport.ScrollToLeftEnd();
        await Until(window, () => viewport.VerticalOffset == 0 && viewport.HorizontalOffset == 0);
        Click(center);
        await Until(window, () => Math.Abs(viewport.VerticalOffset - viewport.ScrollableHeight / 2) < 2 &&
            Math.Abs(viewport.HorizontalOffset - viewport.ScrollableWidth / 2) < 2);
        var centerOffset = new Point(viewport.HorizontalOffset, viewport.VerticalOffset);
        window.PanFrom(centerOffset, new Vector(-12, -12));
        await Until(window, () => viewport.HorizontalOffset > centerOffset.X && viewport.VerticalOffset > centerOffset.Y);
        window.PanFrom(centerOffset, new Vector(12, 12));
        await Until(window, () => viewport.HorizontalOffset < centerOffset.X && viewport.VerticalOffset < centerOffset.Y);
        Click(center);
        await Until(window, () => Math.Abs(viewport.HorizontalOffset - centerOffset.X) < 2 && Math.Abs(viewport.VerticalOffset - centerOffset.Y) < 2);
        CaptureReview(window, window.Width < 900 ? "zoom-narrow" : "zoom-wide");
        var original = Snapshot((BitmapSource)preview.Source);
        int w = PhotoFiles.Open(path).Image.Width;
        int h = PhotoFiles.Open(path).Image.Height;
        Click(picker);
        if (!surface.Focusable) throw new InvalidOperationException("The picker did not enable keyboard input.");
        if (UIElementAutomationPeer.CreatePeerForElement(surface)?.GetName().Length is not > 0 ||
            UIElementAutomationPeer.CreatePeerForElement(curve)?.GetName().Length is not > 0)
            throw new InvalidOperationException("The preview and curve controls must expose names to assistive technology.");
        int step = Math.Max(4, Math.Max(w, h) / 70);
        Move((int)Math.Round((w * fx - w / 2) / step), Key.Left, Key.Right);
        Move((int)Math.Round((h * fy - h / 2) / step), Key.Up, Key.Down);
        KeyPress(Key.Enter);
        if (surface.Focusable || marker.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Sampling did not complete and show the point.");
        double expectedMarkerX = Canvas.GetLeft(preview) + preview.ActualWidth *
            (w / 2 + Math.Round((w * fx - w / 2) / step) * step + .5) / w;
        if (Math.Abs(Canvas.GetLeft(marker) + marker.ActualWidth / 2 - expectedMarkerX) > 2)
            throw new InvalidOperationException("The sampled marker must remain anchored to its photo pixel at zoom.");
        await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        Click(fit);
        await Until(window, () => viewport.ScrollableWidth == 0 && viewport.ScrollableHeight == 0);
        CaptureReview(window, window.Width < 900 ? "fit-narrow" : "fit-wide");

        Click(reset);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        ApplicationCommands.Undo.Execute(null, window);
        await Until(window, () => Snapshot((BitmapSource)preview.Source).SequenceEqual(original));
        ApplicationCommands.Redo.Execute(null, window);
        await Until(window, () => !Snapshot((BitmapSource)preview.Source).SequenceEqual(original));

        var folder = Directory.CreateTempSubdirectory("照片界面-测试 ");
        try
        {
            string copyStem = PhotoFiles.SuggestCopyName(path, folder.FullName, "-已调整");
            string destination = Path.Combine(folder.FullName, copyStem + ".jpg");
            await window.SaveToAsync(destination, false, null);
            var saved = PhotoFiles.Open(destination).Image;
            if (saved.Width != w || saved.Height != h || window.Title.EndsWith('*'))
                throw new InvalidOperationException("The saved image or dirty indicator is incorrect.");
            if (window.SuggestedCopyName() != copyStem + "-2")
                throw new InvalidOperationException("The second Save As must propose a new number on the original stem.");
            await window.SaveToAsync(Path.Combine(folder.FullName, copyStem + "-2.jpg"), false, null);
            if (window.SuggestedCopyName() != copyStem + "-3")
                throw new InvalidOperationException("The third Save As must continue the same sequence.");
        }
        finally { folder.Delete(recursive: true); }

        void Move(int times, Key negative, Key positive)
        {
            for (int count = 0; count < Math.Abs(times); count++) KeyPress(times < 0 ? negative : positive);
        }

        void KeyPress(Key key)
        {
            var presentation = PresentationSource.FromVisual(surface)!;
            surface.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, presentation, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static void CaptureReview(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PHOTO_EDITOR_REVIEW_DIR") is not { Length: > 0 } directory) return;
        var content = (FrameworkElement)window.Content;
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(directory);
        using var output = File.Create(Path.Combine(directory, name + ".png"));
        png.Save(output);
    }

    private static byte[] Snapshot(BitmapSource source)
    {
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }

    private static async Task Until(Window window, Func<bool> condition)
    {
        for (int i = 0; i < 100; i++)
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (condition()) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("The preview did not reach the expected state.");
    }
}
