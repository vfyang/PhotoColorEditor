namespace PhotoColorEditor;

internal static partial class ColorEngine
{
    private static double BlendSoftLight(double value)
    {
        double contrast = value <= .5
            ? -(1 - 2 * value) * value * (1 - value)
            : (2 * value - 1) * (Math.Sqrt(value) - value);
        return value + .45 * contrast;
    }

    private static void SharpenPixels(PixelImage image, int level, CancellationToken token)
    {
        int width = image.Width;
        int height = image.Height;
        double gain = .60 + (level - 1) * .25;
        double maxDelta = .075 + (level - 1) * .015;
        byte[] pixels = image.Pixels;
        // Keep three rows of blurred, alpha-weighted luminance so the output can be written in place.
        float[][] luminance = [new float[width], new float[width], new float[width]];
        float[][] coverage = [new float[width], new float[width], new float[width]];
        var rowLight = new float[width];
        var rowAlpha = new float[width];
        PrepareRow(0);
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            if (y + 1 < height) PrepareRow(y + 1);
            int above = Math.Max(0, y - 1) % 3, row = y % 3, below = Math.Min(height - 1, y + 1) % 3;
            for (int x = 0, index = y * width * 4; x < width; x++, index += 4)
            {
                if (pixels[index + 3] == 0) continue;
                double r = Linear[pixels[index + 2]], g = Linear[pixels[index + 1]], b = Linear[pixels[index]];
                double current = Luminance(r, g, b);
                double light = Encoded[LookupIndex(current)];
                double average = (luminance[above][x] + 2 * luminance[row][x] + luminance[below][x]) /
                    (coverage[above][x] + 2 * coverage[row][x] + coverage[below][x]);
                double detail = light - average;
                const double Threshold = .012;
                if (Math.Abs(detail) <= Threshold) continue;
                double delta = Math.Min(maxDelta, (Math.Abs(detail) - Threshold) * gain) * Math.Sign(detail);
                double target = ToLinear(Math.Clamp(light + delta, 0, 1));
                SetLuminance(ref r, ref g, ref b, current, target);
                pixels[index] = Encode(b);
                pixels[index + 1] = Encode(g);
                pixels[index + 2] = Encode(r);
            }
        }

        void PrepareRow(int y)
        {
            for (int x = 0, index = y * width * 4; x < width; x++, index += 4)
            {
                rowAlpha[x] = pixels[index + 3] / 255f;
                double light = Luminance(Linear[pixels[index + 2]], Linear[pixels[index + 1]], Linear[pixels[index]]);
                rowLight[x] = (float)Encoded[LookupIndex(light)] * rowAlpha[x];
            }
            var luma = luminance[y % 3];
            var alpha = coverage[y % 3];
            for (int x = 0; x < width; x++)
            {
                int left = Math.Max(0, x - 1), right = Math.Min(width - 1, x + 1);
                luma[x] = rowLight[left] + 2 * rowLight[x] + rowLight[right];
                alpha[x] = rowAlpha[left] + 2 * rowAlpha[x] + rowAlpha[right];
            }
        }
    }

    private static byte Encode(double linear) => (byte)(Encoded[LookupIndex(linear)] * 255 + .5);
}
