namespace PhotoColorEditor;

internal static class SamplePhoto
{
    public static PixelImage Create(bool warm)
    {
        const int Width = 960, Height = 660;
        var pixels = new byte[Width * Height * 4];
        for (int x = 0; x < Width; x++)
        {
            double u = x / (double)Width;
            double ridge = .43 - .14 * Math.Sin(u * 9) - .09 * Math.Sin(u * 22 + .6);
            double foothill = .55 - .075 * Math.Sin(u * 12 + 2);
            for (int y = 0; y < Height; y++)
            {
                double v = y / (double)Height;
                (int r, int g, int b) = v > .60 ? (68 + y % 11, 115 + y % 11, 134 + y % 11)
                    : v > foothill ? (49, 88, 74)
                    : v > ridge ? (109, 133, 145)
                    : (154 + y / 4, 190 + y / 9, 219);
                if ((u - .77) * (u - .77) + (v - .20) * (v - .20) < .003) (r, g, b) = (244, 215, 139);
                if (u is > .12 and < .23 && v is > .49 and < .59) (r, g, b) = (206, 206, 206);
                if (v > .43 + Math.Abs(u - .175) && v < .50 && u is > .10 and < .25) (r, g, b) = (139, 67, 52);
                int index = (y * Width + x) * 4;
                pixels[index] = (byte)Math.Clamp(b + (warm ? -19 : 14), 0, 255);
                pixels[index + 1] = (byte)g;
                pixels[index + 2] = (byte)Math.Clamp(r + (warm ? 12 : -15), 0, 255);
                pixels[index + 3] = 255;
            }
        }
        return new PixelImage(Width, Height, pixels);
    }
}
