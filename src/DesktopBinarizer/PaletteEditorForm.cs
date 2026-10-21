namespace DesktopBinarizer;

public sealed class PaletteEditorForm : Form
{
    private readonly ListBox _colorList = new();
    private readonly List<Color> _colors;

    public PaletteEditorForm(IEnumerable<Color> colors)
    {
        _colors = colors.ToList();

        Text = "自定义调色板";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 420);
        ClientSize = new Size(620, 500);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildInterface();
        RefreshColorList();
    }

    public IReadOnlyList<Color> Palette => _colors.ToArray();

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var description = new Label
        {
            Text = "添加 2–256 种颜色，可从颜色对话框或屏幕像素取色。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(description, 0, 0);
        root.SetColumnSpan(description, 2);

        _colorList.Dock = DockStyle.Fill;
        _colorList.DrawMode = DrawMode.OwnerDrawFixed;
        _colorList.ItemHeight = 32;
        _colorList.DrawItem += DrawColorItem;
        _colorList.DoubleClick += (_, _) => EditSelectedColor();
        root.Controls.Add(_colorList, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 0, 0, 0),
        };
        actions.Controls.Add(CreateButton("添加颜色", AddColor));
        actions.Controls.Add(CreateButton("屏幕取色", PickColorFromScreen));
        actions.Controls.Add(CreateButton("编辑颜色", EditSelectedColor));
        actions.Controls.Add(CreateButton("删除颜色", DeleteSelectedColor));
        actions.Controls.Add(CreateButton("上移", () => MoveSelectedColor(-1)));
        actions.Controls.Add(CreateButton("下移", () => MoveSelectedColor(1)));
        actions.Controls.Add(CreateSpacer(10));
        actions.Controls.Add(CreateButton("黑白", () => SetPreset([Color.Black, Color.White])));
        actions.Controls.Add(CreateButton("RGB 8色", () => SetPreset(
        [
            Color.Black,
            Color.Red,
            Color.Lime,
            Color.Blue,
            Color.Cyan,
            Color.Magenta,
            Color.Yellow,
            Color.White,
        ])));
        actions.Controls.Add(CreateButton("Game Boy", () => SetPreset(
        [
            Color.FromArgb(15, 56, 15),
            Color.FromArgb(48, 98, 48),
            Color.FromArgb(139, 172, 15),
            Color.FromArgb(155, 188, 15),
        ])));
        root.Controls.Add(actions, 1, 1);

        var dialogButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
        };
        var okButton = new Button
        {
            Text = "确定",
            AutoSize = true,
        };
        okButton.Click += (_, _) => AcceptPalette();
        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        dialogButtons.Controls.Add(okButton);
        dialogButtons.Controls.Add(cancelButton);
        root.Controls.Add(dialogButtons, 0, 2);
        root.SetColumnSpan(dialogButtons, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.Add(root);
    }

    private void AddColor()
    {
        if (_colors.Count >= 256)
        {
            MessageBox.Show(this, "调色板最多支持 256 种颜色。", "提示");
            return;
        }

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _colors.Count == 0 ? Color.Black : _colors[^1],
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _colors.Add(dialog.Color);
        RefreshColorList(_colors.Count - 1);
    }

    private void EditSelectedColor()
    {
        int index = _colorList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _colors[index],
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _colors[index] = dialog.Color;
        RefreshColorList(index);
    }

    private void PickColorFromScreen()
    {
        if (_colors.Count >= 256)
        {
            MessageBox.Show(this, "调色板最多支持 256 种颜色。", "提示");
            return;
        }

        using var picker = new ScreenColorPickerForm();
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _colors.Add(picker.SelectedColor);
        RefreshColorList(_colors.Count - 1);
    }

    private void DeleteSelectedColor()
    {
        int index = _colorList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        _colors.RemoveAt(index);
        RefreshColorList(Math.Min(index, _colors.Count - 1));
    }

    private void MoveSelectedColor(int offset)
    {
        int oldIndex = _colorList.SelectedIndex;
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= _colors.Count)
        {
            return;
        }

        (_colors[oldIndex], _colors[newIndex]) = (_colors[newIndex], _colors[oldIndex]);
        RefreshColorList(newIndex);
    }

    private void SetPreset(IEnumerable<Color> colors)
    {
        _colors.Clear();
        _colors.AddRange(colors);
        RefreshColorList(0);
    }

    private void AcceptPalette()
    {
        if (_colors.Count < 2)
        {
            MessageBox.Show(
                this,
                "自定义调色板至少需要两种颜色。",
                "调色板无效",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void RefreshColorList(int selectedIndex = -1)
    {
        _colorList.BeginUpdate();
        try
        {
            _colorList.Items.Clear();
            foreach (Color color in _colors)
            {
                _colorList.Items.Add(color);
            }
        }
        finally
        {
            _colorList.EndUpdate();
        }

        if (selectedIndex >= 0 && selectedIndex < _colorList.Items.Count)
        {
            _colorList.SelectedIndex = selectedIndex;
        }
    }

    private static void DrawColorItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox ||
            e.Index < 0 ||
            e.Index >= listBox.Items.Count ||
            listBox.Items[e.Index] is not Color color)
        {
            return;
        }

        e.DrawBackground();
        var swatch = new Rectangle(
            e.Bounds.Left + 5,
            e.Bounds.Top + 5,
            44,
            e.Bounds.Height - 10);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, swatch);
        e.Graphics.DrawRectangle(Pens.DimGray, swatch);

        string text = $"#{color.R:X2}{color.G:X2}{color.B:X2}    RGB({color.R}, {color.G}, {color.B})";
        TextRenderer.DrawText(
            e.Graphics,
            text,
            e.Font,
            new Point(swatch.Right + 10, e.Bounds.Top + 7),
            e.ForeColor);
        e.DrawFocusRectangle();
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Margin = new Padding(0, 0, 0, 6),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Panel CreateSpacer(int height)
    {
        return new Panel
        {
            Width = 1,
            Height = height,
            Margin = new Padding(0),
        };
    }
}
