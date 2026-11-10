using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoColorEditor;

public partial class MainWindow : Window
{
    private const double QuickStep = .2;
    private readonly string? settingsPath;
    private Preferences preferences;
    private string language = AppLanguage.English;
    private EditHistory history = new();
    private EditSettings saved = EditSettings.Default;
    private EditSettings presentedEdit = EditSettings.Default;
    private PixelImage? source, previewSource, lastPreview;
    private int[]? originalLightHistogram, currentLightHistogram;
    private string? path, openedPath;
    private string? sampleKey;
    private byte[]? fingerprint;
    private CancellationTokenSource? previewCancellation;
    private EditSettings? sliderDraft;
    private bool busy, allowClose, leavePending, syncing, picking;
    private int pickX, pickY;
    private double pickViewX, pickViewY;
    private string statusKey = "";
    private object[] statusArgs = [];

    public MainWindow() : this(null) { }

    internal MainWindow(string? settingsPath)
    {
        this.settingsPath = settingsPath;
        preferences = Preferences.Load(settingsPath);
        InitializeComponent();
        Rect workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(workArea.Width, MinWidth, Width);
        Height = Math.Clamp(workArea.Height, MinHeight, Height);
        ApplyPalette();
        SystemParameters.StaticPropertyChanged += SystemPaletteChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemPaletteChanged;
        ApplyPreferences();
        RefreshRecentMenu();
    }

    private bool Dirty => history.Current != saved;
    private bool HasActiveGesture => sliderDraft is not null || curveDraft is not null || fineRotating;
    private string T(string key, params object[] args) => Strings.Get(key, language, args);
    private static Visibility Visible(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyPreferences()
    {
        language = AppLanguage.Resolve(preferences.Language);
        foreach (string key in Strings.All.Keys) Resources[key] = T(key);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(language);
        FontFamily = new FontFamily(language == AppLanguage.Traditional ? "Microsoft JhengHei UI, Segoe UI" : "Microsoft YaHei UI, Segoe UI");
        FontSize = preferences.LargeText ? 20 : 16;
        SystemLanguageMenu.IsChecked = preferences.Language == AppLanguage.System;
        ChineseMenu.IsChecked = preferences.Language == AppLanguage.Simplified;
        TraditionalChineseMenu.IsChecked = preferences.Language == AppLanguage.Traditional;
        EnglishMenu.IsChecked = preferences.Language == AppLanguage.English;
        LargeTextMenu.IsChecked = preferences.LargeText;
        LightThemeMenu.IsChecked = !preferences.DarkTheme;
        DarkThemeMenu.IsChecked = preferences.DarkTheme;
        if (picking) Announce(PickHint, T("PickHelp"));
        RefreshUi();
    }

    private void LanguageClicked(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { Language = (string)((MenuItem)sender).Tag };
        ApplyPreferences();
        preferences.Save(settingsPath);
    }

    private void RegisterOpenWithClicked(object sender, RoutedEventArgs e) =>
        SetStatus(OpenWithRegistration.TryRegister(preferences.Language) ? "OpenWithReady" : "OpenWithFailed");

    private void ThemeClicked(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { DarkTheme = ((MenuItem)sender).Tag as string == "dark" };
        ApplyPalette();
        ApplyPreferences();
        preferences.Save(settingsPath);
    }

    private void LargeTextClicked(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { LargeText = LargeTextMenu.IsChecked };
        ApplyPreferences();
        preferences.Save(settingsPath);
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLayout();
        MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private void WorkspaceResized(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (!IsLoaded || WorkspaceScroll.ActualWidth == 0) return;
        bool stacked = WorkspaceScroll.ActualWidth < 900;
        AdjustColumn.Width = stacked || source is null ? new GridLength(0) : new GridLength(preferences.LargeText ? 340 : 320);
        PhotoRow.Height = stacked ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PreviewStage.Height = stacked ? Math.Clamp(WorkspaceScroll.ActualWidth * .60, 210, 440) : double.NaN;
        Grid.SetRow(AdjustCard, stacked ? 1 : 0);
        Grid.SetColumn(AdjustCard, stacked ? 0 : 1);
        PhotoPanel.Margin = source is null ? new Thickness(0) : stacked ? new Thickness(0, 0, 0, 14) : new Thickness(0, 0, 12, 0);
        WorkspaceScroll.VerticalScrollBarVisibility = stacked ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        AdjustScroll.VerticalScrollBarVisibility = stacked ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void SetStatus(string key, params object[] args)
    {
        statusKey = key;
        statusArgs = args;
        RefreshUi();
    }

    private void RefreshUi()
    {
        if (Status is null) return;
        string filename = PhotoName();
        Title = filename.Length == 0 ? T("Title") : $"{filename} — {T("Title")}{(Dirty ? " *" : "")}";
        bool hasPhoto = source is not null;
        EmptyState.Visibility = Visible(!hasPhoto);
        PreviewBadge.Visibility = Visible(hasPhoto);
        AdjustCard.Visibility = Visible(hasPhoto);
        AdjustPanel.IsEnabled = hasPhoto && !busy && !fineRotating;
        GeometryBar.Visibility = Visible(hasPhoto && !fineRotating);
        FineRotationBar.Visibility = Visible(fineRotating);
        FineRotationBar.IsEnabled = !busy;
        GeometryBar.IsEnabled = hasPhoto && !busy;
        ZoomBar.Visibility = Visible(hasPhoto);
        ZoomBar.IsEnabled = hasPhoto && !busy;
        RecentMenu.IsEnabled = CanOpenPhoto;
        PhotoSurface.Focusable = picking || cropping;
        ResetOriginalButton.IsEnabled = hasPhoto && !busy && history.Current != EditSettings.Default;
        PreviewLabel.Text = T(presentedEdit == EditSettings.Default ? "Original" : "Edited");
        PickButton.Content = T(picking ? "CancelPick" : "Pick");
        var edit = sliderDraft ?? history.Current;
        SyncAdjustmentControls(edit);
        BrightenButton.IsEnabled = edit.Exposure < ExposureSlider.Maximum;
        WarmerButton.IsEnabled = edit.Warmth < WarmthSlider.Maximum;
        CoolerButton.IsEnabled = edit.Warmth > WarmthSlider.Minimum;
        VividButton.IsEnabled = edit.Saturation < SaturationSlider.Maximum;
        UpdateDetailButton(SoftLightButton, "SoftLight", edit.SoftLightLevel);
        UpdateDetailButton(SharpenButton, "Sharpen", edit.SharpenLevel);
        string message = statusKey.Length == 0 ? "" : T(statusKey, statusArgs);
        if (hasPhoto)
        {
            string steps = T("Steps", history.UndoCount, history.RedoCount);
            message = message.Length == 0 ? steps : $"{message}  ·  {steps}";
            if (Dirty) message += $"  ·  {T("Unsaved")}";
        }
        Announce(Status, message);
        CommandManager.InvalidateRequerySuggested();
        ApplyLayout();
        LayoutPhoto();
        DrawCrop();
        RefreshZoomButtons();
    }

    private void UpdateDetailButton(Button button, string label, int level)
    {
        button.Content = $"{T(label)} + · {level}/{EditSettings.MaxDetailLevel}";
        button.IsEnabled = level < EditSettings.MaxDetailLevel;
    }

    private string PhotoName() => path is not null ? Path.GetFileName(path) : sampleKey is not null ? T(sampleKey) : "";

    private static void Announce(TextBlock label, string text)
    {
        if (label.Text == text) return;
        label.Text = text;
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            UIElementAutomationPeer.CreatePeerForElement(label)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private bool CanOpenPhoto => !busy && !leavePending && !HasActiveGesture;
    private void OpenCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = CanOpenPhoto;
    private void SaveCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && !leavePending && !HasActiveGesture && source is not null && Dirty;
    private void PhotoCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && !leavePending && !HasActiveGesture && source is not null;
    private void UndoCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && !HasActiveGesture && history.UndoCount > 0;
    private void RedoCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && !HasActiveGesture && history.CanRedo;

    private async void OpenCommand(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = T("PictureFilter"), Title = T("Open"), CheckFileExists = true };
        bool allowedToLeave = false;
        while (dialog.ShowDialog(this) == true)
        {
            if (!allowedToLeave && !await CanLeaveAsync()) return;
            allowedToLeave = true;
            string? failure = await OpenTargetAsync(dialog.FileName);
            if (failure is null) return;
            if (Ask(T("Open"), failure, T("RetryOpen"), T("Cancel")) != 0) return;
        }
    }

    internal async Task LoadPhotoAsync(string target)
    {
        var loaded = await Task.Run(() =>
        {
            var photo = PhotoFiles.Open(target);
            var preview = PhotoFiles.Preview(photo.Image);
            return (photo, preview, histogram: ColorEngine.LightHistogram(preview));
        });
        BeginPhoto(loaded.photo.Image, loaded.preview, loaded.histogram);
        fingerprint = loaded.photo.Fingerprint;
        sampleKey = null;
        path = openedPath = Path.GetFullPath(target);
        preferences = preferences.WithRecentPhoto(target);
        preferences.Save(settingsPath);
        RefreshRecentMenu();
        statusKey = "Opened";
        statusArgs = [Path.GetFileName(target)];
        RefreshEdit();
    }

    private void BeginPhoto(PixelImage image, PixelImage preview, int[] histogram)
    {
        fineRotating = false;
        StopPicking();
        StopCrop();
        source = image;
        previewSource = preview;
        originalLightHistogram = currentLightHistogram = histogram;
        DrawLightHistogram();
        history = new EditHistory();
        saved = EditSettings.Default;
        presentedEdit = EditSettings.Default;
        PreviewImage.Source = null;
        ResetViewport();
    }

    private async void SaveCommand(object sender, ExecutedRoutedEventArgs e) => await SaveAsync(false);
    private async void SaveAsCommand(object sender, ExecutedRoutedEventArgs e) => await SaveAsync(true);

    private async Task<bool> SaveAsync(bool saveAs)
    {
        if (source is null || busy) return false;
        StopCrop();
        saveAs |= path is null;
        var image = source;
        do
        {
            string target = path ?? "";
            if (saveAs)
            {
                string folder = ExportFolder();
                bool jpeg = path is not null && PhotoFiles.IsJpeg(path);
                var dialog = new SaveFileDialog
                {
                    Title = T("SaveAs"), Filter = T("ExportFilter"), DefaultExt = jpeg ? ".jpg" : ".png",
                    FilterIndex = jpeg ? 1 : 2, AddExtension = true, OverwritePrompt = false,
                    FileName = SuggestedCopyName(), InitialDirectory = folder
                };
                if (dialog.ShowDialog(this) != true) return false;
                target = dialog.FileName;
            }
            if (PhotoFiles.IsJpeg(target) && PhotoFiles.HasTransparency(image) &&
                Ask(T("SaveAs"), T("Transparency"), T("Continue"), T("Cancel")) != 0) return false;

            string? failure = null;
            bool conflict = false;
            try
            {
                bool sameFile = path is not null && string.Equals(Path.GetFullPath(target), path, StringComparison.OrdinalIgnoreCase);
                bool exists = File.Exists(target);
                byte[]? expected = sameFile ? fingerprint : exists ? PhotoFiles.Fingerprint(target) : null;
                if (sameFile && !exists) throw new PhotoConflictException();
                if (exists && Ask(T("Save"), T("BeforeSaving", target), T("Replace"), T("Cancel")) != 0) return false;
                busy = true;
                StopPicking();
                SetStatus("Saving", Path.GetFileName(target));
                await SaveToAsync(target, exists, expected);
                return true;
            }
            catch (PhotoConflictException) { failure = T("Conflict"); conflict = true; }
            catch (Exception error) when (FileError(error)) { failure = T("SaveFailure", target); }
            finally { busy = false; RefreshUi(); }
            statusKey = "";
            RefreshUi();
            if (Ask(T("Save"), failure!, T(conflict ? "SaveAs" : "ChooseLocation"), T("Cancel")) != 0) return false;
            saveAs = true;
        } while (saveAs);
        return false;
    }

    private string ExportFolder() => path is not null ? Path.GetDirectoryName(path)! :
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) is { Length: > 0 } pictures ? pictures : Environment.CurrentDirectory;

    internal string SuggestedCopyName() => PhotoFiles.SuggestCopyName(openedPath ?? T(sampleKey!) + ".png", ExportFolder(), T("CopySuffix"));

    internal async Task SaveToAsync(string target, bool exists, byte[]? expected)
    {
        var image = source ?? throw new InvalidOperationException();
        var settings = history.Current;
        fingerprint = await Task.Run(() => PhotoFiles.Save(RenderPhoto(image, settings, image.Width, image.Height), target, exists, expected));
        path = Path.GetFullPath(target);
        saved = settings;
        SetStatus("Saved", target);
    }

    private static bool FileError(Exception error) => error is IOException or FileFormatException or UnauthorizedAccessException or
        NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException;

    private async Task<bool> CanLeaveAsync()
    {
        if (!Dirty) return true;
        int choice = Ask(T("LeaveTitle"), T("LeaveMessage", PhotoName()), T("SaveAs"), T("Discard"), T("KeepEditing"));
        return choice == 1 || choice == 0 && await SaveAsync(true);
    }

    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose || !Dirty && !busy) { previewCancellation?.Cancel(); return; }
        e.Cancel = true;
        if (busy || leavePending) return;
        leavePending = true;
        try
        {
            if (!await CanLeaveAsync()) return;
            allowClose = true;
            _ = Dispatcher.BeginInvoke(Close);
        }
        finally { leavePending = false; }
    }

    private int Ask(string title, string message, params string[] options)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Owner = this, Title = title, Content = panel, Width = 510, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, FontFamily = FontFamily, FontSize = FontSize
        };
        int choice = options.Length - 1;
        for (int index = 0; index < options.Length; index++)
        {
            int selection = index;
            var button = new Button
            {
                Content = options[index], MinHeight = 44, Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(6, 6, 0, 0), IsDefault = index == choice, IsCancel = index == choice
            };
            button.Click += (_, _) => { choice = selection; dialog.DialogResult = selection != options.Length - 1; };
            if (index == choice) dialog.Loaded += (_, _) => button.Focus();
            buttons.Children.Add(button);
        }
        dialog.ShowDialog();
        return choice;
    }

    private void Commit(EditSettings settings, string status = "Adjusted")
    {
        StopCrop();
        sliderDraft = null;
        SetStatus(history.Commit(settings) ? status : "NoChange");
        RefreshEdit();
    }

    private void RefreshEdit()
    {
        RefreshUi();
        DrawCurve();
        RenderPreview(history.Current);
    }

    private void SyncAdjustmentControls(EditSettings edit)
    {
        syncing = true;
        ExposureSlider.Value = edit.Exposure;
        WarmthSlider.Value = edit.Warmth;
        TintSlider.Value = edit.Tint;
        SaturationSlider.Value = edit.Saturation;
        ShadowsSlider.Value = edit.Shadows;
        HighlightsSlider.Value = edit.Highlights;
        syncing = false;

        AutoValues.Visibility = Visible(edit.Exposure != 0 || edit.Balance is not null);
        ExposureReadout.Text = T("ExposureValue", edit.Exposure);
        BalanceReadout.Visibility = Visible(edit.Balance is not null);
        if (edit.Balance is { } balance)
            BalanceReadout.Text = T(balance.X is null ? "AutoBalanceValue" : "PickedBalanceValue", balance.Gains.R, balance.Gains.G, balance.Gains.B);
    }

    private void UndoCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (!history.Undo()) return;
        StopPicking();
        StopCrop();
        SetStatus("Undone");
        RefreshEdit();
    }

    private void RedoCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (!history.Redo()) return;
        StopPicking();
        StopCrop();
        SetStatus("Redone");
        RefreshEdit();
    }

    private void ResetOriginalClicked(object sender, RoutedEventArgs e)
    {
        if (source is null || busy || HasActiveGesture || history.Current == EditSettings.Default) return;
        StopPicking();
        ResetViewport();
        Commit(EditSettings.Default, "ResetOriginalApplied");
    }

    private void PresetClicked(object sender, RoutedEventArgs e)
    {
        if (source is null || previewSource is null || busy) return;
        StopPicking();
        var current = history.Current;
        var preset = (string)((Button)sender).Tag;
        var analysis = (current.Geometry.Crop is not null || current.Geometry.Angle != 0) && preset is "balance" or "levels" or "optimize"
            ? (current.Geometry with { Turns = 0 }).Apply(previewSource, source.Width, source.Height, default) : source;
        if (preset == "balance")
        {
            var balance = ColorEngine.AutoBalance(analysis);
            if (balance is null) SetStatus("AutoInsufficient");
            else Commit(current with { Balance = balance });
            return;
        }
        if (preset == "levels")
        {
            var adjusted = ColorEngine.AutoLevels(analysis, current);
            if (adjusted == current && current.Levels is null) SetStatus("LevelsStable");
            else Commit(adjusted);
            return;
        }
        Commit(preset switch
        {
            "optimize" => AutoEnhance(analysis, current),
            "bright" => current with { Exposure = StepAdjustment(current.Exposure, QuickStep, ExposureSlider) },
            "warm" => current with { Warmth = StepAdjustment(current.Warmth, QuickStep, WarmthSlider) },
            "cool" => current with { Warmth = StepAdjustment(current.Warmth, -QuickStep, WarmthSlider) },
            "vivid" => current with { Saturation = StepAdjustment(current.Saturation, QuickStep, SaturationSlider) },
            "soft-light" => current with { SoftLightLevel = Math.Min(current.SoftLightLevel + 1, EditSettings.MaxDetailLevel) },
            "sharpen" => current with { SharpenLevel = Math.Min(current.SharpenLevel + 1, EditSettings.MaxDetailLevel) },
            _ => current
        });
    }

    private static double StepAdjustment(double current, double delta, Slider slider) =>
        Math.Clamp(Math.Round(current + delta, 6), slider.Minimum, slider.Maximum);

    internal static EditSettings AutoEnhance(PixelImage image, EditSettings current)
    {
        var auto = ColorEngine.AutoOptimize(image, current);
        return current with { Balance = current.Balance ?? auto.Balance, Exposure = current.Exposure == 0 ? auto.Exposure : current.Exposure };
    }

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncing || source is null || busy) return;
        StopPicking();
        StopCrop();
        var current = sliderDraft ?? history.Current;
        var slider = (Slider)sender;
        var next = slider == ExposureSlider ? current with { Exposure = e.NewValue } :
            slider == WarmthSlider ? current with { Warmth = e.NewValue } :
            slider == TintSlider ? current with { Tint = e.NewValue } :
            slider == ShadowsSlider ? current with { Shadows = e.NewValue } :
            slider == HighlightsSlider ? current with { Highlights = e.NewValue } : current with { Saturation = e.NewValue };
        if (slider.IsMouseCaptureWithin)
        {
            sliderDraft = next;
            RenderPreview(next);
        }
        else Commit(next);
    }

    private void SliderReleased(object sender, MouseButtonEventArgs e) => FinishSlider();
    private void SliderCaptureLost(object sender, MouseEventArgs e) => FinishSlider();

    private void FinishSlider()
    {
        if (sliderDraft is { } settings) Commit(settings);
    }

    private async void RenderPreview(EditSettings settings)
    {
        previewCancellation?.Cancel();
        if (previewSource is not { } image || source is not { } full) return;
        var cancellation = new CancellationTokenSource();
        previewCancellation = cancellation;
        try
        {
            var result = await Task.Run(() =>
            {
                var pixels = RenderPhoto(image, settings, full.Width, full.Height, cancellation.Token);
                return (pixels, bitmap: PhotoFiles.Bitmap(pixels), histogram: ColorEngine.LightHistogram(pixels));
            }, cancellation.Token);
            if (previewCancellation != cancellation || cancellation.IsCancellationRequested) return;
            lastPreview = result.pixels;
            PreviewImage.Source = result.bitmap;
            presentedEdit = settings;
            PreviewLabel.Text = T(settings == EditSettings.Default ? "Original" : "Edited");
            LayoutPhoto();
            currentLightHistogram = result.histogram;
            DrawLightHistogram();
            DrawHistogram();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (FileError(error)) { SetStatus("PreviewFailure"); }
        finally
        {
            if (previewCancellation == cancellation) previewCancellation = null;
            cancellation.Dispose();
        }
    }

    private static PixelImage RenderPhoto(PixelImage image, EditSettings settings, int width, int height, CancellationToken token = default) =>
        ColorEngine.Render(settings.Geometry.Apply(image, width, height, token), settings with { Geometry = default }, token);

    private void PickClicked(object sender, RoutedEventArgs e)
    {
        if (source is null) return;
        if (picking) { StopPicking(true); return; }
        StopCrop();
        EndPan();
        picking = true;
        var prior = history.Current.Balance;
        var geometry = history.Current.Geometry;
        var point = prior is { X: int x, Y: int y } ? geometry.ViewPoint(x, y, source.Width, source.Height) : (.5, .5);
        (pickViewX, pickViewY) = point.Item1 is < 0 or > 1 || point.Item2 is < 0 or > 1 ? (.5, .5) : point;
        (pickX, pickY) = geometry.SourcePoint(pickViewX, pickViewY, source.Width, source.Height);
        UpdatePhotoCursor();
        PickHint.Visibility = Visibility.Visible;
        Announce(PickHint, T("PickHelp"));
        RefreshUi();
        PhotoSurface.Focus();
        PreviewStage.BringIntoView();
        ScrollPointIntoView();
        RenderPreview(history.Current);
    }

    private void StopPicking(bool returnFocus = false)
    {
        if (!picking) return;
        picking = false;
        UpdatePhotoCursor();
        PickHint.Visibility = Visibility.Collapsed;
        RefreshUi();
        if (returnFocus) PickButton.Focus();
    }

    private void PhotoClicked(object sender, MouseButtonEventArgs e)
    {
        if (cropping) { CropMouseDown(e); return; }
        if (!picking) { BeginPan(e); return; }
        if (source is null || DisplayedGeometry != history.Current.Geometry) return;
        var bounds = ImageRect();
        var point = e.GetPosition(PhotoSurface);
        if (bounds.IsEmpty || point.X < bounds.Left || point.X >= bounds.Right || point.Y < bounds.Top || point.Y >= bounds.Bottom) return;
        pickViewX = (point.X - bounds.Left) / bounds.Width;
        pickViewY = (point.Y - bounds.Top) / bounds.Height;
        (pickX, pickY) = DisplayedGeometry.SourcePoint(pickViewX, pickViewY, source.Width, source.Height);
        ApplyPoint();
    }

    private void PhotoKeyDown(object sender, KeyEventArgs e)
    {
        if (cropping && e.Key is Key.Enter or Key.Escape)
        {
            if (e.Key == Key.Enter) ApplyCrop();
            else { StopCrop(); CropButton.Focus(); }
            e.Handled = true;
            return;
        }
        if (!picking || source is null) return;
        if (DisplayedGeometry != history.Current.Geometry && e.Key != Key.Escape) return;
        int distance = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 1 : Math.Max(4, Math.Max(source.Width, source.Height) / 70);
        switch (e.Key)
        {
            case Key.Left: MovePick(-distance, 0); break;
            case Key.Right: MovePick(distance, 0); break;
            case Key.Up: MovePick(0, -distance); break;
            case Key.Down: MovePick(0, distance); break;
            case Key.Enter: ApplyPoint(); break;
            case Key.Escape: StopPicking(true); break;
            default: return;
        }
        e.Handled = true;
        DrawMarker();
        if (picking) ScrollPointIntoView();

        void MovePick(int dx, int dy)
        {
            var frame = DisplayedGeometry;
            var (width, height) = frame.Size(source.Width, source.Height);
            pickViewX = Math.Clamp(pickViewX + (double)dx / width, .5 / width, 1 - .5 / width);
            pickViewY = Math.Clamp(pickViewY + (double)dy / height, .5 / height, 1 - .5 / height);
            (pickX, pickY) = frame.SourcePoint(pickViewX, pickViewY, source.Width, source.Height);
        }
    }

    private void ApplyPoint()
    {
        if (source is null) return;
        var balance = ColorEngine.PickBalance(source, pickX, pickY);
        if (balance is null)
        {
            Announce(PickHint, T("PickInvalid"));
            DrawMarker();
            return;
        }
        StopPicking(true);
        Commit(history.Current with { Balance = balance, Warmth = 0, Tint = 0 }, "PickApplied");
    }

    private void DrawMarker()
    {
        if (Marker is null) return;
        var bounds = ImageRect();
        var balance = history.Current.Balance;
        bool show = !bounds.IsEmpty && !fineRotating && (picking || balance?.X is not null);
        Marker.Visibility = MarkerOutline.Visibility = Visible(show);
        if (!show || source is null) return;
        int x = picking ? pickX : balance!.Value.X!.Value;
        int y = picking ? pickY : balance!.Value.Y!.Value;
        var point = picking ? new Point(pickViewX, pickViewY) : PickViewPoint(x, y);
        if (point.X is < 0 or > 1 || point.Y is < 0 or > 1)
        {
            Marker.Visibility = MarkerOutline.Visibility = Visibility.Collapsed;
            return;
        }
        foreach (var marker in new[] { Marker, MarkerOutline })
        {
            Canvas.SetLeft(marker, bounds.Left + point.X * bounds.Width - marker.Width / 2);
            Canvas.SetTop(marker, bounds.Top + point.Y * bounds.Height - marker.Height / 2);
        }
    }
}
