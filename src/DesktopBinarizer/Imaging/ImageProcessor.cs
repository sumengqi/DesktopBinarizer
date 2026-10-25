using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopBinarizer.Imaging;

public static class ImageProcessor
{
    private static readonly byte[][] ChannelQuantizationTables =
        CreateChannelQuantizationTables();

    private static readonly DiffusionKernel FloydSteinberg = new(
        16,
        [
            new(1, 0, 7),
            new(-1, 1, 3),
            new(0, 1, 5),
            new(1, 1, 1),
        ]);

    private static readonly DiffusionKernel Atkinson = new(
        8,
        [
            new(1, 0, 1),
            new(2, 0, 1),
            new(-1, 1, 1),
            new(0, 1, 1),
            new(1, 1, 1),
            new(0, 2, 1),
        ]);

    private static readonly DiffusionKernel JarvisJudiceNinke = new(
        48,
        [
            new(1, 0, 7),
            new(2, 0, 5),
            new(-2, 1, 3),
            new(-1, 1, 5),
            new(0, 1, 7),
            new(1, 1, 5),
            new(2, 1, 3),
            new(-2, 2, 1),
            new(-1, 2, 3),
            new(0, 2, 5),
            new(1, 2, 3),
            new(2, 2, 1),
        ]);

    private static readonly DiffusionKernel Stucki = new(
        42,
        [
            new(1, 0, 8),
            new(2, 0, 4),
            new(-2, 1, 2),
            new(-1, 1, 4),
            new(0, 1, 8),
            new(1, 1, 4),
            new(2, 1, 2),
            new(-2, 2, 1),
            new(-1, 2, 2),
            new(0, 2, 4),
            new(1, 2, 2),
            new(2, 2, 1),
        ]);

    private static readonly DiffusionKernel Burkes = new(
        32,
        [
            new(1, 0, 8),
            new(2, 0, 4),
            new(-2, 1, 2),
            new(-1, 1, 4),
            new(0, 1, 8),
            new(1, 1, 4),
            new(2, 1, 2),
        ]);

    private static readonly DiffusionKernel SierraLite = new(
        4,
        [
            new(1, 0, 2),
            new(-1, 1, 1),
            new(0, 1, 1),
        ]);

    public static Bitmap Process(Bitmap source, ImageProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        Bitmap scaled = ScaleToOutputSize(
            source,
            options.OutputWidth,
            options.OutputHeight,
            options.InputScaleMode);
        ApplyQuantization(scaled, options);

        if (!options.EnablePixelSpacing)
        {
            return scaled;
        }

        try
        {
            return ApplyPixelSpacing(
                scaled,
                options.PixelSpacingWidth,
                options.PixelSpacingColor ?? Color.Black);
        }
        finally
        {
            scaled.Dispose();
        }
    }

    private static void ValidateOptions(ImageProcessingOptions options)
    {
        if (options.OutputWidth is < 1 or > 8192 ||
            options.OutputHeight is < 1 or > 8192)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "输出宽高必须在 1 到 8192 之间。");
        }

        if (options.Threshold is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "阈值必须在 0 到 255 之间。");
        }

        ValidateBitDepth(options.GrayscaleBits, nameof(options.GrayscaleBits));
        ValidateBitDepth(options.RgbBits, nameof(options.RgbBits));
        ValidateBitDepth(options.RedBits, nameof(options.RedBits));
        ValidateBitDepth(options.GreenBits, nameof(options.GreenBits));
        ValidateBitDepth(options.BlueBits, nameof(options.BlueBits));

        if (!Enum.IsDefined(options.ColorMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "不支持所选的颜色量化模式。");
        }

        if (options.ColorMode == ColorQuantizationMode.CustomPalette &&
            (options.Palette is null || options.Palette.Count < 2))
        {
            throw new ArgumentException(
                "自定义调色板至少需要两种颜色。",
                nameof(options));
        }

        if (options.Palette is { Count: > 256 })
        {
            throw new ArgumentException(
                "自定义调色板最多支持 256 种颜色。",
                nameof(options));
        }

        if (!Enum.IsDefined(options.DitherAlgorithm))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "不支持所选的扩散算法。");
        }

        if (!Enum.IsDefined(options.InputScaleMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "不支持所选的输入缩放方式。");
        }

        if (options.EnablePixelSpacing)
        {
            if (options.PixelSpacingWidth is < 1 or > 32)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "像素间距宽度必须在 1 到 32 之间。");
            }

            int finalWidth = GetSpacedDimension(
                options.OutputWidth,
                options.PixelSpacingWidth);
            int finalHeight = GetSpacedDimension(
                options.OutputHeight,
                options.PixelSpacingWidth);
            if (finalWidth > 8192 || finalHeight > 8192)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"插入间距后的尺寸 {finalWidth} × {finalHeight} 超过 8192 × 8192。");
            }
        }
    }

    private static void ValidateBitDepth(int bits, string parameterName)
    {
        if (bits is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "颜色位深必须在 1 到 8 bit 之间。");
        }
    }

    private static Bitmap ScaleToOutputSize(
        Bitmap source,
        int width,
        int height,
        InputScaleMode scaleMode)
    {
        var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        output.SetResolution(96, 96);

        using Graphics graphics = Graphics.FromImage(output);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        graphics.Clear(Color.Black);

        Rectangle destination = scaleMode == InputScaleMode.Stretch
            ? new Rectangle(0, 0, width, height)
            : GetProportionalRectangle(source.Size, new Size(width, height));

        graphics.DrawImage(
            source,
            destination,
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel);

        return output;
    }

    private static Rectangle GetProportionalRectangle(Size source, Size output)
    {
        double scale = Math.Min(
            (double)output.Width / source.Width,
            (double)output.Height / source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            (output.Width - width) / 2,
            (output.Height - height) / 2,
            width,
            height);
    }

    public static Bitmap ApplyPixelSpacing(
        Bitmap source,
        int spacingWidth,
        Color spacingColor)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (spacingWidth is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spacingWidth),
                "像素间距宽度必须在 1 到 32 之间。");
        }

        int outputWidth = GetSpacedDimension(source.Width, spacingWidth);
        int outputHeight = GetSpacedDimension(source.Height, spacingWidth);
        if (outputWidth > 8192 || outputHeight > 8192)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                $"插入间距后的尺寸 {outputWidth} × {outputHeight} 超过 8192 × 8192。");
        }

        var output = new Bitmap(
            outputWidth,
            outputHeight,
            PixelFormat.Format32bppArgb);
        output.SetResolution(96, 96);

        using (Graphics graphics = Graphics.FromImage(output))
        {
            graphics.Clear(spacingColor);
        }

        Rectangle sourceBounds = new(0, 0, source.Width, source.Height);
        Rectangle outputBounds = new(0, 0, output.Width, output.Height);
        try
        {
            BitmapData sourceData = source.LockBits(
                sourceBounds,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData? outputData = null;

            try
            {
                outputData = output.LockBits(
                    outputBounds,
                    ImageLockMode.ReadWrite,
                    PixelFormat.Format32bppArgb);
                int sourceByteCount = Math.Abs(sourceData.Stride) * source.Height;
                int outputByteCount = Math.Abs(outputData.Stride) * output.Height;
                byte[] sourcePixels = new byte[sourceByteCount];
                byte[] outputPixels = new byte[outputByteCount];
                Marshal.Copy(sourceData.Scan0, sourcePixels, 0, sourceByteCount);
                Marshal.Copy(outputData.Scan0, outputPixels, 0, outputByteCount);

                int pixelStep = spacingWidth + 1;
                for (int y = 0; y < source.Height; y++)
                {
                    int sourceRow = y * sourceData.Stride;
                    int outputRow = (y * pixelStep) * outputData.Stride;
                    for (int x = 0; x < source.Width; x++)
                    {
                        int sourceOffset = sourceRow + (x * 4);
                        int outputOffset = outputRow + (x * pixelStep * 4);
                        outputPixels[outputOffset] = sourcePixels[sourceOffset];
                        outputPixels[outputOffset + 1] = sourcePixels[sourceOffset + 1];
                        outputPixels[outputOffset + 2] = sourcePixels[sourceOffset + 2];
                        outputPixels[outputOffset + 3] = 255;
                    }
                }

                Marshal.Copy(outputPixels, 0, outputData.Scan0, outputByteCount);
            }
            finally
            {
                source.UnlockBits(sourceData);
                if (outputData is not null)
                {
                    output.UnlockBits(outputData);
                }
            }

            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static int GetSpacedDimension(int pixelCount, int spacingWidth)
    {
        long dimension = pixelCount + ((long)pixelCount - 1) * spacingWidth;
        if (dimension > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelCount));
        }

        return (int)dimension;
    }

    private static void ApplyQuantization(Bitmap bitmap, ImageProcessingOptions options)
    {
        Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(
            bounds,
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);

        try
        {
            int byteCount = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(data.Scan0, pixels, 0, byteCount);

            DiffusionKernel? kernel = GetKernel(options.DitherAlgorithm);
            if (kernel is null)
            {
                ApplyDirectQuantization(
                    pixels,
                    data.Stride,
                    bitmap.Width,
                    bitmap.Height,
                    options);
            }
            else
            {
                ApplyErrorDiffusion(
                    pixels,
                    data.Stride,
                    bitmap.Width,
                    bitmap.Height,
                    options,
                    kernel);
            }

            Marshal.Copy(pixels, 0, data.Scan0, byteCount);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static DiffusionKernel? GetKernel(DitherAlgorithm algorithm)
    {
        return algorithm switch
        {
            DitherAlgorithm.None => null,
            DitherAlgorithm.FloydSteinberg => FloydSteinberg,
            DitherAlgorithm.Atkinson => Atkinson,
            DitherAlgorithm.JarvisJudiceNinke => JarvisJudiceNinke,
            DitherAlgorithm.Stucki => Stucki,
            DitherAlgorithm.Burkes => Burkes,
            DitherAlgorithm.SierraLite => SierraLite,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
    }

    private static void ApplyDirectQuantization(
        byte[] pixels,
        int stride,
        int width,
        int height,
        ImageProcessingOptions options)
    {
        switch (options.ColorMode)
        {
            case ColorQuantizationMode.Grayscale:
                ApplyDirectGrayscale(pixels, stride, width, height, options);
                return;
            case ColorQuantizationMode.RgbUniform:
                byte[] uniformTable = ChannelQuantizationTables[options.RgbBits];
                ApplyDirectRgb(
                    pixels,
                    stride,
                    width,
                    height,
                    uniformTable,
                    uniformTable,
                    uniformTable,
                    options.Invert);
                return;
            case ColorQuantizationMode.RgbPerChannel:
                ApplyDirectRgb(
                    pixels,
                    stride,
                    width,
                    height,
                    ChannelQuantizationTables[options.RedBits],
                    ChannelQuantizationTables[options.GreenBits],
                    ChannelQuantizationTables[options.BlueBits],
                    options.Invert);
                return;
            case ColorQuantizationMode.CustomPalette:
                ApplyDirectPalette(pixels, stride, width, height, options);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.ColorMode));
        }
    }

    private static void ApplyDirectGrayscale(
        byte[] pixels,
        int stride,
        int width,
        int height,
        ImageProcessingOptions options)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);
                double red = pixels[offset + 2];
                double green = pixels[offset + 1];
                double blue = pixels[offset];
                if (options.Invert)
                {
                    red = 255 - red;
                    green = 255 - green;
                    blue = 255 - blue;
                }

                double luminance =
                    (0.2126 * red) +
                    (0.7152 * green) +
                    (0.0722 * blue);
                byte value = options.GrayscaleBits == 1
                    ? luminance >= options.Threshold ? (byte)255 : (byte)0
                    : QuantizeChannelToByte(luminance, options.GrayscaleBits);
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static void ApplyDirectRgb(
        byte[] pixels,
        int stride,
        int width,
        int height,
        byte[] redTable,
        byte[] greenTable,
        byte[] blueTable,
        bool invert)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);
                int blue = pixels[offset];
                int green = pixels[offset + 1];
                int red = pixels[offset + 2];
                if (invert)
                {
                    blue = 255 - blue;
                    green = 255 - green;
                    red = 255 - red;
                }

                pixels[offset] = blueTable[blue];
                pixels[offset + 1] = greenTable[green];
                pixels[offset + 2] = redTable[red];
                pixels[offset + 3] = 255;
            }
        }
    }

    private static void ApplyDirectPalette(
        byte[] pixels,
        int stride,
        int width,
        int height,
        ImageProcessingOptions options)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);
                RgbValue source = ReadPixel(pixels, offset, options.Invert);
                RgbValue quantized = FindNearestPaletteColor(
                    source,
                    options.Palette!);
                SetPixel(pixels, offset, quantized);
            }
        }
    }

    private static void ApplyErrorDiffusion(
        byte[] pixels,
        int stride,
        int width,
        int height,
        ImageProcessingOptions options,
        DiffusionKernel kernel)
    {
        int padding = kernel.MaxHorizontalOffset;
        int rowLength = width + (padding * 2);
        int rowCount = kernel.MaxVerticalOffset + 1;
        var redErrors = CreateErrorRows(rowCount, rowLength);
        var greenErrors = CreateErrorRows(rowCount, rowLength);
        var blueErrors = CreateErrorRows(rowCount, rowLength);

        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + (x * 4);
                int errorIndex = x + padding;
                RgbValue source = ReadPixel(pixels, offset, options.Invert);
                var adjusted = new RgbValue(
                    ClampByteRange(source.Red + redErrors[0][errorIndex]),
                    ClampByteRange(source.Green + greenErrors[0][errorIndex]),
                    ClampByteRange(source.Blue + blueErrors[0][errorIndex]));
                RgbValue quantized = Quantize(adjusted, options);
                SetPixel(pixels, offset, quantized);

                double redError = adjusted.Red - quantized.Red;
                double greenError = adjusted.Green - quantized.Green;
                double blueError = adjusted.Blue - quantized.Blue;

                foreach (DiffusionTap tap in kernel.Taps)
                {
                    int targetX = x + tap.X;
                    if (targetX < 0 || targetX >= width)
                    {
                        continue;
                    }

                    int targetIndex = targetX + padding;
                    double factor = tap.Weight / kernel.Divisor;
                    redErrors[tap.Y][targetIndex] += redError * factor;
                    greenErrors[tap.Y][targetIndex] += greenError * factor;
                    blueErrors[tap.Y][targetIndex] += blueError * factor;
                }
            }

            RotateErrorRows(redErrors);
            RotateErrorRows(greenErrors);
            RotateErrorRows(blueErrors);
        }
    }

    private static double[][] CreateErrorRows(int rowCount, int rowLength)
    {
        var rows = new double[rowCount][];
        for (int i = 0; i < rowCount; i++)
        {
            rows[i] = new double[rowLength];
        }

        return rows;
    }

    private static void RotateErrorRows(double[][] rows)
    {
        double[] first = rows[0];
        for (int i = 0; i < rows.Length - 1; i++)
        {
            rows[i] = rows[i + 1];
        }

        Array.Clear(first);
        rows[^1] = first;
    }

    private static RgbValue ReadPixel(byte[] pixels, int offset, bool invert)
    {
        double red = pixels[offset + 2];
        double green = pixels[offset + 1];
        double blue = pixels[offset];

        return invert
            ? new RgbValue(255 - red, 255 - green, 255 - blue)
            : new RgbValue(red, green, blue);
    }

    private static RgbValue Quantize(
        RgbValue source,
        ImageProcessingOptions options)
    {
        return options.ColorMode switch
        {
            ColorQuantizationMode.Grayscale => QuantizeGrayscale(source, options),
            ColorQuantizationMode.RgbUniform => new RgbValue(
                QuantizeChannel(source.Red, options.RgbBits),
                QuantizeChannel(source.Green, options.RgbBits),
                QuantizeChannel(source.Blue, options.RgbBits)),
            ColorQuantizationMode.RgbPerChannel => new RgbValue(
                QuantizeChannel(source.Red, options.RedBits),
                QuantizeChannel(source.Green, options.GreenBits),
                QuantizeChannel(source.Blue, options.BlueBits)),
            ColorQuantizationMode.CustomPalette => FindNearestPaletteColor(
                source,
                options.Palette!),
            _ => throw new ArgumentOutOfRangeException(nameof(options.ColorMode)),
        };
    }

    private static RgbValue QuantizeGrayscale(
        RgbValue source,
        ImageProcessingOptions options)
    {
        double luminance =
            (0.2126 * source.Red) +
            (0.7152 * source.Green) +
            (0.0722 * source.Blue);
        double value = options.GrayscaleBits == 1
            ? luminance >= options.Threshold ? 255 : 0
            : QuantizeChannel(luminance, options.GrayscaleBits);
        return new RgbValue(value, value, value);
    }

    private static double QuantizeChannel(double value, int bits)
    {
        int maximumLevel = (1 << bits) - 1;
        int quantizedLevel = (int)Math.Round(
            ClampByteRange(value) * maximumLevel / 255.0);
        return quantizedLevel * 255.0 / maximumLevel;
    }

    private static byte QuantizeChannelToByte(double value, int bits)
    {
        return (byte)Math.Clamp(
            (int)Math.Round(QuantizeChannel(value, bits)),
            0,
            255);
    }

    private static byte[][] CreateChannelQuantizationTables()
    {
        var tables = new byte[9][];
        for (int bits = 1; bits <= 8; bits++)
        {
            var table = new byte[256];
            for (int value = 0; value < table.Length; value++)
            {
                table[value] = QuantizeChannelToByte(value, bits);
            }

            tables[bits] = table;
        }

        return tables;
    }

    private static RgbValue FindNearestPaletteColor(
        RgbValue source,
        IReadOnlyList<Color> palette)
    {
        Color nearest = palette[0];
        double nearestDistance = double.MaxValue;

        foreach (Color candidate in palette)
        {
            double redDifference = source.Red - candidate.R;
            double greenDifference = source.Green - candidate.G;
            double blueDifference = source.Blue - candidate.B;
            double distance =
                (redDifference * redDifference * 0.2126) +
                (greenDifference * greenDifference * 0.7152) +
                (blueDifference * blueDifference * 0.0722);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return new RgbValue(nearest.R, nearest.G, nearest.B);
    }

    private static double ClampByteRange(double value)
    {
        return Math.Clamp(value, 0, 255);
    }

    private static void SetPixel(byte[] pixels, int offset, RgbValue value)
    {
        pixels[offset] = (byte)Math.Clamp((int)Math.Round(value.Blue), 0, 255);
        pixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(value.Green), 0, 255);
        pixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(value.Red), 0, 255);
        pixels[offset + 3] = 255;
    }

    private sealed record DiffusionKernel(double Divisor, DiffusionTap[] Taps)
    {
        public int MaxHorizontalOffset { get; } =
            Taps.Max(tap => Math.Abs(tap.X));

        public int MaxVerticalOffset { get; } =
            Taps.Max(tap => tap.Y);
    }

    private readonly record struct DiffusionTap(int X, int Y, double Weight);

    private readonly record struct RgbValue(double Red, double Green, double Blue);
}
