namespace DesktopBinarizer.Controls;

public sealed class PalettePreview : Control
{
    private IReadOnlyList<Color> _colors = Array.Empty<Color>();

    public PalettePreview()
    {
        DoubleBuffered = true;
        Height = 34;
        BackColor = Color.White;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public IReadOnlyList<Color> Colors
    {
        get => _colors;
        set
        {
            _colors = value ?? Array.Empty<Color>();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        if (_colors.Count == 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                AppLocalizer.Get("NoColors"),
                Font,
                bounds,
                Color.DimGray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Graphics.DrawRectangle(Pens.Silver, bounds);
            return;
        }

        int visibleCount = Math.Min(_colors.Count, Math.Max(1, bounds.Width / 12));
        for (int index = 0; index < visibleCount; index++)
        {
            int left = bounds.Left + (index * bounds.Width / visibleCount);
            int right = bounds.Left + ((index + 1) * bounds.Width / visibleCount);
            using var brush = new SolidBrush(_colors[index]);
            e.Graphics.FillRectangle(
                brush,
                left,
                bounds.Top,
                Math.Max(1, right - left),
                bounds.Height);
        }

        e.Graphics.DrawRectangle(Pens.DimGray, bounds);
    }
}
