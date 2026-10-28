using System.Drawing.Drawing2D;

namespace DesktopBinarizer.Controls;

public sealed class PixelPreview : ScrollableControl
{
    private const int ImageMargin = 12;
    private Image? _image;
    private double _displayScale;
    private bool _showPixelSpacing;
    private int _pixelSpacingWidth = 1;
    private Color _pixelSpacingColor = Color.Black;
    private Bitmap? _renderedGrid;

    public PixelPreview()
    {
        AutoScroll = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 24, 24);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.Opaque |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
    }

    public Image? PreviewImage
    {
        get => _image;
        set => SetPreviewFrame(value, false, 1, Color.Black);
    }

    public double DisplayScale
    {
        get => _displayScale;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Bitmap? newGrid = CreateRenderedGrid(
                _image,
                value,
                _showPixelSpacing,
                _pixelSpacingWidth,
                _pixelSpacingColor);
            Bitmap? oldGrid = _renderedGrid;
            _displayScale = value;
            _renderedGrid = newGrid;
            AutoScrollPosition = Point.Empty;
            UpdateScrollArea();
            Invalidate();
            oldGrid?.Dispose();
        }
    }

    public void SetPreviewFrame(
        Image? image,
        bool showPixelSpacing,
        int pixelSpacingWidth,
        Color pixelSpacingColor)
    {
        if (pixelSpacingWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelSpacingWidth));
        }

        Bitmap? newGrid = CreateRenderedGrid(
            image,
            _displayScale,
            showPixelSpacing,
            pixelSpacingWidth,
            pixelSpacingColor);
        Image? oldImage = _image;
        Bitmap? oldGrid = _renderedGrid;
        _image = image;
        _renderedGrid = newGrid;
        _showPixelSpacing = showPixelSpacing;
        _pixelSpacingWidth = pixelSpacingWidth;
        _pixelSpacingColor = pixelSpacingColor;
        UpdateScrollArea();
        Invalidate();
        oldGrid?.Dispose();
        oldImage?.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The complete frame is painted into the double buffer in OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        if (_image is null)
        {
            using var brush = new SolidBrush(Color.Gainsboro);
            string text = AppLocalizer.Get("WaitingForFrame");
            SizeF textSize = e.Graphics.MeasureString(text, Font);
            e.Graphics.DrawString(
                text,
                Font,
                brush,
                (ClientSize.Width - textSize.Width) / 2,
                (ClientSize.Height - textSize.Height) / 2);
            return;
        }

        Rectangle destination = GetImageRectangle(_image.Size);
        if (_showPixelSpacing)
        {
            if (_renderedGrid is not null)
            {
                DrawPixelPerfect(e.Graphics, _renderedGrid, destination);
            }
        }
        else
        {
            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.DrawImage(
                _image,
                destination,
                0,
                0,
                _image.Width,
                _image.Height,
                GraphicsUnit.Pixel);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        Bitmap? newGrid = _displayScale == 0
            ? CreateRenderedGrid(
                _image,
                _displayScale,
                _showPixelSpacing,
                _pixelSpacingWidth,
                _pixelSpacingColor)
            : _renderedGrid;
        Bitmap? oldGrid = _displayScale == 0 ? _renderedGrid : null;
        if (_displayScale == 0)
        {
            _renderedGrid = newGrid;
        }

        if (_displayScale == 0)
        {
            AutoScrollPosition = Point.Empty;
        }

        UpdateScrollArea();
        base.OnResize(e);
        oldGrid?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeRenderedGrid();
            _image?.Dispose();
            _image = null;
        }

        base.Dispose(disposing);
    }

    private void UpdateScrollArea()
    {
        if (_image is null)
        {
            AutoScrollMinSize = Size.Empty;
            return;
        }

        Size scaledSize = GetDisplaySize(_image.Size);
        if (_displayScale == 0)
        {
            AutoScrollMinSize = Size.Empty;
            return;
        }

        AutoScrollMinSize = new Size(
            scaledSize.Width + (ImageMargin * 2),
            scaledSize.Height + (ImageMargin * 2));
    }

    private Rectangle GetImageRectangle(Size imageSize)
    {
        Size displaySize = GetDisplaySize(imageSize);
        int x = displaySize.Width + (ImageMargin * 2) <= ClientSize.Width
            ? (ClientSize.Width - displaySize.Width) / 2
            : ImageMargin + AutoScrollPosition.X;
        int y = displaySize.Height + (ImageMargin * 2) <= ClientSize.Height
            ? (ClientSize.Height - displaySize.Height) / 2
            : ImageMargin + AutoScrollPosition.Y;

        return new Rectangle(x, y, displaySize.Width, displaySize.Height);
    }

    private Size GetDisplaySize(Size imageSize)
    {
        double scale = GetEffectiveScale(imageSize);
        return _showPixelSpacing
            ? PixelGridRenderer.CalculateOutputSize(
                imageSize,
                scale,
                _pixelSpacingWidth)
            : new Size(
                Math.Max(1, (int)Math.Round(imageSize.Width * scale)),
                Math.Max(1, (int)Math.Round(imageSize.Height * scale)));
    }

    private double GetEffectiveScale(Size imageSize)
    {
        return GetEffectiveScale(
            imageSize,
            _displayScale,
            _showPixelSpacing,
            _pixelSpacingWidth);
    }

    private double GetEffectiveScale(
        Size imageSize,
        double displayScale,
        bool showPixelSpacing,
        int pixelSpacingWidth)
    {
        if (displayScale > 0)
        {
            return displayScale;
        }

        int availableWidth = Math.Max(1, ClientSize.Width - (ImageMargin * 2));
        int availableHeight = Math.Max(1, ClientSize.Height - (ImageMargin * 2));
        if (showPixelSpacing)
        {
            availableWidth = Math.Max(
                1,
                availableWidth -
                ((imageSize.Width - 1) * pixelSpacingWidth));
            availableHeight = Math.Max(
                1,
                availableHeight -
                ((imageSize.Height - 1) * pixelSpacingWidth));
        }

        return Math.Max(
            1.0 / Math.Max(imageSize.Width, imageSize.Height),
            Math.Min(
            (double)availableWidth / imageSize.Width,
            (double)availableHeight / imageSize.Height));
    }

    private Bitmap? CreateRenderedGrid(
        Image? image,
        double displayScale,
        bool showPixelSpacing,
        int pixelSpacingWidth,
        Color pixelSpacingColor)
    {
        if (!showPixelSpacing || image is null)
        {
            return null;
        }

        if (image is not Bitmap bitmap)
        {
            throw new InvalidOperationException(
                AppLocalizer.Get("PixelGridRequiresBitmap"));
        }

        return PixelGridRenderer.Render(
            bitmap,
            GetEffectiveScale(
                bitmap.Size,
                displayScale,
                showPixelSpacing,
                pixelSpacingWidth),
            pixelSpacingWidth,
            pixelSpacingColor);
    }

    private static void DrawPixelPerfect(
        Graphics graphics,
        Bitmap bitmap,
        Rectangle destination)
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.None;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(
            bitmap,
            destination,
            0,
            0,
            bitmap.Width,
            bitmap.Height,
            GraphicsUnit.Pixel);
    }

    private void DisposeRenderedGrid()
    {
        _renderedGrid?.Dispose();
        _renderedGrid = null;
    }
}
