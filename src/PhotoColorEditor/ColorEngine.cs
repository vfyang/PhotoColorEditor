namespace PhotoColorEditor;

internal sealed record PixelImage(int Width, int Height, byte[] Pixels);
internal readonly record struct Rgb(double R, double G, double B);
internal readonly record struct WhiteBalance(Rgb Gains, int? X = null, int? Y = null);
internal readonly record struct CurvePoint(double X, double Y);
internal readonly record struct LevelRange(double Low, double High, double DarkOutput, double LightOutput)
{
    public double Apply(double value)
    {
        if (value < Low) return value * DarkOutput / Low;
        if (value > High) return LightOutput + (value - High) * (1 - LightOutput) / (1 - High);
        return DarkOutput + (value - Low) * (LightOutput - DarkOutput) / (High - Low);
    }
}

internal sealed class ToneCurve
{
    public static ToneCurve Identity { get; } = new([new(0, 0), new(1, 1)]);
    public IReadOnlyList<CurvePoint> Points { get; }

    public ToneCurve(IEnumerable<CurvePoint> points) => Points = Array.AsReadOnly(points.ToArray());

    public double Apply(double value)
    {
        for (int index = 1; index < Points.Count; index++)
        {
            var right = Points[index];
            if (value > right.X) continue;
            var left = Points[index - 1];
            return left.Y + (right.Y - left.Y) * (value - left.X) / (right.X - left.X);
        }
        return Points[^1].Y;
    }
}

internal sealed record EditSettings(
    double Exposure = 0, double Saturation = 1, WhiteBalance? Balance = null,
    double Warmth = 0, double Tint = 0, ToneCurve? Master = null, ToneCurve? Red = null,
    ToneCurve? Green = null, ToneCurve? Blue = null, LevelRange? Levels = null, double Shadows = 0, double Highlights = 0,
    int SoftLightLevel = 0, int SharpenLevel = 0, PhotoGeometry Geometry = default)
{
    public const int MaxDetailLevel = 5;
    public static EditSettings Default { get; } = new();

    public ToneCurve Curve(int channel) => channel switch
    {
        1 => Red ?? ToneCurve.Identity,
        2 => Green ?? ToneCurve.Identity,
        3 => Blue ?? ToneCurve.Identity,
        _ => Master ?? ToneCurve.Identity
    };

    public EditSettings WithCurve(int channel, ToneCurve? curve) => channel switch
    {
        1 => this with { Red = curve },
        2 => this with { Green = curve },
        3 => this with { Blue = curve },
        _ => this with { Master = curve }
    };
}

internal static partial class ColorEngine
{
    private static double ToLinear(double srgb) => srgb <= .04045 ? srgb / 12.92 : Math.Pow((srgb + .055) / 1.055, 2.4);
    private static readonly double[] Linear = Enumerable.Range(0, 256).Select(value => ToLinear(value / 255.0)).ToArray();

    private const int LookupSize = 8192;
    private static readonly double[] Encoded = Enumerable.Range(0, LookupSize + 1).Select(value =>
    {
        double linear = value / (double)LookupSize;
        return linear <= .0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - .055;
    }).ToArray();

    private static double Luminance(double r, double g, double b) => .2126 * r + .7152 * g + .0722 * b;

    public static Rgb? AveragePatch(PixelImage image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) return null;
        double r = 0, g = 0, b = 0;
        int count = 0, area = 0;
        for (int row = Math.Max(0, y - 2); row <= Math.Min(image.Height - 1, y + 2); row++)
        for (int col = Math.Max(0, x - 2); col <= Math.Min(image.Width - 1, x + 2); col++)
        {
            area++;
            int index = (row * image.Width + col) * 4;
            if (!IsUsable(image.Pixels, index, 20, 249)) continue;
            b += Linear[image.Pixels[index]];
            g += Linear[image.Pixels[index + 1]];
            r += Linear[image.Pixels[index + 2]];
            count++;
        }
        return count > 0 && count * 2 >= area ? new Rgb(r / count, g / count, b / count) : null;
    }

    private static bool IsUsable(byte[] pixels, int i, int low, int high) => pixels[i + 3] >= 128 &&
        pixels[i] >= low && pixels[i] <= high && pixels[i + 1] >= low && pixels[i + 1] <= high &&
        pixels[i + 2] >= low && pixels[i + 2] <= high;

    private static Rgb GainsFor(Rgb sample)
    {
        double light = Luminance(sample.R, sample.G, sample.B);
        return new Rgb(light / sample.R, light / sample.G, light / sample.B);
    }

    public static WhiteBalance? PickBalance(PixelImage image, int x, int y)
    {
        if (AveragePatch(image, x, y) is not { } sample) return null;
        var gains = GainsFor(sample);
        return gains.R is >= .55 and <= 1.9 && gains.G is >= .55 and <= 1.9 && gains.B is >= .55 and <= 1.9
            ? new WhiteBalance(gains, x, y) : null;
    }

    public static WhiteBalance? AutoBalance(PixelImage image)
    {
        double r = 0, g = 0, b = 0, totalWeight = 0;
        int examined = 0, accepted = 0;
        int stride = Math.Max(1, (int)Math.Sqrt((double)image.Width * image.Height / 65_536));
        byte[] pixels = image.Pixels;
        for (int y = stride / 2; y < image.Height; y += stride)
        for (int x = stride / 2; x < image.Width; x += stride)
        {
            int i = (y * image.Width + x) * 4;
            examined++;
            if (!IsUsable(pixels, i, 90, 249)) continue;
            int max = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
            int min = Math.Min(pixels[i], Math.Min(pixels[i + 1], pixels[i + 2]));
            if (max - min > .29 * max) continue;
            double red = Linear[pixels[i + 2]], green = Linear[pixels[i + 1]], blue = Linear[pixels[i]];
            double light = Luminance(red, green, blue);
            double weight = light * light * light;
            r += red * weight;
            g += green * weight;
            b += blue * weight;
            totalWeight += weight;
            accepted++;
        }
        if (accepted < Math.Max(8, examined / 25) || totalWeight == 0) return null;
        var gains = GainsFor(new Rgb(r / totalWeight, g / totalWeight, b / totalWeight));
        return new WhiteBalance(new Rgb(LimitAuto(gains.R), LimitAuto(gains.G), LimitAuto(gains.B)));
    }

    private static double LimitAuto(double gain) => Math.Clamp(1 + (gain - 1) * .76, .78, 1.28);

    public static EditSettings AutoOptimize(PixelImage image, EditSettings settings)
    {
        var balance = AutoBalance(image);
        double light = 0;
        int count = 0;
        int stride = Math.Max(1, image.Pixels.Length / (4 * 32_768));
        for (int index = 0; index < image.Pixels.Length; index += 4 * stride)
        {
            if (image.Pixels[index + 3] < 128) continue;
            light += Luminance(Linear[image.Pixels[index + 2]], Linear[image.Pixels[index + 1]], Linear[image.Pixels[index]]);
            count++;
        }
        if (count == 0) return settings;
        double exposure = Math.Clamp((.48 - light / count) * 1.15, -.12, .35);
        return balance is null ? settings with { Exposure = exposure }
            : settings with { Balance = balance, Exposure = exposure };
    }

    public static EditSettings AutoLevels(PixelImage image, EditSettings settings)
    {
        int pixels = image.Pixels.Length / 4;
        int stride = (pixels + 65_535) / 65_536;
        int count = (pixels - 1) / stride + 1;
        var sample = new byte[count * 4];
        for (int i = 0; i < count; i++) image.Pixels.AsSpan(i * stride * 4, 4).CopyTo(sample.AsSpan(i * 4));
        var input = settings with { Levels = null, Shadows = 0, Highlights = 0, Master = null, Red = null, Green = null, Blue = null,
            SoftLightLevel = 0, SharpenLevel = 0 };
        int[] histogram = LightHistogram(Render(new PixelImage(count, 1, sample), input));
        int valid = histogram.Sum();
        if (valid < 8) return settings;
        double low = Percentile(histogram, (int)((valid - 1) * .01)) / 255d;
        double high = Percentile(histogram, (int)((valid - 1) * .99)) / 255d;
        if (high - low < .08) return settings;
        double dark = low - Math.Min(.20, Math.Max(0, low - .02) * .8);
        double light = high + Math.Min(.20, Math.Max(0, .98 - high) * .8);
        return settings with { Levels = low == dark && high == light ? null : new LevelRange(low, high, dark, light) };
    }

    private static int Percentile(int[] histogram, int rank)
    {
        for (int i = 0; i < histogram.Length; i++)
        {
            rank -= histogram[i];
            if (rank < 0) return i;
        }
        return histogram.Length - 1;
    }

    public static int[] LightHistogram(PixelImage image)
    {
        int[] bins = new int[256];
        var pixels = image.Pixels;
        int stride = Math.Max(1, (pixels.Length / 4 + 131_071) / 131_072);
        for (int i = 0; i < pixels.Length; i += 4 * stride)
        {
            if (pixels[i + 3] < 128) continue;
            double light = Luminance(Linear[pixels[i + 2]], Linear[pixels[i + 1]], Linear[pixels[i]]);
            bins[(int)(Encoded[LookupIndex(light)] * 255 + .5)]++;
        }
        return bins;
    }

    public static PixelImage Render(PixelImage image, EditSettings settings, CancellationToken token = default)
    {
        if (settings == EditSettings.Default) return image;
        if (settings.SharpenLevel > 0)
        {
            var rendered = Render(image, settings with { SharpenLevel = 0 }, token);
            if (ReferenceEquals(rendered.Pixels, image.Pixels)) rendered = rendered with { Pixels = (byte[])image.Pixels.Clone() };
            SharpenPixels(rendered, Math.Min(settings.SharpenLevel, EditSettings.MaxDetailLevel), token);
            return rendered;
        }
        var gains = settings.Balance?.Gains ?? new Rgb(1, 1, 1);
        double redGain = gains.R * Math.Pow(2, settings.Warmth * .32 + settings.Tint * .10);
        double greenGain = gains.G * Math.Pow(2, -settings.Tint * .24);
        double blueGain = gains.B * Math.Pow(2, -settings.Warmth * .32 + settings.Tint * .10);
        double exposure = Math.Pow(2, settings.Exposure);
        var redCurve = MakeOutputLookup(settings.Master, settings.Red);
        var greenCurve = MakeOutputLookup(settings.Master, settings.Green);
        var blueCurve = MakeOutputLookup(settings.Master, settings.Blue);
        var lightLookup = MakeLuminanceLookup(settings);
        byte[] source = image.Pixels;
        byte[] result = new byte[source.Length];
        for (int row = 0; row < image.Height; row++)
        {
            token.ThrowIfCancellationRequested();
            int end = (row + 1) * image.Width * 4;
            for (int i = row * image.Width * 4; i < end; i += 4)
            {
                double r = Linear[source[i + 2]] * redGain;
                double g = Linear[source[i + 1]] * greenGain;
                double b = Linear[source[i]] * blueGain;
                double light = Luminance(r, g, b);
                double tone = exposure > 1 ? exposure / (1 + (exposure - 1) * light) : exposure;
                r = (light + (r - light) * settings.Saturation) * tone;
                g = (light + (g - light) * settings.Saturation) * tone;
                b = (light + (b - light) * settings.Saturation) * tone;
                if (lightLookup is not null) RemapLuminance(ref r, ref g, ref b, lightLookup);
                result[i] = blueCurve[LookupIndex(b)];
                result[i + 1] = greenCurve[LookupIndex(g)];
                result[i + 2] = redCurve[LookupIndex(r)];
                result[i + 3] = source[i + 3];
            }
        }
        return new PixelImage(image.Width, image.Height, result);
    }

    private static int LookupIndex(double value) => (int)(Math.Clamp(value, 0, 1) * LookupSize + .5);

    private static double[]? MakeLuminanceLookup(EditSettings settings)
    {
        int softLightLevel = Math.Min(settings.SoftLightLevel, EditSettings.MaxDetailLevel);
        if (settings.Levels is null && settings.Shadows == 0 && settings.Highlights == 0 && softLightLevel <= 0) return null;
        return Encoded.Select(value =>
        {
            double x = settings.Levels?.Apply(value) ?? value;
            double dark = 1 - x, darkSquared = dark * dark, lightSquared = x * x;
            double adjusted = x + .92 * (settings.Shadows * x * darkSquared * darkSquared +
                settings.Highlights * dark * lightSquared * lightSquared);
            adjusted = Math.Clamp(adjusted, 0, 1);
            for (int level = 0; level < softLightLevel; level++) adjusted = BlendSoftLight(adjusted);
            return ToLinear(adjusted);
        }).ToArray();
    }

    private static void RemapLuminance(ref double r, ref double g, ref double b, double[] lookup)
    {
        r = Math.Clamp(r, 0, 1);
        g = Math.Clamp(g, 0, 1);
        b = Math.Clamp(b, 0, 1);
        double current = Luminance(r, g, b);
        if (current <= 0 || current >= 1) return;
        SetLuminance(ref r, ref g, ref b, current, lookup[LookupIndex(current)]);
    }

    private static void SetLuminance(ref double r, ref double g, ref double b, double current, double target)
    {
        if (current <= 0 || current >= 1)
        {
            r = g = b = target;
            return;
        }
        double factor = target / current;
        if (factor * Math.Max(r, Math.Max(g, b)) <= 1)
        {
            r *= factor;
            g *= factor;
            b *= factor;
            return;
        }
        // Blend toward white only after reaching the brightest in-gamut color on this hue ray.
        factor = 1 / Math.Max(r, Math.Max(g, b));
        double peak = current * factor;
        double mix = (target - peak) / (1 - peak);
        r = r * factor * (1 - mix) + mix;
        g = g * factor * (1 - mix) + mix;
        b = b * factor * (1 - mix) + mix;
    }

    private static byte[] MakeOutputLookup(ToneCurve? master, ToneCurve? channel) => Encoded.Select(value =>
    {
        double overall = master?.Apply(value) ?? value;
        double adjusted = channel?.Apply(overall) ?? overall;
        return (byte)Math.Round(Math.Clamp(adjusted, 0, 1) * 255);
    }).ToArray();
}
