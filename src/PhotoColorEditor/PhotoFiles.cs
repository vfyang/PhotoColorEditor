using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoColorEditor;

internal sealed record OpenPhoto(PixelImage Image, byte[] Fingerprint);

internal static class PhotoFiles
{
    public static OpenPhoto Open(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fingerprint = SHA256.HashData(stream);
        stream.Position = 0;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        if (decoder is not JpegBitmapDecoder and not PngBitmapDecoder) throw new NotSupportedException();
        var frame = decoder.Frames[0];
        int width = frame.PixelWidth, height = frame.PixelHeight;
        if ((long)width * height > 64_000_000) throw new ImageSizeException();
        BitmapSource normalized = frame;
        if (frame.ColorContexts is { Count: > 0 } contexts)
        {
            try { normalized = new ColorConvertedBitmap(frame, contexts[0], new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or FileFormatException or System.Runtime.InteropServices.COMException) { }
        }
        if (normalized.Format != PixelFormats.Bgra32)
            normalized = new FormatConvertedBitmap(normalized, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(width * height * 4)];
        normalized.CopyPixels(pixels, width * 4, 0);
        int orientation = decoder is PngBitmapDecoder ? PngOrientation(stream) : JpegOrientation(frame);
        return new OpenPhoto(Orient(new PixelImage(width, height, pixels), orientation), fingerprint);
    }

    private static int JpegOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is not BitmapMetadata meta) return 1;
            foreach (string tag in new[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
            {
                if (meta.GetQuery(tag) is ushort value and >= 1 and <= 8) return value;
            }
        }
        catch (Exception error) when (error is NotSupportedException or FileFormatException or ArgumentException) { }
        return 1;
    }

    private static int PngOrientation(Stream stream)
    {
        stream.Position = 8;
        Span<byte> header = stackalloc byte[8];
        while (stream.Length - stream.Position >= 12)
        {
            stream.ReadExactly(header);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (stream.Length - stream.Position < length + 4L || header[4..].SequenceEqual("IEND"u8)) break;
            if (header[4..].SequenceEqual("eXIf"u8))
            {
                if (length is < 8 or > 1_048_576) break;
                var data = new byte[length];
                stream.ReadExactly(data);
                return TiffOrientation(data);
            }
            stream.Seek(length + 4L, SeekOrigin.Current);
        }
        return 1;
    }

    private static int TiffOrientation(ReadOnlySpan<byte> data)
    {
        bool little = data[..2].SequenceEqual("II"u8);
        if (!little && !data[..2].SequenceEqual("MM"u8)) return 1;
        ushort Short(ReadOnlySpan<byte> value) => little ? BinaryPrimitives.ReadUInt16LittleEndian(value) : BinaryPrimitives.ReadUInt16BigEndian(value);
        uint Long(ReadOnlySpan<byte> value) => little ? BinaryPrimitives.ReadUInt32LittleEndian(value) : BinaryPrimitives.ReadUInt32BigEndian(value);
        if (Short(data[2..]) != 42) return 1;
        uint offset = Long(data[4..]);
        if (offset > data.Length - 2) return 1;
        var entries = data[(int)offset..];
        int count = Math.Min(Short(entries), (entries.Length - 2) / 12);
        for (int index = 0; index < count; index++)
        {
            var entry = entries[(2 + index * 12)..];
            if (Short(entry) != 274 || Short(entry[2..]) != 3 || Long(entry[4..]) != 1) continue;
            return Short(entry[8..]) is >= 1 and <= 8 and var orientation ? orientation : 1;
        }
        return 1;
    }

    private static PixelImage Orient(PixelImage image, int orientation)
    {
        if (orientation == 1) return image;
        int w = image.Width, h = image.Height;
        int width = orientation >= 5 ? h : w, height = orientation >= 5 ? w : h;
        var pixels = new byte[image.Pixels.Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var (dx, dy) = orientation switch
            {
                2 => (w - 1 - x, y), 3 => (w - 1 - x, h - 1 - y), 4 => (x, h - 1 - y),
                5 => (y, x), 6 => (h - 1 - y, x), 7 => (h - 1 - y, w - 1 - x), 8 => (y, w - 1 - x),
                _ => (x, y)
            };
            Buffer.BlockCopy(image.Pixels, (y * w + x) * 4, pixels, (dy * width + dx) * 4, 4);
        }
        return new PixelImage(width, height, pixels);
    }

    public static BitmapSource Bitmap(PixelImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static PixelImage Preview(PixelImage source)
    {
        const int PreviewLimit = 2400;
        int longest = Math.Max(source.Width, source.Height);
        if (longest <= PreviewLimit) return source;
        var scale = (double)PreviewLimit / longest;
        var scaled = new TransformedBitmap(Bitmap(source), new ScaleTransform(scale, scale));
        var normalized = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        int width = normalized.PixelWidth, height = normalized.PixelHeight;
        var pixels = new byte[width * height * 4];
        normalized.CopyPixels(pixels, width * 4, 0);
        return new PixelImage(width, height, pixels);
    }

    public static bool IsJpeg(string path) => Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

    public static string SuggestCopyName(string source, string folder, string suffix)
    {
        string stem = Path.GetFileNameWithoutExtension(source);
        foreach (string copySuffix in new[] { "-已调整", "-已調整", "-edited" })
        {
            int marker = stem.LastIndexOf(copySuffix, StringComparison.OrdinalIgnoreCase);
            if (marker <= 0) continue;
            ReadOnlySpan<char> tail = stem.AsSpan(marker + copySuffix.Length);
            if (tail.IsEmpty || tail[0] == '-' && int.TryParse(tail[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 2)
            {
                stem = stem[..marker];
                break;
            }
        }

        string basis = stem + suffix;
        string candidate = basis;
        for (int number = 2; CopyNameExists(folder, candidate); number++)
            candidate = basis + "-" + number.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }

    private static bool CopyNameExists(string folder, string stem)
    {
        foreach (string extension in new[] { "", ".jpg", ".jpeg", ".png" })
        {
            string full = Path.Combine(folder, stem + extension);
            if (File.Exists(full) || Directory.Exists(full)) return true;
        }
        return false;
    }

    public static bool HasTransparency(PixelImage image)
    {
        for (int i = 3; i < image.Pixels.Length; i += 4)
            if (image.Pixels[i] < 255) return true;
        return false;
    }

    public static byte[] Fingerprint(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    public static byte[] Save(PixelImage image, string path, bool overwrite = false, byte[]? expected = null)
    {
        path = Path.GetFullPath(path);
        bool jpeg = IsJpeg(path);
        if (!jpeg && !Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException();
        var bitmap = jpeg ? FlattenOnWhite(image) : Bitmap(image);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
        var contexts = new ReadOnlyCollection<ColorContext>([new ColorContext(bitmap.Format)]);
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, null, contexts));
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".photo-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
                stream.Flush(true);
            }
            byte[] result = Fingerprint(temporary);
            if (overwrite)
            {
                using var existing = OpenDestination(path);
                if (expected is not null && !SHA256.HashData(existing).AsSpan().SequenceEqual(expected))
                    throw new PhotoConflictException();
                File.Replace(temporary, path, null);
            }
            else File.Move(temporary, path);
            return result;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static FileStream OpenDestination(string path)
    {
        try { return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
        catch (FileNotFoundException) { throw new PhotoConflictException(); }
    }

    private static BitmapSource FlattenOnWhite(PixelImage image)
    {
        var source = image.Pixels;
        var flattened = new byte[checked(image.Width * image.Height * 3)];
        for (int i = 0, j = 0; i < source.Length; i += 4, j += 3)
        {
            int alpha = source[i + 3];
            for (int c = 0; c < 3; c++)
                flattened[j + c] = (byte)((source[i + c] * alpha + 255 * (255 - alpha) + 127) / 255);
        }
        return BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr24, null, flattened, image.Width * 3);
    }
}

internal sealed class PhotoConflictException : IOException;
internal sealed class ImageSizeException : IOException;
