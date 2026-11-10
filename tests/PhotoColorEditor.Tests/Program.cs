using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using PhotoColorEditor;

internal static class Program
{
    [STAThread]
    public static void Main(string[] paths)
    {
        bool desktop = paths is [.., "--ui"];
        if (desktop) paths = paths[..^1];
        Require(paths.Length is 0 or 2, "Run with generated fixtures or supply two compatible photo paths. Add --ui for desktop tests.");
        if (paths.Length == 2)
        {
            RunChecks(paths, desktop);
            return;
        }

        var folder = Directory.CreateTempSubdirectory("photo-editor-synthetic-");
        try { RunChecks(SyntheticPhotos.Create(folder.FullName), desktop); }
        finally { folder.Delete(recursive: true); }
    }

    private static void RunChecks(string[] paths, bool desktop)
    {
        CheckPhoto(paths[0], .12, .05, .55, .10);
        CheckPhoto(paths[1], .95, .60, .20, .83);
        CheckHistory();
        CheckInvalidSamples();
        CheckSaveFailure();
        CheckFileDetails();
        CheckCurves();
        CheckLevels();
        CheckDetailEffects();
        CheckCopyNames();
        CheckLanguage();
        CheckGeometry();
        var prefs = new Preferences();
        for (int index = 0; index < 12; index++) prefs = prefs.WithRecentPhoto($@"C:\Photos\{index}.jpg");
        prefs = prefs.WithRecentPhoto(@"c:\photos\7.JPG");
        Require(prefs.RecentPhotos is { Length: 10 } recent && recent[0].EndsWith("7.JPG") &&
            recent.Count(path => path.EndsWith("7.jpg", StringComparison.OrdinalIgnoreCase)) == 1 &&
            !recent.Any(path => Path.GetFileName(path) == "1.jpg"), "Recent photos are bounded, newest first and deduplicated.");
        Console.WriteLine("PASS: both fixtures, neutral sampling, automatic correction, export, and undo history.");
        if (desktop) WindowSmoke.Run(paths[0], paths[1]);
    }

    private static void CheckPhoto(string path, double neutralX, double neutralY, double colorX, double colorY)
    {
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(path));
        PixelImage image = PhotoFiles.Open(path).Image;
        int x = (int)(image.Width * neutralX), y = (int)(image.Height * neutralY);
        int colorColumn = (int)(image.Width * colorX), colorRow = (int)(image.Height * colorY);
        var source = ColorEngine.AveragePatch(image, x, y) ?? throw new Exception("Fixture sample is invalid.");
        var balance = ColorEngine.PickBalance(image, x, y) ?? throw new Exception("Valid neutral sample must produce a correction.");
        var edited = ColorEngine.Render(image, EditSettings.Default with { Balance = balance });
        var corrected = ColorEngine.AveragePatch(edited, x, y) ?? throw new Exception("Corrected sample is invalid.");
        double beforeSpread = Spread(source), afterSpread = Spread(corrected);
        Require(afterSpread < .009 && afterSpread < beforeSpread, "Sample should become neutral without losing its shading.");
        Require(Math.Abs(Luminance(source) - Luminance(corrected)) / Luminance(source) < .01, "Manual white balance must preserve sample luminance.");
        var color = ColorEngine.AveragePatch(edited, colorColumn, colorRow);
        Require(color is { } colored && Spread(colored) > .025, "Colored regions must remain colored.");

        var automatic = ColorEngine.AutoBalance(image) ?? throw new Exception("Fixture has sufficient near-neutral content for automatic correction.");
        var auto = ColorEngine.Render(image, EditSettings.Default with { Balance = automatic });
        var autoSample = ColorEngine.AveragePatch(auto, x, y) ?? throw new Exception("Auto sample is invalid.");
        Require(Spread(autoSample) < beforeSpread, "Automatic white balance must reduce the paper's color cast.");
        var optimized = ColorEngine.Render(image, ColorEngine.AutoOptimize(image, EditSettings.Default));
        Require(!optimized.Pixels.AsSpan().SequenceEqual(image.Pixels), "Automatic optimization must change the edited pixels.");

        string folder = Directory.CreateTempSubdirectory("照片调整-测试 ").FullName;
        try
        {
            foreach (string extension in new[] { ".png", ".jpg" })
            {
                string exported = Path.Combine(folder, "已调整" + extension);
                PhotoFiles.Save(edited, exported);
                PixelImage reopened = PhotoFiles.Open(exported).Image;
                Require(reopened.Width == image.Width && reopened.Height == image.Height, "Export must preserve dimensions.");
                var reopenedSample = ColorEngine.AveragePatch(reopened, x, y);
                Require(reopenedSample is { } rgb && Spread(rgb) < .025, "Reopened output must retain neutralization.");
            }
            Require(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Save As must preserve the source file byte for byte.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"PASS: {Path.GetFileName(path)} · {image.Width}×{image.Height} · neutral spread {beforeSpread:F4} → {afterSpread:F4} (auto {Spread(autoSample):F4})");
    }

    private static void CheckHistory()
    {
        var history = new EditHistory();
        Require(!history.Commit(EditSettings.Default), "No-op does not add history.");
        for (int step = 1; step <= 22; step++) history.Commit(EditSettings.Default with { Exposure = step / 25.0 });
        int undo = 0;
        while (history.Undo()) undo++;
        Require(undo == 21 && history.Current.Exposure == .04, "Exactly 21 undo steps remain after 22 edits.");
        int redo = 0;
        while (history.Redo()) redo++;
        Require(redo == 21 && history.Current.Exposure == .88, "All 21 undone adjustments can be redone.");
        Require(history.Undo(), "Undo can be used after reaching the end of redo.");
        history.Commit(history.Current with { Saturation = 1.2 });
        Require(!history.CanRedo, "New edit clears redo branch.");
    }

    private static void CheckInvalidSamples()
    {
        foreach (byte[] pixel in new byte[][] { [255,255,255,255], [0,0,0,255], [50,65,80,0] })
        {
            var image = new PixelImage(5, 5, Enumerable.Range(0, 25).SelectMany(_ => pixel).ToArray());
            Require(ColorEngine.PickBalance(image, 2, 2) is null, "Clipped, dark and transparent samples must be rejected.");
            Require(ColorEngine.PickBalance(image, -1, 2) is null, "Outside samples must be rejected.");
        }
        byte[] pixels = [110, 115, 120, 80];
        var adjusted = ColorEngine.Render(new PixelImage(1, 1, pixels), EditSettings.Default with { Exposure = .25 });
        Require(adjusted.Pixels[3] == 80 && pixels.SequenceEqual(new byte[] {110,115,120,80}), "Rendering preserves alpha and immutable source pixels.");
    }

    private static void CheckSaveFailure()
    {
        string folder = Directory.CreateTempSubdirectory("photo-save-test-").FullName;
        try
        {
            string path = Path.Combine(folder, "protected.png");
            byte[] original = [1, 2, 3, 4];
            File.WriteAllBytes(path, original);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool refused = false;
                try { PhotoFiles.Save(new PixelImage(1, 1, [0, 0, 0, 255]), path, overwrite: true); }
                catch (IOException) { refused = true; }
                Require(refused, "A locked destination must fail safely.");
            }
            Require(File.ReadAllBytes(path).SequenceEqual(original), "Failed overwrite must leave original bytes intact.");
            Require(Directory.GetFiles(folder).Length == 1, "Failed overwrite must clean up its temporary file.");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void CheckCurves()
    {
        var curve = new ToneCurve([new(0, 0), new(.5, .72), new(1, 1)]);
        Require(curve.Apply(.25) == .36 && curve.Apply(.5) == .72, "Curve interpolation follows control points.");
        var image = new PixelImage(1, 1, [128, 128, 128, 255]);
        var red = ColorEngine.Render(image, EditSettings.Default.WithCurve(1, curve));
        Require(red.Pixels[2] > 175 && red.Pixels[1] == 128 && red.Pixels[0] == 128, "Red curve changes only red.");
    }

    private static void CheckFileDetails()
    {
        string folder = Directory.CreateTempSubdirectory("photo-file-details-").FullName;
        try
        {
            string png = Path.Combine(folder, "半透明.png");
            var image = new PixelImage(2, 1, [60, 80, 140, 64, 100, 20, 200, 255]);
            var fingerprint = PhotoFiles.Save(image, png);
            var reopened = PhotoFiles.Open(png).Image;
            Require(reopened.Pixels[3] == 64 && reopened.Pixels[7] == 255, "PNG export preserves alpha.");
            PhotoFiles.Save(image with { Pixels = [40, 80, 140, 64, 100, 20, 200, 255] }, png, true, fingerprint);
            var updated = File.ReadAllBytes(png);
            bool conflict = false;
            try { PhotoFiles.Save(image, png, true, fingerprint); }
            catch (PhotoConflictException) { conflict = true; }
            Require(conflict && File.ReadAllBytes(png).SequenceEqual(updated), "External changes cause a conflict instead of an overwrite.");

            string jpeg = Path.Combine(folder, "orientation.jpg");
            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
            var metadata = new System.Windows.Media.Imaging.BitmapMetadata("jpg");
            metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(PhotoFiles.Bitmap(image), null, metadata, null));
            using (var stream = File.Create(jpeg)) encoder.Save(stream);
            var rotated = PhotoFiles.Open(jpeg).Image;
            Require(rotated.Width == 1 && rotated.Height == 2, "EXIF orientation is applied on open.");

            string taggedPng = Path.Combine(folder, "orientation.png");
            byte[] pngBytes = File.ReadAllBytes(png);
            using (var stream = File.Create(taggedPng))
            {
                stream.Write(pngBytes.AsSpan(0, 33));
                WritePngExifChunk(stream);
                stream.Write(pngBytes.AsSpan(33));
            }
            var rotatedPng = PhotoFiles.Open(taggedPng).Image;
            Require(rotatedPng.Width == 1 && rotatedPng.Height == 2 && rotatedPng.Pixels[3] == 64,
                "PNG eXIf orientation and transparency are both preserved.");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void WritePngExifChunk(Stream stream)
    {
        byte[] chunk = [.. "eXIf"u8, 0x49, 0x49, 42, 0, 8, 0, 0, 0, 1, 0, 0x12, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0];
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, chunk.Length - 4);
        stream.Write(value);
        stream.Write(chunk);
        uint crc = 0xffffffff;
        foreach (byte b in chunk)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        stream.Write(value);
    }

    private static void CheckLevels()
    {
        byte[] tones = [0, .. Enumerable.Repeat((byte)70, 100), 125, .. Enumerable.Repeat((byte)180, 100), 255];
        var image = new PixelImage(tones.Length, 1, tones.SelectMany(value => new byte[] { value, value, value, 255 }).ToArray());
        var settings = ColorEngine.AutoLevels(image, EditSettings.Default);
        var edited = ColorEngine.Render(image, settings).Pixels;
        Require(!ColorEngine.LightHistogram(image).SequenceEqual(ColorEngine.LightHistogram(image with { Pixels = edited })),
            "The post-edit histogram reflects the adjusted pixels.");
        Require(edited[4] < 60 && edited[408] > 190 && edited[0] == 0 && edited[^4] == 255,
            "Auto Levels stretches the useful tones while preserving the endpoints.");
        Require(edited[4] == edited[5] && edited[5] == edited[6] && settings == ColorEngine.AutoLevels(image, settings),
            "Auto Levels keeps neutral tones neutral and can be reapplied without stacking.");
        var manual = ColorEngine.Render(image, EditSettings.Default with { Shadows = 1, Highlights = -1 }).Pixels;
        Require(manual[4] > 70 && manual[408] < 180, "Shadow lift and highlight reduction operate in their respective ranges.");
        var flat = new PixelImage(2, 1, [122, 122, 122, 255, 122, 122, 122, 255]);
        Require(ColorEngine.AutoLevels(flat, EditSettings.Default) == EditSettings.Default, "Auto Levels leaves flat input stable.");
    }

    private static void CheckDetailEffects()
    {
        var step = new PixelImage(6, 1, [80,80,80,255, 80,80,80,255, 80,80,80,255, 170,170,170,255, 170,170,170,255, 170,170,170,255]);
        var soft = ColorEngine.Render(step, EditSettings.Default with { SoftLightLevel = 1 }).Pixels;
        Require(soft[0] < 80 && soft[^4] > 170 && soft[0] == soft[1] && soft[1] == soft[2], "Soft Light adds gentle tonal contrast to neutrals.");
        var sharp = ColorEngine.Render(step, EditSettings.Default with { SharpenLevel = 1 }).Pixels;
        Require(sharp[0] == 80 && sharp[^4] == 170 && sharp[8] < 80 && sharp[12] > 170, "Sharpen increases edge contrast and preserves flat areas.");
        for (int level = 2; level <= EditSettings.MaxDetailLevel; level++)
        {
            var nextSoft = ColorEngine.Render(step, EditSettings.Default with { SoftLightLevel = level }).Pixels;
            var nextSharp = ColorEngine.Render(step, EditSettings.Default with { SharpenLevel = level }).Pixels;
            Require(nextSoft[0] < soft[0] && nextSoft[^4] > soft[^4], "Each Soft Light level adds more tonal contrast.");
            Require(nextSharp[8] < sharp[8] && nextSharp[12] > sharp[12], "Each Sharpen level adds more edge contrast.");
            soft = nextSoft;
            sharp = nextSharp;
        }
        var transparent = new PixelImage(2, 1, [100,100,100,255, 255,255,255,0]);
        var output = ColorEngine.Render(transparent, EditSettings.Default with { SharpenLevel = EditSettings.MaxDetailLevel });
        Require(output.Pixels.SequenceEqual(transparent.Pixels), "Sharpen preserves alpha and avoids halos from invisible neighbors.");
    }

    private static void CheckCopyNames()
    {
        var folder = Directory.CreateTempSubdirectory("photo-copy-names-");
        try
        {
            var original = Path.Combine(folder.FullName, "OriginalA.png");
            Require(PhotoFiles.SuggestCopyName(original, folder.FullName, "-已调整") == "OriginalA-已调整", "First copy uses the source stem.");
            File.WriteAllBytes(Path.Combine(folder.FullName, "OriginalA-已调整.png"), []);
            File.WriteAllBytes(Path.Combine(folder.FullName, "originala-已调整-2.JPG"), []);
            Directory.CreateDirectory(Path.Combine(folder.FullName, "OriginalA-已调整-3.png"));
            Require(PhotoFiles.SuggestCopyName(original, folder.FullName, "-已调整") == "OriginalA-已调整-4",
                "Save As skips existing files and folders across available formats.");
            var reopened = Path.Combine(folder.FullName, "OriginalA-已调整-2.png");
            Require(PhotoFiles.SuggestCopyName(reopened, folder.FullName, "-edited") == "OriginalA-edited", "Reopening a generated copy keeps a clean stem.");
        }
        finally { folder.Delete(recursive: true); }
    }

    private static void CheckLanguage()
    {
        foreach (var (culture, expected) in new[] { ("zh-CN", "zh-Hans"), ("zh-SG", "zh-Hans"), ("zh-TW", "zh-Hant"),
            ("zh-HK", "zh-Hant"), ("zh-MO", "zh-Hant"), ("en-US", "en"), ("ja-JP", "en") })
            Require(AppLanguage.Resolve("system", System.Globalization.CultureInfo.GetCultureInfo(culture)) == expected,
                $"System language fallback for {culture} must be {expected}.");
        Require(AppLanguage.Resolve("zh-Hant", System.Globalization.CultureInfo.GetCultureInfo("en-US")) == "zh-Hant", "An explicit Traditional Chinese selection overrides the system.");
        Require(Strings.Get("Title", "zh-Hans") == "照片颜色纠正器" && Strings.Get("Title", "zh-Hant") == "照片顏色校正器" &&
            Strings.Get("Title", "en") == "Photo Color Editor", "Application titles must be localized.");
        Require(OpenWithRegistration.OpenCommand(@"C:\中文 資料\PhotoColorEditor.exe") == "\"C:\\中文 資料\\PhotoColorEditor.exe\" \"%1\"",
            "Windows must pass the quoted filename to the quoted executable path.");
        Require(OpenWithRegistration.ContextMenuLabel("en") == "Open with Photo Color Editor" &&
            OpenWithRegistration.ContextMenuLabel("zh-Hans") == "用照片颜色纠正器打开" &&
            OpenWithRegistration.ContextMenuLabel("zh-Hant") == "使用照片顏色校正器開啟", "The direct context command uses the Windows display language.");
        const string movedExe = @"C:\新 資料\PhotoColorEditor.exe";
        Require(OpenWithRegistration.RegisteredExecutable(OpenWithRegistration.OpenCommand(movedExe)) == movedExe &&
            OpenWithRegistration.RegisteredExecutable("some-other-command %1") is null,
            "Startup must recover the exact registered executable path, including spaces and Chinese characters.");
    }

    private static void CheckGeometry()
    {
        var image = new PixelImage(4, 3, Enumerable.Range(1, 12).SelectMany(i => new byte[] { (byte)i, 0, 0, 255 }).ToArray());
        var frame = default(PhotoGeometry).Rotate(1);
        var rotated = frame.Apply(image, image.Width, image.Height, default);
        Require(rotated is { Width: 3, Height: 4 } && rotated.Pixels[0] == 9 && rotated.Pixels[^4] == 4, "90-degree rotation maps source pixels and swaps dimensions.");
        var cropped = frame.CropTo(0, 0, 2d / 3, .5, 4, 3).Apply(image, 4, 3, default);
        Require(cropped is { Width: 2, Height: 2 } && cropped.Pixels.Where((_, i) => i % 4 == 0).SequenceEqual(new byte[] { 9, 5, 10, 6 }),
            "Cropping a rotated view keeps the selected source pixels.");
        Require(frame.Rotate(-1) == default, "Opposite rotations restore the original orientation.");
        var fine = frame.CropTo(.2, .15, .8, .6, 200, 80).WithAngle(-3.25);
        var visible = fine.SourcePoint(.65, .3, 200, 80);
        var view = fine.ViewPoint(visible.X, visible.Y, 200, 80);
        Require(Math.Abs(view.X - .65) < .02 && Math.Abs(view.Y - .3) < .02,
            "A precise rotation and crop must map visible points back to the original pixels.");
        var solid = new PixelImage(12, 3, Enumerable.Repeat(new byte[] { 38, 92, 165, 255 }, 36).SelectMany(pixel => pixel).ToArray());
        foreach (double angle in new[] { -15d, 2.35, 15d })
            Require(default(PhotoGeometry).WithAngle(angle).Apply(solid, 12, 3, default).Pixels.SequenceEqual(solid.Pixels),
                "Straightening a solid panorama must preserve dimensions, color, and full corner coverage.");
        var edited = EditSettings.Default with { Exposure = .23, Balance = new WhiteBalance(new Rgb(.8, 1, 1.1)), SoftLightLevel = 3, SharpenLevel = 4, Geometry = frame };
        Require(MainWindow.AutoEnhance(image, edited) == edited, "Auto Enhance preserves existing exposure, white balance, effects and framing.");
    }

    private static double Spread(Rgb rgb) => Math.Max(rgb.R, Math.Max(rgb.G, rgb.B)) - Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
    private static double Luminance(Rgb rgb) => .2126 * rgb.R + .7152 * rgb.G + .0722 * rgb.B;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
