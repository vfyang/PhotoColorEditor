namespace PhotoColorEditor;

internal readonly record struct PixelBox(int X, int Y, int Width, int Height);

internal readonly record struct PhotoGeometry(int Turns = 0, PixelBox? Crop = null, double Angle = 0, PixelBox? AngleArea = null)
{
    public const int AngleLimit = 15;
    public PixelBox SourceBox(int width, int height) => Crop ?? new PixelBox(0, 0, width, height);

    public (int Width, int Height) Size(int width, int height)
    {
        var box = SourceBox(width, height);
        return Turns % 2 == 0 ? (box.Width, box.Height) : (box.Height, box.Width);
    }

    public PhotoGeometry Rotate(int direction) => this with { Turns = (Turns + direction + 4) % 4 };

    public PhotoGeometry WithAngle(double angle) => this with { Angle = angle, AngleArea = angle == 0 ? null : Angle == 0 ? Crop : AngleArea };

    private (double X, double Y) ToCanvas(double x, double y) => Turns switch
    {
        1 => (y, 1 - x), 2 => (1 - x, 1 - y), 3 => (1 - y, x), _ => (x, y)
    };

    private (double X, double Y) FromCanvas(double x, double y) => Turns switch
    {
        1 => (1 - y, x), 2 => (1 - x, 1 - y), 3 => (y, 1 - x), _ => (x, y)
    };

    public (double X, double Y) ViewPoint(int x, int y, int width, int height)
    {
        var box = SourceBox(width, height);
        var point = new StraightenTransform(Angle, width, height, AngleArea).Forward((x + .5) / width, (y + .5) / height);
        return FromCanvas((point.X * width - box.X) / box.Width, (point.Y * height - box.Y) / box.Height);
    }

    public (int X, int Y) SourcePoint(double x, double y, int width, int height)
    {
        var box = SourceBox(width, height);
        var canvas = ToCanvas(x, y);
        var point = new StraightenTransform(Angle, width, height, AngleArea).Inverse((box.X + canvas.X * box.Width) / width,
            (box.Y + canvas.Y * box.Height) / height);
        return (Math.Clamp((int)(point.X * width), 0, width - 1), Math.Clamp((int)(point.Y * height), 0, height - 1));
    }

    public PhotoGeometry CropTo(double left, double top, double right, double bottom, int width, int height)
    {
        var box = SourceBox(width, height);
        var a = ToCanvas(left, top);
        var b = ToCanvas(right, bottom);
        int x = Math.Clamp((int)Math.Round(Math.Min(a.X, b.X) * box.Width), 0, box.Width - 1);
        int y = Math.Clamp((int)Math.Round(Math.Min(a.Y, b.Y) * box.Height), 0, box.Height - 1);
        int endX = Math.Clamp((int)Math.Round(Math.Max(a.X, b.X) * box.Width), x + 1, box.Width);
        int endY = Math.Clamp((int)Math.Round(Math.Max(a.Y, b.Y) * box.Height), y + 1, box.Height);
        var selected = new PixelBox(box.X + x, box.Y + y, endX - x, endY - y);
        return this with { Crop = selected == new PixelBox(0, 0, width, height) ? null : selected };
    }

    public PixelImage Apply(PixelImage image, int originalWidth, int originalHeight, CancellationToken token)
    {
        if (this == default) return image;
        var crop = SourceBox(originalWidth, originalHeight);
        int x0 = Scale(crop.X, image.Width, originalWidth), y0 = Scale(crop.Y, image.Height, originalHeight);
        int width = Math.Max(1, Scale(crop.X + crop.Width, image.Width, originalWidth) - x0);
        int height = Math.Max(1, Scale(crop.Y + crop.Height, image.Height, originalHeight) - y0);
        x0 = Math.Min(x0, image.Width - 1);
        y0 = Math.Min(y0, image.Height - 1);
        var (outputWidth, outputHeight) = Turns % 2 == 0 ? (width, height) : (height, width);
        var pixels = new byte[outputWidth * outputHeight * 4];
        var transform = new StraightenTransform(Angle, originalWidth, originalHeight, AngleArea);
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            int input = ((y + y0) * image.Width + x0) * 4;
            if (Turns == 0 && Angle == 0)
            {
                Buffer.BlockCopy(image.Pixels, input, pixels, y * width * 4, width * 4);
                continue;
            }
            for (int x = 0; x < width; x++, input += 4)
            {
                var (dx, dy) = Turns switch
                {
                    1 => (height - 1 - y, x), 2 => (width - 1 - x, height - 1 - y), 3 => (y, width - 1 - x), _ => (x, y)
                };
                int output = (dy * outputWidth + dx) * 4;
                if (Angle == 0) Buffer.BlockCopy(image.Pixels, input, pixels, output, 4);
                else
                {
                    var sample = transform.Inverse((x0 + x + .5) / image.Width, (y0 + y + .5) / image.Height);
                    Sample(image, sample.X * image.Width - .5, sample.Y * image.Height - .5, pixels.AsSpan(output, 4));
                }
            }
        }
        return new PixelImage(outputWidth, outputHeight, pixels);
    }

    private static int Scale(int pixel, int size, int original) => (int)Math.Round(pixel * (double)size / original);

    private readonly struct StraightenTransform
    {
        private readonly double cosine, sineX, sineY, fill, centerX, centerY;

        public StraightenTransform(double angle, int width, int height, PixelBox? area)
        {
            double radians = angle * Math.PI / 180;
            cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            sineX = sine * height / width;
            sineY = sine * width / height;
            var box = area ?? new PixelBox(0, 0, width, height);
            centerX = (box.X + box.Width / 2d) / width;
            centerY = (box.Y + box.Height / 2d) / height;
            // Enlarge the rotated image just enough to cover all four canvas corners.
            fill = cosine + Math.Abs(sine) * Math.Max(box.Width / (double)box.Height, box.Height / (double)box.Width);
        }

        public (double X, double Y) Inverse(double x, double y) =>
            (centerX + (cosine * (x - centerX) + sineX * (y - centerY)) / fill,
             centerY + (cosine * (y - centerY) - sineY * (x - centerX)) / fill);

        public (double X, double Y) Forward(double x, double y) =>
            (centerX + fill * (cosine * (x - centerX) - sineX * (y - centerY)),
             centerY + fill * (cosine * (y - centerY) + sineY * (x - centerX)));
    }

    private static void Sample(PixelImage image, double x, double y, Span<byte> destination)
    {
        x = Math.Clamp(x, 0, image.Width - 1);
        y = Math.Clamp(y, 0, image.Height - 1);
        int left = (int)x, top = (int)y;
        int right = Math.Min(left + 1, image.Width - 1), bottom = Math.Min(top + 1, image.Height - 1);
        double fx = x - left, fy = y - top;
        int tl = (top * image.Width + left) * 4, tr = (top * image.Width + right) * 4;
        int bl = (bottom * image.Width + left) * 4, br = (bottom * image.Width + right) * 4;
        byte[] pixels = image.Pixels;
        double a = (1 - fx) * (1 - fy) * pixels[tl + 3], b = fx * (1 - fy) * pixels[tr + 3];
        double c = (1 - fx) * fy * pixels[bl + 3], d = fx * fy * pixels[br + 3];
        double alpha = a + b + c + d;
        destination[3] = (byte)(alpha + .5);
        if (destination[3] == 0) return;
        for (int channel = 0; channel < 3; channel++)
            destination[channel] = (byte)((a * pixels[tl + channel] + b * pixels[tr + channel] +
                c * pixels[bl + channel] + d * pixels[br + channel]) / alpha + .5);
    }
}
