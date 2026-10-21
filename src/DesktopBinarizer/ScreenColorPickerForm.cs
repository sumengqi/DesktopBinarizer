using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DesktopBinarizer;

public sealed class ScreenColorPickerForm : Form
{
    private const int MagnifierPixels = 15;
    private const int MagnifierPixelSize = 10;
    private const int PanelPadding = 10;
    private readonly Bitmap _snapshot;
    private Point _currentPoint;

    public ScreenColorPickerForm()
    {
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        _snapshot = CaptureScreen(virtualScreen);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Text = "屏幕取色";

        _currentPoint = PointToClient(Cursor.Position);
        MouseMove += (_, eventArgs) =>
        {
            _currentPoint = ClampPoint(eventArgs.Location);
            Invalidate();
        };
        MouseDown += OnPickerMouseDown;
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };
    }

    public Color SelectedColor { get; private set; }

    public static Color SampleColor(Bitmap snapshot, Point point)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (point.X < 0 ||
            point.X >= snapshot.Width ||
            point.Y < 0 ||
            point.Y >= snapshot.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }

        return snapshot.GetPixel(point.X, point.Y);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_snapshot, Point.Empty);

        Point point = ClampPoint(_currentPoint);
        Color color = SampleColor(_snapshot, point);
        DrawCrosshair(e.Graphics, point, color);
        DrawMagnifier(e.Graphics, point, color);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _snapshot.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Bitmap CaptureScreen(Rectangle bounds)
    {
        var bitmap = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            bounds.Left,
            bounds.Top,
            0,
            0,
            bounds.Size,
            CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private void OnPickerMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Point point = ClampPoint(e.Location);
        SelectedColor = SampleColor(_snapshot, point);
        DialogResult = DialogResult.OK;
        Close();
    }

    private Point ClampPoint(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, _snapshot.Width - 1)),
            Math.Clamp(point.Y, 0, Math.Max(0, _snapshot.Height - 1)));
    }

    private void DrawCrosshair(Graphics graphics, Point point, Color sampledColor)
    {
        Color outlineColor = GetContrastingColor(sampledColor);
        using var outlinePen = new Pen(outlineColor, 3);
        using var centerPen = new Pen(Color.Red, 1);

        graphics.DrawLine(outlinePen, point.X - 18, point.Y, point.X + 18, point.Y);
        graphics.DrawLine(outlinePen, point.X, point.Y - 18, point.X, point.Y + 18);
        graphics.DrawLine(centerPen, point.X - 18, point.Y, point.X + 18, point.Y);
        graphics.DrawLine(centerPen, point.X, point.Y - 18, point.X, point.Y + 18);
        graphics.DrawRectangle(
            Pens.Red,
            point.X - 2,
            point.Y - 2,
            4,
            4);
    }

    private void DrawMagnifier(Graphics graphics, Point point, Color color)
    {
        int imageSize = MagnifierPixels * MagnifierPixelSize;
        const int detailsWidth = 190;
        int panelWidth = imageSize + detailsWidth + (PanelPadding * 3);
        int panelHeight = imageSize + (PanelPadding * 2);
        Rectangle panel = GetPanelBounds(point, panelWidth, panelHeight);

        using var panelBrush = new SolidBrush(Color.FromArgb(235, 24, 24, 24));
        graphics.FillRectangle(panelBrush, panel);
        graphics.DrawRectangle(Pens.White, panel);

        Rectangle imageBounds = new(
            panel.Left + PanelPadding,
            panel.Top + PanelPadding,
            imageSize,
            imageSize);
        int half = MagnifierPixels / 2;
        Rectangle source = new(
            point.X - half,
            point.Y - half,
            MagnifierPixels,
            MagnifierPixels);

        GraphicsState state = graphics.Save();
        graphics.SetClip(imageBounds);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(
            _snapshot,
            imageBounds,
            source,
            GraphicsUnit.Pixel);
        graphics.Restore(state);

        using var gridPen = new Pen(Color.FromArgb(90, Color.Black));
        for (int index = 1; index < MagnifierPixels; index++)
        {
            int x = imageBounds.Left + (index * MagnifierPixelSize);
            int y = imageBounds.Top + (index * MagnifierPixelSize);
            graphics.DrawLine(gridPen, x, imageBounds.Top, x, imageBounds.Bottom);
            graphics.DrawLine(gridPen, imageBounds.Left, y, imageBounds.Right, y);
        }

        int center = MagnifierPixels / 2;
        Rectangle selectedPixel = new(
            imageBounds.Left + (center * MagnifierPixelSize),
            imageBounds.Top + (center * MagnifierPixelSize),
            MagnifierPixelSize,
            MagnifierPixelSize);
        using var selectedPen = new Pen(Color.Red, 2);
        graphics.DrawRectangle(selectedPen, selectedPixel);
        graphics.DrawRectangle(Pens.White, imageBounds);

        Rectangle swatch = new(
            imageBounds.Right + PanelPadding,
            panel.Top + PanelPadding,
            54,
            54);
        using var swatchBrush = new SolidBrush(color);
        graphics.FillRectangle(swatchBrush, swatch);
        graphics.DrawRectangle(Pens.White, swatch);

        int detailsLeft = swatch.Left;
        int detailsTop = swatch.Bottom + 10;
        TextRenderer.DrawText(
            graphics,
            $"#{color.R:X2}{color.G:X2}{color.B:X2}",
            Font,
            new Rectangle(
                detailsLeft,
                detailsTop,
                detailsWidth,
                24),
            Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            graphics,
            $"RGB({color.R}, {color.G}, {color.B})",
            Font,
            new Rectangle(
                detailsLeft,
                detailsTop + 26,
                detailsWidth,
                24),
            Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            graphics,
            "左键选择",
            Font,
            new Rectangle(
                detailsLeft,
                detailsTop + 62,
                detailsWidth,
                22),
            Color.Gainsboro,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            graphics,
            "右键 / Esc 取消",
            Font,
            new Rectangle(
                detailsLeft,
                detailsTop + 84,
                detailsWidth,
                22),
            Color.Gainsboro,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private Rectangle GetPanelBounds(Point cursor, int width, int height)
    {
        const int offset = 24;
        int x = cursor.X + offset;
        int y = cursor.Y + offset;

        if (x + width > ClientSize.Width)
        {
            x = cursor.X - width - offset;
        }

        if (y + height > ClientSize.Height)
        {
            y = cursor.Y - height - offset;
        }

        x = Math.Clamp(x, 0, Math.Max(0, ClientSize.Width - width));
        y = Math.Clamp(y, 0, Math.Max(0, ClientSize.Height - height));
        return new Rectangle(x, y, width, height);
    }

    private static Color GetContrastingColor(Color color)
    {
        double luminance =
            (0.2126 * color.R) +
            (0.7152 * color.G) +
            (0.0722 * color.B);
        return luminance > 128 ? Color.Black : Color.White;
    }
}
