using System.IO;
using PhotoColorEditor;

internal static class SyntheticPhotos
{
    public static string[] Create(string directory)
    {
        string warm = Path.Combine(directory, "Synthetic warm.png");
        string cool = Path.Combine(directory, "Synthetic cool.png");
        PhotoFiles.Save(CreateImage(800, 980, warm: true), warm);
        PhotoFiles.Save(CreateImage(900, 1100, warm: false), cool);
        return [warm, cool];
    }

    private static PixelImage CreateImage(int width, int height, bool warm)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double u = (double)x / width, v = (double)y / height;
            int light = 75 + 135 * y / height;
            int b = light + (warm ? -12 : 13), g = light, r = light + (warm ? 12 : -8);
            if (warm && u is > .40 and < .71 && v is > .06 and < .22 ||
                !warm && u is > .10 and < .35 && v is > .76 and < .91)
                (b, g, r) = (65, 140, 190);
            else if (u is > .30 and < .73 && v is > .38 and < .63 && (y / 18) % 2 == 0)
                (b, g, r) = (45, 62, 68);
            int index = (y * width + x) * 4;
            pixels[index] = (byte)b;
            pixels[index + 1] = (byte)g;
            pixels[index + 2] = (byte)r;
            pixels[index + 3] = 255;
        }
        return new PixelImage(width, height, pixels);
    }
}
