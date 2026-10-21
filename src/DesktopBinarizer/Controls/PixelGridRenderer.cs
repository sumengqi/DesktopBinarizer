using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopBinarizer.Controls;

public static class PixelGridRenderer
{
    public static Size CalculateOutputSize(
        Size logicalSize,
        double scale,
        int spacingWidth)
    {
        Validate(logicalSize, scale, spacingWidth);

        int contentWidth = GetBoundary(logicalSize.Width, scale);
        int contentHeight = GetBoundary(logicalSize.Height, scale);
        long width =
            contentWidth + (((long)logicalSize.Width - 1) * spacingWidth);
        long height =
            contentHeight + (((long)logicalSize.Height - 1) * spacingWidth);
        if (width > int.MaxValue || height > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSize));
        }

        return new Size((int)width, (int)height);
    }

    public static Bitmap Render(
        Bitmap source,
        double scale,
        int spacingWidth,
        Color spacingColor)
    {
        ArgumentNullException.ThrowIfNull(source);
        Size outputSize = CalculateOutputSize(source.Size, scale, spacingWidth);
        var output = new Bitmap(
            outputSize.Width,
            outputSize.Height,
            PixelFormat.Format32bppArgb);
        output.SetResolution(96, 96);

        Bitmap? convertedSource = null;
        Bitmap renderSource = source;
        if (source.PixelFormat != PixelFormat.Format32bppArgb)
        {
            convertedSource = new Bitmap(
                source.Width,
                source.Height,
                PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(convertedSource);
            graphics.DrawImageUnscaled(source, 0, 0);
            renderSource = convertedSource;
        }

        Rectangle sourceBounds = new(0, 0, renderSource.Width, renderSource.Height);
        Rectangle outputBounds = new(0, 0, output.Width, output.Height);
        BitmapData? sourceData = null;
        BitmapData? outputData = null;
        try
        {
            sourceData = renderSource.LockBits(
                sourceBounds,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            outputData = output.LockBits(
                outputBounds,
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int sourceByteCount = Math.Abs(sourceData.Stride) * renderSource.Height;
            int outputByteCount = Math.Abs(outputData.Stride) * output.Height;
            byte[] sourcePixels = new byte[sourceByteCount];
            byte[] outputPixels = new byte[outputByteCount];
            Marshal.Copy(sourceData.Scan0, sourcePixels, 0, sourceByteCount);
            Fill(outputPixels, outputData.Stride, output.Size, spacingColor);

            int[] xMap = BuildSourceMap(renderSource.Width, scale, spacingWidth);
            int[] yMap = BuildSourceMap(renderSource.Height, scale, spacingWidth);
            for (int outputY = 0; outputY < output.Height; outputY++)
            {
                int sourceY = yMap[outputY];
                if (sourceY < 0)
                {
                    continue;
                }

                int sourceRow = sourceY * sourceData.Stride;
                int outputRow = outputY * outputData.Stride;
                for (int outputX = 0; outputX < output.Width; outputX++)
                {
                    int sourceX = xMap[outputX];
                    if (sourceX < 0)
                    {
                        continue;
                    }

                    int sourceOffset = sourceRow + (sourceX * 4);
                    int outputOffset = outputRow + (outputX * 4);
                    outputPixels[outputOffset] = sourcePixels[sourceOffset];
                    outputPixels[outputOffset + 1] = sourcePixels[sourceOffset + 1];
                    outputPixels[outputOffset + 2] = sourcePixels[sourceOffset + 2];
                    outputPixels[outputOffset + 3] = 255;
                }
            }

            Marshal.Copy(outputPixels, 0, outputData.Scan0, outputByteCount);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
        finally
        {
            if (sourceData is not null)
            {
                renderSource.UnlockBits(sourceData);
            }

            if (outputData is not null)
            {
                output.UnlockBits(outputData);
            }

            convertedSource?.Dispose();
        }
    }

    private static int[] BuildSourceMap(
        int logicalPixelCount,
        double scale,
        int spacingWidth)
    {
        int outputLength =
            CalculateOutputLength(logicalPixelCount, scale, spacingWidth);
        int[] map = Enumerable.Repeat(-1, outputLength).ToArray();

        for (int sourceIndex = 0; sourceIndex < logicalPixelCount; sourceIndex++)
        {
            int start =
                GetBoundary(sourceIndex, scale) + (sourceIndex * spacingWidth);
            int end =
                GetBoundary(sourceIndex + 1, scale) + (sourceIndex * spacingWidth);
            for (int outputIndex = start; outputIndex < end; outputIndex++)
            {
                map[outputIndex] = sourceIndex;
            }
        }

        return map;
    }

    private static int CalculateOutputLength(
        int logicalPixelCount,
        double scale,
        int spacingWidth)
    {
        return checked(
            GetBoundary(logicalPixelCount, scale) +
            ((logicalPixelCount - 1) * spacingWidth));
    }

    private static int GetBoundary(int logicalPixelBoundary, double scale)
    {
        return Math.Max(
            logicalPixelBoundary == 0 ? 0 : 1,
            (int)Math.Round(
                logicalPixelBoundary * scale,
                MidpointRounding.AwayFromZero));
    }

    private static void Fill(
        byte[] pixels,
        int stride,
        Size size,
        Color color)
    {
        for (int y = 0; y < size.Height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < size.Width; x++)
            {
                int offset = row + (x * 4);
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static void Validate(
        Size logicalSize,
        double scale,
        int spacingWidth)
    {
        if (logicalSize.Width < 1 || logicalSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSize));
        }

        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        if (spacingWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(spacingWidth));
        }
    }
}
