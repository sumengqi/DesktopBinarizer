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

            int outputRowBytes = output.Width * 4;
            var renderedRow = new byte[outputRowBytes];
            FillRow(renderedRow, spacingColor);
            for (int sourceY = 0; sourceY < renderSource.Height; sourceY++)
            {
                int sourceRow = sourceY * sourceData.Stride;
                for (int sourceX = 0; sourceX < renderSource.Width; sourceX++)
                {
                    int sourceOffset = sourceRow + (sourceX * 4);
                    int startX =
                        GetBoundary(sourceX, scale) +
                        (sourceX * spacingWidth);
                    int endX =
                        GetBoundary(sourceX + 1, scale) +
                        (sourceX * spacingWidth);
                    for (int outputX = startX; outputX < endX; outputX++)
                    {
                        int outputOffset = outputX * 4;
                        renderedRow[outputOffset] = sourcePixels[sourceOffset];
                        renderedRow[outputOffset + 1] = sourcePixels[sourceOffset + 1];
                        renderedRow[outputOffset + 2] = sourcePixels[sourceOffset + 2];
                        renderedRow[outputOffset + 3] = 255;
                    }
                }

                int startY =
                    GetBoundary(sourceY, scale) +
                    (sourceY * spacingWidth);
                int endY =
                    GetBoundary(sourceY + 1, scale) +
                    (sourceY * spacingWidth);
                for (int outputY = startY; outputY < endY; outputY++)
                {
                    Buffer.BlockCopy(
                        renderedRow,
                        0,
                        outputPixels,
                        outputY * outputData.Stride,
                        outputRowBytes);
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
        var rowPixels = new byte[size.Width * 4];
        FillRow(rowPixels, color);
        for (int y = 0; y < size.Height; y++)
        {
            Buffer.BlockCopy(
                rowPixels,
                0,
                pixels,
                y * stride,
                rowPixels.Length);
        }
    }

    private static void FillRow(byte[] rowPixels, Color color)
    {
        for (int offset = 0; offset < rowPixels.Length; offset += 4)
        {
            rowPixels[offset] = color.B;
            rowPixels[offset + 1] = color.G;
            rowPixels[offset + 2] = color.R;
            rowPixels[offset + 3] = 255;
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
