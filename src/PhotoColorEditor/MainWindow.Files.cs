using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhotoColorEditor;

public partial class MainWindow
{
    internal Task OpenStartupPhotoAsync(string target) => OpenExternalPhotoAsync(target, recent: false);

    private async void SampleClicked(object sender, RoutedEventArgs e)
    {
        if (!CanOpenPhoto || !await CanLeaveAsync()) return;
        bool warm = (sender as FrameworkElement)?.Tag as string != "cool";
        await LoadSampleAsync(warm);
    }

    internal async Task LoadSampleAsync(bool warm)
    {
        busy = true;
        RefreshUi();
        try
        {
            var sample = await Task.Run(() =>
            {
                var image = SamplePhoto.Create(warm);
                return (image, histogram: ColorEngine.LightHistogram(image));
            });
            BeginPhoto(sample.image, sample.image, sample.histogram);
            fingerprint = null;
            path = openedPath = null;
            sampleKey = warm ? "WarmSample" : "CoolSample";
            SetStatus("SampleOpened");
            RefreshEdit();
        }
        finally { busy = false; RefreshUi(); }
    }

    private void RefreshRecentMenu()
    {
        RecentMenu.Items.Clear();
        var files = preferences.RecentPhotos ?? [];
        if (files.Length == 0)
        {
            var empty = new MenuItem { IsEnabled = false };
            empty.SetResourceReference(HeaderedItemsControl.HeaderProperty, "NoRecent");
            RecentMenu.Items.Add(empty);
            return;
        }

        var names = files.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            var entry = new MenuItem
            {
                Header = new TextBlock { Text = names.Contains(name) ? file : name, MaxWidth = 430, TextTrimming = TextTrimming.CharacterEllipsis },
                Tag = file, ToolTip = file
            };
            entry.Click += RecentClicked;
            RecentMenu.Items.Add(entry);
        }
    }

    private async void RecentClicked(object sender, RoutedEventArgs e) =>
        await OpenExternalPhotoAsync((string)((MenuItem)sender).Tag, recent: true);

    internal static string? DroppedPhoto(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] { Length: 1 } paths) return null;
        return PhotoFiles.IsJpeg(paths[0]) || string.Equals(Path.GetExtension(paths[0]), ".png", StringComparison.OrdinalIgnoreCase)
            ? paths[0] : null;
    }

    private void PreviewFileDrag(object sender, DragEventArgs e)
    {
        bool valid = CanOpenPhoto && (e.AllowedEffects & DragDropEffects.Copy) != 0 && DroppedPhoto(e.Data) is not null;
        DropOverlay.Visibility = Visible(valid);
        e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PreviewFileDragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;

    private async void PreviewFileDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (!CanOpenPhoto || (e.AllowedEffects & DragDropEffects.Copy) == 0) return;
        if (DroppedPhoto(e.Data) is not { } file) { SetStatus("DropSingle"); return; }
        e.Effects = DragDropEffects.Copy;
        await OpenExternalPhotoAsync(file, recent: false);
    }

    private async Task OpenExternalPhotoAsync(string target, bool recent)
    {
        if (!CanOpenPhoto || !await CanLeaveAsync()) return;
        if (await OpenTargetAsync(target, recent) is { } failure)
            Ask(T("Open"), failure, T("Cancel"));
    }

    private async Task<string?> OpenTargetAsync(string target, bool recent = false)
    {
        string name = Path.GetFileName(target);
        busy = true;
        SetStatus("Loading", name);
        try
        {
            await LoadPhotoAsync(target);
            return null;
        }
        catch (Exception error) when (recent && error is FileNotFoundException or DirectoryNotFoundException)
        {
            preferences = preferences.WithoutRecentPhoto(target);
            preferences.Save(settingsPath);
            RefreshRecentMenu();
            return T("RecentMissing", name);
        }
        catch (ImageSizeException) { return T("TooLarge"); }
        catch (Exception error) when (FileError(error)) { return T("OpenFailure", name); }
        finally
        {
            busy = false;
            if (statusKey == "Loading") statusKey = "";
            RefreshUi();
        }
    }
}
