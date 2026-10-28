using System.Diagnostics;
using System.Drawing.Imaging;
using DesktopBinarizer.Capture;
using DesktopBinarizer.Configuration;
using DesktopBinarizer.Controls;
using DesktopBinarizer.Imaging;
using DesktopBinarizer.Recording;

namespace DesktopBinarizer;

public sealed class MainForm : Form
{
    private static readonly int[] FrameRates =
        [5, 10, 15, 20, 24, 30, 45, 60, 90, 120, 144, 165, 240];
    private static readonly double[] OutputMultipliers = [0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4];
    private static readonly double[] PreviewScales =
        [0, 0.125, 0.25, 1.0 / 3.0, 0.5, 1, 2, 3, 4, 8];

    private readonly ScreenCaptureService _captureService = new();
    private readonly VideoRecorder _recorder = new();
    private readonly Action<AppLanguage> _saveLanguagePreference;
    private readonly System.Windows.Forms.Timer _captureTimer = new();
    private readonly System.Windows.Forms.Timer _recordingStatusTimer = new();
    private readonly MenuStrip _menuStrip = new();
    private readonly ToolStripMenuItem _languageMenuItem = new();
    private readonly ToolStripMenuItem _chineseMenuItem = new();
    private readonly ToolStripMenuItem _englishMenuItem = new();
    private readonly ComboBox _sourceComboBox = new();
    private readonly ComboBox _windowComboBox = new();
    private readonly Button _refreshWindowsButton = new();
    private readonly ComboBox _inputScaleModeComboBox = new();
    private readonly ComboBox _outputSizeModeComboBox = new();
    private readonly NumericUpDown _outputWidthInput = new();
    private readonly NumericUpDown _outputHeightInput = new();
    private readonly ComboBox _outputMultiplierComboBox = new();
    private readonly Label _resolvedOutputLabel = new();
    private readonly ComboBox _previewScaleComboBox = new();
    private readonly CheckBox _showOriginalPreviewCheckBox = new();
    private readonly ComboBox _colorModeComboBox = new();
    private readonly NumericUpDown _grayscaleBitsInput = new();
    private readonly NumericUpDown _rgbBitsInput = new();
    private readonly NumericUpDown _redBitsInput = new();
    private readonly NumericUpDown _greenBitsInput = new();
    private readonly NumericUpDown _blueBitsInput = new();
    private readonly TableLayoutPanel _grayscaleOptionsPanel = new();
    private readonly TableLayoutPanel _rgbUniformOptionsPanel = new();
    private readonly TableLayoutPanel _rgbPerChannelOptionsPanel = new();
    private readonly TableLayoutPanel _paletteOptionsPanel = new();
    private readonly TableLayoutPanel _thresholdOptionsPanel = new();
    private readonly PalettePreview _palettePreview = new();
    private readonly Label _paletteCountLabel = new();
    private readonly Button _editPaletteButton = new();
    private readonly TrackBar _thresholdTrackBar = new();
    private readonly Label _thresholdValueLabel = new();
    private readonly ComboBox _ditherAlgorithmComboBox = new();
    private readonly CheckBox _invertCheckBox = new();
    private readonly CheckBox _pixelSpacingCheckBox = new();
    private readonly NumericUpDown _pixelSpacingWidthInput = new();
    private readonly ComboBox _pixelSpacingColorComboBox = new();
    private readonly Button _editSpacingPaletteButton = new();
    private readonly TableLayoutPanel _pixelSpacingOptionsPanel = new();
    private readonly ComboBox _frameRateComboBox = new();
    private readonly CheckBox _captureSystemAudioCheckBox = new();
    private readonly Button _recordButton = new();
    private readonly Button _pauseButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _importConfigurationButton = new();
    private readonly Button _exportConfigurationButton = new();
    private readonly PixelPreview _preview = new();
    private readonly PixelPreview _originalPreview = new();
    private readonly TableLayoutPanel _previewLayout = new();
    private readonly TableLayoutPanel _originalPreviewContainer = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _recordingTimeLabel = new();
    private readonly ToolStripStatusLabel _frameTimeLabel = new();
    private readonly List<Color> _customPalette = [Color.Black, Color.White];
    private Bitmap? _latestOutputFrame;

    private bool _isCapturing;
    private bool _isPaused;
    private bool _isClosing;
    private bool _closeCommitted;

    public MainForm()
        : this(language => AppPreferencesFile.Save(
            new AppPreferences { Language = language }))
    {
    }

    internal MainForm(Action<AppLanguage> saveLanguagePreference)
    {
        ArgumentNullException.ThrowIfNull(saveLanguagePreference);
        _saveLanguagePreference = saveLanguagePreference;
        Text = AppLocalizer.Get("AppTitle");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 700);
        ClientSize = new Size(1260, 840);
        BackColor = Color.FromArgb(245, 246, 248);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildInterface();
        WireEvents();
        RefreshWindowList();
        UpdateOutputControls();
        UpdateColorModeControls();
        UpdatePixelSpacingControls();
        UpdateOriginalPreviewMode();

        _captureTimer.Interval = GetFrameInterval(GetSelectedFrameRate());
        _captureTimer.Start();
        _recordingStatusTimer.Interval = 250;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var settingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = Color.White,
            AutoScroll = true,
        };

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = AppLocalizer.Get("HeaderTitle"),
            Tag = new LocalizedTextTag("HeaderTitle"),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
        };
        var subtitle = new Label
        {
            Text = AppLocalizer.Get("Subtitle"),
            Tag = new LocalizedTextTag("Subtitle"),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 20),
        };

        SetLocalizedText(_importConfigurationButton, "ImportConfiguration");
        _importConfigurationButton.AutoSize = true;
        _importConfigurationButton.Dock = DockStyle.Fill;
        SetLocalizedText(_exportConfigurationButton, "ExportConfiguration");
        _exportConfigurationButton.AutoSize = true;
        _exportConfigurationButton.Dock = DockStyle.Fill;
        var configurationButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 18),
        };
        configurationButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        configurationButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        configurationButtons.Controls.Add(_importConfigurationButton, 0, 0);
        configurationButtons.Controls.Add(_exportConfigurationButton, 1, 0);

        ConfigureLocalizedComboBox(_sourceComboBox, ["Desktop", "Window"], 0);
        ConfigureLocalizedComboBox(
            _inputScaleModeComboBox,
            ["ScaleProportional", "ScaleStretch"],
            0);

        _windowComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _windowComboBox.Dock = DockStyle.Fill;
        _windowComboBox.Enabled = false;

        SetLocalizedText(_refreshWindowsButton, "Refresh");
        _refreshWindowsButton.AutoSize = true;
        _refreshWindowsButton.Enabled = false;

        var windowSelector = CreateTwoColumnLayout();
        windowSelector.Controls.Add(_windowComboBox, 0, 0);
        windowSelector.Controls.Add(_refreshWindowsButton, 1, 0);

        ConfigureLocalizedComboBox(
            _outputSizeModeComboBox,
            ["OutputCustom", "OutputFollowInput", "OutputInputMultiplier"],
            0);
        ConfigureDimensionInput(_outputWidthInput, 400);
        ConfigureDimensionInput(_outputHeightInput, 300);

        var resolutionSelector = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0),
        };
        resolutionSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        resolutionSelector.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        resolutionSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        resolutionSelector.Controls.Add(_outputWidthInput, 0, 0);
        resolutionSelector.Controls.Add(new Label
        {
            Text = "×",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(8, 6, 8, 0),
        }, 1, 0);
        resolutionSelector.Controls.Add(_outputHeightInput, 2, 0);

        ConfigureComboBox(
            _outputMultiplierComboBox,
            ["0.25×", "0.5×", "0.75×", "1×", "1.5×", "2×", "3×", "4×"],
            3);

        _resolvedOutputLabel.Text = AppLocalizer.Format("CurrentOutput", 400, 300);
        _resolvedOutputLabel.AutoSize = true;
        _resolvedOutputLabel.ForeColor = Color.DimGray;
        _resolvedOutputLabel.Margin = new Padding(0, 6, 0, 0);

        ConfigureLocalizedComboBox(
            _previewScaleComboBox,
            [
                "PreviewFit",
                "Preview0125",
                "Preview025",
                "Preview0333",
                "Preview05",
                "Preview1",
                "Preview2",
                "Preview3",
                "Preview4",
                "Preview8",
            ],
            0);
        SetLocalizedText(_showOriginalPreviewCheckBox, "ShowOriginalPreview");
        _showOriginalPreviewCheckBox.AutoSize = true;
        _showOriginalPreviewCheckBox.Margin = new Padding(0, 8, 0, 0);

        ConfigureLocalizedComboBox(
            _colorModeComboBox,
            [
                "ColorModeGrayscale",
                "ColorModeRgbUniform",
                "ColorModeRgbPerChannel",
                "ColorModeCustomPalette",
            ],
            (int)ColorQuantizationMode.Grayscale);

        ConfigureBitDepthInput(_grayscaleBitsInput, 1);
        ConfigureBitDepthInput(_rgbBitsInput, 4);
        ConfigureBitDepthInput(_redBitsInput, 4);
        ConfigureBitDepthInput(_greenBitsInput, 4);
        ConfigureBitDepthInput(_blueBitsInput, 4);

        ConfigureSingleOptionPanel(
            _grayscaleOptionsPanel,
            "GrayscaleDepth",
            _grayscaleBitsInput,
            "BitRange");
        ConfigureSingleOptionPanel(
            _rgbUniformOptionsPanel,
            "PerChannelDepth",
            _rgbBitsInput,
            "SameRgb");

        _rgbPerChannelOptionsPanel.Dock = DockStyle.Top;
        _rgbPerChannelOptionsPanel.AutoSize = true;
        _rgbPerChannelOptionsPanel.ColumnCount = 3;
        _rgbPerChannelOptionsPanel.Margin = new Padding(0, 6, 0, 0);
        _rgbPerChannelOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        _rgbPerChannelOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        _rgbPerChannelOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        _rgbPerChannelOptionsPanel.Controls.Add(
            CreateLabeledControl("RedDepth", _redBitsInput),
            0,
            0);
        _rgbPerChannelOptionsPanel.Controls.Add(
            CreateLabeledControl("GreenDepth", _greenBitsInput),
            1,
            0);
        _rgbPerChannelOptionsPanel.Controls.Add(
            CreateLabeledControl("BlueDepth", _blueBitsInput),
            2,
            0);

        _paletteOptionsPanel.Dock = DockStyle.Top;
        _paletteOptionsPanel.AutoSize = true;
        _paletteOptionsPanel.ColumnCount = 2;
        _paletteOptionsPanel.Margin = new Padding(0, 8, 0, 0);
        _paletteOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _paletteOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _palettePreview.Dock = DockStyle.Fill;
        _palettePreview.Colors = _customPalette.ToArray();
        _paletteCountLabel.Text = AppLocalizer.Format("ColorCount", 2);
        _paletteCountLabel.AutoSize = true;
        _paletteCountLabel.ForeColor = Color.DimGray;
        _paletteCountLabel.Margin = new Padding(0, 5, 0, 0);

        var paletteVisual = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0),
        };
        paletteVisual.Controls.Add(_palettePreview);
        paletteVisual.Controls.Add(_paletteCountLabel);

        SetLocalizedText(_editPaletteButton, "EditPalette");
        _editPaletteButton.AutoSize = true;
        _editPaletteButton.Anchor = AnchorStyles.Top;
        _editPaletteButton.Margin = new Padding(8, 0, 0, 0);
        _paletteOptionsPanel.Controls.Add(paletteVisual, 0, 0);
        _paletteOptionsPanel.Controls.Add(_editPaletteButton, 1, 0);

        _thresholdTrackBar.Minimum = 0;
        _thresholdTrackBar.Maximum = 255;
        _thresholdTrackBar.TickFrequency = 32;
        _thresholdTrackBar.Value = 128;
        _thresholdTrackBar.Dock = DockStyle.Top;

        _thresholdValueLabel.Text = "128";
        _thresholdValueLabel.AutoSize = true;
        _thresholdValueLabel.ForeColor = Color.DimGray;

        var thresholdHeader = CreateTwoColumnLayout();
        thresholdHeader.Controls.Add(CreateSectionLabel("BinaryThreshold"), 0, 0);
        thresholdHeader.Controls.Add(_thresholdValueLabel, 1, 0);

        _thresholdOptionsPanel.Dock = DockStyle.Top;
        _thresholdOptionsPanel.AutoSize = true;
        _thresholdOptionsPanel.ColumnCount = 1;
        _thresholdOptionsPanel.Margin = new Padding(0, 8, 0, 0);
        _thresholdOptionsPanel.Controls.Add(thresholdHeader);
        _thresholdOptionsPanel.Controls.Add(_thresholdTrackBar);

        ConfigureLocalizedComboBox(
            _ditherAlgorithmComboBox,
            [
                "DitherNone",
                "DitherFloydSteinberg",
                "DitherAtkinson",
                "DitherJarvisJudiceNinke",
                "DitherStucki",
                "DitherBurkes",
                "DitherSierraLite",
            ],
            (int)DitherAlgorithm.FloydSteinberg);

        SetLocalizedText(_invertCheckBox, "InvertColors");
        _invertCheckBox.AutoSize = true;
        _invertCheckBox.Margin = new Padding(0, 10, 0, 18);

        SetLocalizedText(_pixelSpacingCheckBox, "EnablePixelSpacing");
        _pixelSpacingCheckBox.AutoSize = true;
        _pixelSpacingCheckBox.Margin = new Padding(0, 0, 0, 8);

        _pixelSpacingWidthInput.Minimum = 1;
        _pixelSpacingWidthInput.Maximum = 32;
        _pixelSpacingWidthInput.Value = 1;
        _pixelSpacingWidthInput.Dock = DockStyle.Fill;
        _pixelSpacingWidthInput.TextAlign = HorizontalAlignment.Center;

        _pixelSpacingColorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _pixelSpacingColorComboBox.DrawMode = DrawMode.OwnerDrawFixed;
        _pixelSpacingColorComboBox.ItemHeight = 24;
        _pixelSpacingColorComboBox.Dock = DockStyle.Fill;
        _pixelSpacingColorComboBox.DrawItem += DrawColorComboItem;
        RefreshPixelSpacingColors();

        SetLocalizedText(_editSpacingPaletteButton, "EditPalette");
        _editSpacingPaletteButton.AutoSize = true;
        _editSpacingPaletteButton.Dock = DockStyle.Fill;

        _pixelSpacingOptionsPanel.Dock = DockStyle.Top;
        _pixelSpacingOptionsPanel.AutoSize = true;
        _pixelSpacingOptionsPanel.ColumnCount = 3;
        _pixelSpacingOptionsPanel.RowCount = 2;
        _pixelSpacingOptionsPanel.Margin = new Padding(0);
        _pixelSpacingOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _pixelSpacingOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _pixelSpacingOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pixelSpacingOptionsPanel.Controls.Add(new Label
        {
            Text = AppLocalizer.Get("SpacingWidth"),
            Tag = new LocalizedTextTag("SpacingWidth"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0),
        }, 0, 0);
        _pixelSpacingOptionsPanel.Controls.Add(_pixelSpacingWidthInput, 1, 0);
        _pixelSpacingOptionsPanel.Controls.Add(new Label
        {
            Text = "px",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 6, 0, 0),
        }, 2, 0);
        _pixelSpacingOptionsPanel.Controls.Add(new Label
        {
            Text = AppLocalizer.Get("SpacingColor"),
            Tag = new LocalizedTextTag("SpacingColor"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 10, 8, 0),
        }, 0, 1);
        _pixelSpacingOptionsPanel.Controls.Add(_pixelSpacingColorComboBox, 1, 1);
        _pixelSpacingOptionsPanel.Controls.Add(_editSpacingPaletteButton, 2, 1);

        ConfigureComboBox(
            _frameRateComboBox,
            FrameRates.Select(rate => $"{rate} FPS").Cast<object>().ToArray(),
            Array.IndexOf(FrameRates, 30));

        SetLocalizedText(_captureSystemAudioCheckBox, "CaptureSystemAudio");
        _captureSystemAudioCheckBox.Checked = true;
        _captureSystemAudioCheckBox.AutoSize = true;
        _captureSystemAudioCheckBox.Margin = new Padding(0, 10, 0, 0);

        SetLocalizedText(_recordButton, "StartRecording");
        _recordButton.AutoSize = true;
        _recordButton.Dock = DockStyle.Top;
        _recordButton.Margin = new Padding(0, 8, 0, 0);
        _recordButton.BackColor = Color.FromArgb(215, 55, 55);
        _recordButton.ForeColor = Color.White;
        _recordButton.FlatStyle = FlatStyle.Flat;

        SetLocalizedText(_pauseButton, "Pause");
        _pauseButton.AutoSize = true;
        _pauseButton.Dock = DockStyle.Fill;

        SetLocalizedText(_saveButton, "SaveCurrentFrame");
        _saveButton.AutoSize = true;
        _saveButton.Dock = DockStyle.Fill;

        var utilityButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 10, 0, 0),
        };
        utilityButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        utilityButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        utilityButtons.Controls.Add(_pauseButton, 0, 0);
        utilityButtons.Controls.Add(_saveButton, 1, 0);

        settings.Controls.Add(title);
        settings.Controls.Add(subtitle);
        settings.Controls.Add(configurationButtons);
        settings.Controls.Add(CreateSectionLabel("SectionCaptureSource"));
        settings.Controls.Add(_sourceComboBox);
        settings.Controls.Add(CreateSpacer(8));
        settings.Controls.Add(windowSelector);
        settings.Controls.Add(CreateSpacer(12));
        settings.Controls.Add(CreateSectionLabel("SectionInputScaling"));
        settings.Controls.Add(_inputScaleModeComboBox);
        settings.Controls.Add(CreateSpacer(18));
        settings.Controls.Add(CreateSectionLabel("SectionOutputResolution"));
        settings.Controls.Add(_outputSizeModeComboBox);
        settings.Controls.Add(CreateSpacer(6));
        settings.Controls.Add(resolutionSelector);
        settings.Controls.Add(_outputMultiplierComboBox);
        settings.Controls.Add(_resolvedOutputLabel);
        settings.Controls.Add(CreateSpacer(18));
        settings.Controls.Add(CreateSectionLabel("SectionPreviewScale"));
        settings.Controls.Add(_previewScaleComboBox);
        settings.Controls.Add(_showOriginalPreviewCheckBox);
        settings.Controls.Add(CreateSpacer(18));
        settings.Controls.Add(CreateSectionLabel("SectionColorMode"));
        settings.Controls.Add(_colorModeComboBox);
        settings.Controls.Add(_grayscaleOptionsPanel);
        settings.Controls.Add(_rgbUniformOptionsPanel);
        settings.Controls.Add(_rgbPerChannelOptionsPanel);
        settings.Controls.Add(_paletteOptionsPanel);
        settings.Controls.Add(_thresholdOptionsPanel);
        settings.Controls.Add(CreateSpacer(14));
        settings.Controls.Add(CreateSectionLabel("SectionDithering"));
        settings.Controls.Add(_ditherAlgorithmComboBox);
        settings.Controls.Add(_invertCheckBox);
        settings.Controls.Add(CreateSectionLabel("SectionPixelSpacing"));
        settings.Controls.Add(_pixelSpacingCheckBox);
        settings.Controls.Add(_pixelSpacingOptionsPanel);
        settings.Controls.Add(CreateSpacer(18));
        settings.Controls.Add(CreateSectionLabel("SectionFrameRate"));
        settings.Controls.Add(_frameRateComboBox);
        settings.Controls.Add(_captureSystemAudioCheckBox);
        settings.Controls.Add(_recordButton);
        settings.Controls.Add(utilityButtons);
        settingsPanel.Controls.Add(settings);

        var previewFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 0, 0),
        };
        _previewLayout.Dock = DockStyle.Fill;
        _previewLayout.ColumnCount = 2;
        _previewLayout.RowCount = 1;
        _previewLayout.Margin = new Padding(0);
        _previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _previewLayout.Controls.Add(
            ConfigurePreviewContainer(
                _originalPreviewContainer,
                "OriginalPreview",
                _originalPreview),
            0,
            0);
        _previewLayout.Controls.Add(
            ConfigurePreviewContainer(
                new TableLayoutPanel(),
                "OutputPreview",
                _preview),
            1,
            0);
        previewFrame.Controls.Add(_previewLayout);

        var statusStrip = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.White,
        };
        _statusLabel.Text = AppLocalizer.Get("Ready");
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _recordingTimeLabel.Text = string.Empty;
        _recordingTimeLabel.ForeColor = Color.Firebrick;
        _frameTimeLabel.Text = "0 ms";
        statusStrip.Items.Add(_statusLabel);
        statusStrip.Items.Add(_recordingTimeLabel);
        statusStrip.Items.Add(_frameTimeLabel);

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(previewFrame, 1, 0);
        root.Controls.Add(statusStrip, 0, 1);
        root.SetColumnSpan(statusStrip, 2);
        Controls.Add(root);
        BuildLanguageMenu();
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        _menuStrip.BringToFront();
    }

    private void BuildLanguageMenu()
    {
        _menuStrip.BackColor = Color.White;
        _menuStrip.Items.Add(_languageMenuItem);
        _languageMenuItem.DropDownItems.Add(_chineseMenuItem);
        _languageMenuItem.DropDownItems.Add(_englishMenuItem);
        UpdateLanguageMenu();
    }

    private void ChangeLanguage(AppLanguage language)
    {
        if (AppLocalizer.Language == language)
        {
            return;
        }

        AppLocalizer.Language = language;
        ApplyLocalizedControls(this);
        UpdateLanguageMenu();
        Text = AppLocalizer.Get("AppTitle");
        _paletteCountLabel.Text = AppLocalizer.Format("ColorCount", _customPalette.Count);
        _pauseButton.Text = AppLocalizer.Get(_isPaused ? "Resume" : "Pause");
        _recordButton.Text = AppLocalizer.Get(
            _recorder.IsRecording ? "StopRecording" : "StartRecording");
        _statusLabel.Text = AppLocalizer.Get(
            _recorder.IsRecording
                ? "RecordingActive"
                : _isPaused
                    ? "Paused"
                    : "Ready");
        UpdateOutputControls();
        _preview.Invalidate();
        _originalPreview.Invalidate();
        _palettePreview.Invalidate();
        PerformLayout();

        try
        {
            _saveLanguagePreference(language);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Format("PreferenceSaveError", exception.Message),
                AppLocalizer.Get("Information"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateLanguageMenu()
    {
        _languageMenuItem.Text = AppLocalizer.Get("Language");
        _chineseMenuItem.Text = AppLocalizer.Get("ChineseSimplified");
        _englishMenuItem.Text = AppLocalizer.Get("English");
        _chineseMenuItem.Checked =
            AppLocalizer.Language == AppLanguage.ChineseSimplified;
        _englishMenuItem.Checked = AppLocalizer.Language == AppLanguage.English;
    }

    private static void ApplyLocalizedControls(Control parent)
    {
        if (parent.Tag is LocalizedTextTag textTag)
        {
            parent.Text = AppLocalizer.Get(textTag.Key);
        }
        else if (parent is ComboBox comboBox &&
                 parent.Tag is LocalizedItemsTag itemsTag)
        {
            ReplaceLocalizedItems(comboBox, itemsTag.Keys);
        }

        foreach (Control child in parent.Controls)
        {
            ApplyLocalizedControls(child);
        }
    }

    private void WireEvents()
    {
        _chineseMenuItem.Click += (_, _) => ChangeLanguage(AppLanguage.ChineseSimplified);
        _englishMenuItem.Click += (_, _) => ChangeLanguage(AppLanguage.English);

        _sourceComboBox.SelectedIndexChanged += (_, _) =>
        {
            bool windowMode = _sourceComboBox.SelectedIndex == 1;
            _windowComboBox.Enabled = windowMode && !_recorder.IsRecording;
            _refreshWindowsButton.Enabled = windowMode && !_recorder.IsRecording;
            if (windowMode)
            {
                RefreshWindowList();
            }
        };

        _refreshWindowsButton.Click += (_, _) => RefreshWindowList();
        _thresholdTrackBar.ValueChanged += (_, _) =>
            _thresholdValueLabel.Text = _thresholdTrackBar.Value.ToString();
        _colorModeComboBox.SelectedIndexChanged += (_, _) => UpdateColorModeControls();
        _grayscaleBitsInput.ValueChanged += (_, _) => UpdateColorModeControls();
        _editPaletteButton.Click += (_, _) => EditCustomPalette();
        _editSpacingPaletteButton.Click += (_, _) => EditCustomPalette();
        _pixelSpacingCheckBox.CheckedChanged += (_, _) => UpdatePixelSpacingControls();
        _outputSizeModeComboBox.SelectedIndexChanged += (_, _) => UpdateOutputControls();
        _outputMultiplierComboBox.SelectedIndexChanged += (_, _) => UpdateOutputControls();
        _outputWidthInput.ValueChanged += (_, _) => UpdateCustomOutputLabel();
        _outputHeightInput.ValueChanged += (_, _) => UpdateCustomOutputLabel();

        _previewScaleComboBox.SelectedIndexChanged += (_, _) =>
        {
            double scale =
                PreviewScales[Math.Max(0, _previewScaleComboBox.SelectedIndex)];
            _preview.DisplayScale = scale;
            _originalPreview.DisplayScale = scale;
        };
        _showOriginalPreviewCheckBox.CheckedChanged += (_, _) =>
            UpdateOriginalPreviewMode();

        _frameRateComboBox.SelectedIndexChanged += (_, _) =>
            _captureTimer.Interval = GetFrameInterval(GetSelectedFrameRate());

        _pauseButton.Click += (_, _) =>
        {
            _isPaused = !_isPaused;
            _pauseButton.Text = AppLocalizer.Get(_isPaused ? "Resume" : "Pause");
            _statusLabel.Text = AppLocalizer.Get(_isPaused ? "Paused" : "Capturing");
        };

        _recordButton.Click += async (_, _) =>
        {
            if (_recorder.IsRecording)
            {
                await StopRecordingAsync();
            }
            else
            {
                await StartRecordingAsync();
            }
        };

        _saveButton.Click += (_, _) => SaveCurrentFrame();
        _importConfigurationButton.Click += (_, _) => ImportConfiguration();
        _exportConfigurationButton.Click += (_, _) => ExportConfiguration();
        _outputWidthInput.Validated += (_, _) => CommitDimensionInput(_outputWidthInput);
        _outputHeightInput.Validated += (_, _) => CommitDimensionInput(_outputHeightInput);
        _captureTimer.Tick += CaptureTimerOnTick;
        _recordingStatusTimer.Tick += (_, _) =>
            _recordingTimeLabel.Text = $"● REC {_recorder.Elapsed:hh\\:mm\\:ss}";
        FormClosing += MainFormOnFormClosing;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captureTimer.Dispose();
            _recordingStatusTimer.Dispose();
            _captureService.Dispose();
            _latestOutputFrame?.Dispose();
            _latestOutputFrame = null;
        }

        base.Dispose(disposing);
    }

    private async void CaptureTimerOnTick(object? sender, EventArgs e)
    {
        if (_isCapturing || _isPaused || _isClosing)
        {
            return;
        }

        bool captureWindow = _sourceComboBox.SelectedIndex == 1;
        WindowInfo? selectedWindow = _windowComboBox.SelectedItem as WindowInfo;
        if (captureWindow && selectedWindow is null)
        {
            _statusLabel.Text = AppLocalizer.Get("SelectTargetWindow");
            return;
        }

        var settings = new CaptureSettings(
            (OutputSizeMode)_outputSizeModeComboBox.SelectedIndex,
            GetEffectiveCustomOutputSize(),
            OutputMultipliers[Math.Max(0, _outputMultiplierComboBox.SelectedIndex)],
            (InputScaleMode)_inputScaleModeComboBox.SelectedIndex,
            (ColorQuantizationMode)_colorModeComboBox.SelectedIndex,
            (int)_grayscaleBitsInput.Value,
            _thresholdTrackBar.Value,
            (int)_rgbBitsInput.Value,
            (int)_redBitsInput.Value,
            (int)_greenBitsInput.Value,
            (int)_blueBitsInput.Value,
            _customPalette.ToArray(),
            (DitherAlgorithm)_ditherAlgorithmComboBox.SelectedIndex,
            _invertCheckBox.Checked,
            _pixelSpacingCheckBox.Checked,
            (int)_pixelSpacingWidthInput.Value,
            _pixelSpacingColorComboBox.SelectedItem is Color spacingColor
                ? spacingColor
                : Color.Black,
            _showOriginalPreviewCheckBox.Checked);

        _isCapturing = true;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            (
                Bitmap processed,
                Bitmap logical,
                Bitmap? original,
                Size inputSize,
                Size logicalOutputSize
            ) =
                await Task.Run(() =>
            {
                using Bitmap captured = captureWindow
                    ? _captureService.CaptureWindow(selectedWindow!.Handle)
                    : _captureService.CaptureDesktop();

                Size outputSize = OutputSizeCalculator.Resolve(
                    captured.Size,
                    settings.OutputSizeMode,
                    settings.CustomOutputSize,
                    settings.OutputMultiplier);
                var options = new ImageProcessingOptions(
                    OutputWidth: outputSize.Width,
                    OutputHeight: outputSize.Height,
                    ColorMode: settings.ColorMode,
                    GrayscaleBits: settings.GrayscaleBits,
                    Threshold: settings.Threshold,
                    RgbBits: settings.RgbBits,
                    RedBits: settings.RedBits,
                    GreenBits: settings.GreenBits,
                    BlueBits: settings.BlueBits,
                    Palette: settings.Palette,
                    DitherAlgorithm: settings.DitherAlgorithm,
                    Invert: settings.Invert,
                    InputScaleMode: settings.InputScaleMode);

                Bitmap? logical = null;
                Bitmap? processed = null;
                Bitmap? original = null;
                try
                {
                    logical = ImageProcessor.Process(captured, options);
                    processed = settings.EnablePixelSpacing
                        ? ImageProcessor.ApplyPixelSpacing(
                            logical,
                            settings.PixelSpacingWidth,
                            settings.PixelSpacingColor)
                        : new Bitmap(logical);
                    original = settings.ShowOriginalPreview
                        ? new Bitmap(captured)
                        : null;
                    return (
                        processed,
                        logical,
                        original,
                        captured.Size,
                        outputSize);
                }
                catch
                {
                    processed?.Dispose();
                    logical?.Dispose();
                    original?.Dispose();
                    throw;
                }
            });

            if (_isClosing)
            {
                processed.Dispose();
                logical.Dispose();
                original?.Dispose();
                return;
            }

            if (_recorder.IsRecording)
            {
                _recorder.SubmitFrame(processed);
            }

            Bitmap? previousOutput = _latestOutputFrame;
            _latestOutputFrame = processed;
            previousOutput?.Dispose();
            _preview.SetPreviewFrame(
                logical,
                settings.EnablePixelSpacing,
                settings.PixelSpacingWidth,
                settings.PixelSpacingColor);
            if (_showOriginalPreviewCheckBox.Checked && original is not null)
            {
                _originalPreview.PreviewImage = original;
            }
            else
            {
                original?.Dispose();
                if (!_showOriginalPreviewCheckBox.Checked)
                {
                    _originalPreview.PreviewImage = null;
                }
            }
            _resolvedOutputLabel.Text = settings.EnablePixelSpacing
                ? AppLocalizer.Format(
                    "CaptureDimensions",
                    inputSize.Width,
                    inputSize.Height,
                    logicalOutputSize.Width,
                    logicalOutputSize.Height,
                    processed.Width,
                    processed.Height)
                : AppLocalizer.Format(
                    "CaptureOutputDimensions",
                    inputSize.Width,
                    inputSize.Height,
                    processed.Width,
                    processed.Height);
            string sourceDescription = captureWindow
                ? AppLocalizer.Format("SourceWindow", selectedWindow!.Title)
                : AppLocalizer.Get("SourceDesktop");
            _statusLabel.Text = AppLocalizer.Format(
                "CaptureStatus",
                sourceDescription,
                GetColorModeDescription(settings),
                processed.Width,
                processed.Height);
            _frameTimeLabel.Text = $"{stopwatch.ElapsedMilliseconds} ms";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = exception.Message;
            if (_recorder.IsRecording)
            {
                await StopRecordingAsync();
            }
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_latestOutputFrame is null)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Get("WaitForFirstFrame"),
                AppLocalizer.Get("Information"));
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = AppLocalizer.Get("SaveQuantizedVideo"),
            Filter = AppLocalizer.Get("Mp4VideoFilter"),
            DefaultExt = "mp4",
            AddExtension = true,
            FileName = $"quantized-recording-{DateTime.Now:yyyyMMdd-HHmmss}.mp4",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            Size size = _latestOutputFrame.Size;
            var options = new RecordingOptions(
                dialog.FileName,
                size.Width,
                size.Height,
                GetSelectedFrameRate(),
                _captureSystemAudioCheckBox.Checked);
            await _recorder.StartAsync(options);
            _recorder.SubmitFrame(_latestOutputFrame);

            SetRecordingControlsEnabled(false);
            _recordButton.Text = AppLocalizer.Get("StopRecording");
            _recordButton.BackColor = Color.FromArgb(130, 30, 30);
            _recordingStatusTimer.Start();
            _recordingTimeLabel.Text = "● REC 00:00:00";
            _statusLabel.Text = AppLocalizer.Format("RecordingPath", dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Format("CannotStartRecording", exception.Message),
                AppLocalizer.Get("RecordingError"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!_recorder.IsRecording)
        {
            return;
        }

        _recordButton.Enabled = false;
        _statusLabel.Text = AppLocalizer.Get("FinalizingVideo");

        try
        {
            await _recorder.StopAsync();
            _statusLabel.Text = AppLocalizer.Get("RecordingCompleted");
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Format("StopRecordingError", exception.Message),
                AppLocalizer.Get("RecordingError"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _statusLabel.Text = AppLocalizer.Get("RecordingFailed");
        }
        finally
        {
            _recordingStatusTimer.Stop();
            _recordingTimeLabel.Text = string.Empty;
            _recordButton.Text = AppLocalizer.Get("StartRecording");
            _recordButton.BackColor = Color.FromArgb(215, 55, 55);
            _recordButton.Enabled = true;
            SetRecordingControlsEnabled(true);
        }
    }

    private void SetRecordingControlsEnabled(bool enabled)
    {
        _sourceComboBox.Enabled = enabled;
        bool windowMode = _sourceComboBox.SelectedIndex == 1;
        _windowComboBox.Enabled = enabled && windowMode;
        _refreshWindowsButton.Enabled = enabled && windowMode;
        _inputScaleModeComboBox.Enabled = enabled;
        _outputSizeModeComboBox.Enabled = enabled;
        _frameRateComboBox.Enabled = enabled;
        _captureSystemAudioCheckBox.Enabled = enabled;
        _pixelSpacingCheckBox.Enabled = enabled;
        _importConfigurationButton.Enabled = enabled;

        if (enabled)
        {
            UpdateOutputControls();
            UpdatePixelSpacingControls();
        }
        else
        {
            _outputWidthInput.Enabled = false;
            _outputHeightInput.Enabled = false;
            _outputMultiplierComboBox.Enabled = false;
            _pixelSpacingWidthInput.Enabled = false;
        }
    }

    private void UpdateOutputControls()
    {
        if (_outputSizeModeComboBox.SelectedIndex < 0)
        {
            return;
        }

        OutputSizeMode mode = (OutputSizeMode)_outputSizeModeComboBox.SelectedIndex;
        bool controlsEnabled = !_recorder.IsRecording;
        _outputWidthInput.Enabled = controlsEnabled && mode == OutputSizeMode.Custom;
        _outputHeightInput.Enabled = controlsEnabled && mode == OutputSizeMode.Custom;
        _outputMultiplierComboBox.Enabled =
            controlsEnabled && mode == OutputSizeMode.InputMultiplier;
        _resolvedOutputLabel.Text = mode switch
        {
            OutputSizeMode.Custom =>
                AppLocalizer.Format(
                    "CurrentOutput",
                    _outputWidthInput.Value.ToString("0"),
                    _outputHeightInput.Value.ToString("0")),
            OutputSizeMode.FollowInput => AppLocalizer.Get("CurrentOutputFollow"),
            OutputSizeMode.InputMultiplier =>
                AppLocalizer.Format(
                    "CurrentOutputMultiplier",
                    _outputMultiplierComboBox.SelectedItem ?? string.Empty),
            _ => string.Empty,
        };
    }

    private void UpdateColorModeControls()
    {
        if (_colorModeComboBox.SelectedIndex < 0)
        {
            return;
        }

        ColorQuantizationMode mode =
            (ColorQuantizationMode)_colorModeComboBox.SelectedIndex;
        _grayscaleOptionsPanel.Visible = mode == ColorQuantizationMode.Grayscale;
        _rgbUniformOptionsPanel.Visible = mode == ColorQuantizationMode.RgbUniform;
        _rgbPerChannelOptionsPanel.Visible =
            mode == ColorQuantizationMode.RgbPerChannel;
        _paletteOptionsPanel.Visible =
            mode == ColorQuantizationMode.CustomPalette;
        _thresholdOptionsPanel.Visible =
            mode == ColorQuantizationMode.Grayscale &&
            _grayscaleBitsInput.Value == 1;
    }

    private void EditCustomPalette()
    {
        using var dialog = new PaletteEditorForm(_customPalette);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _customPalette.Clear();
        _customPalette.AddRange(dialog.Palette);
        _palettePreview.Colors = _customPalette.ToArray();
        _paletteCountLabel.Text = AppLocalizer.Format("ColorCount", _customPalette.Count);
        RefreshPixelSpacingColors();
    }

    private void UpdatePixelSpacingControls()
    {
        bool enabled = _pixelSpacingCheckBox.Checked;
        _pixelSpacingOptionsPanel.Enabled = enabled;
        _pixelSpacingWidthInput.Enabled = enabled && !_recorder.IsRecording;
    }

    private void UpdateOriginalPreviewMode()
    {
        bool showOriginal = _showOriginalPreviewCheckBox.Checked;
        _originalPreviewContainer.Visible = showOriginal;
        _previewLayout.ColumnStyles[0].SizeType = showOriginal
            ? SizeType.Percent
            : SizeType.Absolute;
        _previewLayout.ColumnStyles[0].Width = showOriginal ? 50 : 0;
        _previewLayout.ColumnStyles[1].SizeType = SizeType.Percent;
        _previewLayout.ColumnStyles[1].Width = showOriginal ? 50 : 100;

        if (!showOriginal)
        {
            _originalPreview.PreviewImage = null;
        }
    }

    private void RefreshPixelSpacingColors()
    {
        int previousArgb =
            _pixelSpacingColorComboBox.SelectedItem is Color previousColor
                ? previousColor.ToArgb()
                : Color.Black.ToArgb();

        _pixelSpacingColorComboBox.BeginUpdate();
        try
        {
            _pixelSpacingColorComboBox.Items.Clear();
            foreach (Color color in _customPalette)
            {
                _pixelSpacingColorComboBox.Items.Add(color);
            }
        }
        finally
        {
            _pixelSpacingColorComboBox.EndUpdate();
        }

        int selectedIndex = _customPalette.FindIndex(
            color => color.ToArgb() == previousArgb);
        _pixelSpacingColorComboBox.SelectedIndex =
            selectedIndex >= 0 ? selectedIndex : 0;
    }

    private void UpdateCustomOutputLabel()
    {
        if (_outputSizeModeComboBox.SelectedIndex == (int)OutputSizeMode.Custom)
        {
            _resolvedOutputLabel.Text =
                AppLocalizer.Format(
                    "CurrentOutput",
                    _outputWidthInput.Value.ToString("0"),
                    _outputHeightInput.Value.ToString("0"));
        }
    }

    private void RefreshWindowList()
    {
        nint previousHandle = (_windowComboBox.SelectedItem as WindowInfo)?.Handle ?? nint.Zero;
        IReadOnlyList<WindowInfo> windows = _captureService.GetCapturableWindows();

        _windowComboBox.BeginUpdate();
        try
        {
            _windowComboBox.Items.Clear();
            foreach (WindowInfo window in windows)
            {
                _windowComboBox.Items.Add(window);
            }

            int previousIndex = windows
                .Select((window, index) => new { window.Handle, Index = index })
                .FirstOrDefault(item => item.Handle == previousHandle)?.Index ?? -1;

            if (previousIndex >= 0)
            {
                _windowComboBox.SelectedIndex = previousIndex;
            }
            else if (_windowComboBox.Items.Count > 0)
            {
                _windowComboBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _windowComboBox.EndUpdate();
        }

        if (_sourceComboBox.SelectedIndex == 1)
        {
            _statusLabel.Text = AppLocalizer.Format("FoundWindows", windows.Count);
        }
    }

    private void ExportConfiguration()
    {
        CommitDimensionInput(_outputWidthInput);
        CommitDimensionInput(_outputHeightInput);

        using var dialog = new SaveFileDialog
        {
            Title = AppLocalizer.Get("ExportConfigurationTitle"),
            Filter = AppLocalizer.Get("ConfigurationSaveFilter"),
            DefaultExt = "dbconfig.json",
            AddExtension = true,
            FileName = $"desktop-binarizer-{DateTime.Now:yyyyMMdd-HHmmss}.dbconfig.json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ConfigurationFile.Save(dialog.FileName, CreateConfiguration());
            _statusLabel.Text = AppLocalizer.Format("ConfigurationExported", dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Format("CannotExportConfiguration", exception.Message),
                AppLocalizer.Get("ConfigurationExportError"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ImportConfiguration()
    {
        if (_recorder.IsRecording)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Get("StopBeforeImport"),
                AppLocalizer.Get("Information"));
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = AppLocalizer.Get("ImportConfigurationTitle"),
            Filter = AppLocalizer.Get("ConfigurationOpenFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            BinarizerConfiguration configuration =
                ConfigurationFile.Load(dialog.FileName);
            ApplyConfiguration(configuration);
            _statusLabel.Text = AppLocalizer.Format("ConfigurationImported", dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Format("CannotImportConfiguration", exception.Message),
                AppLocalizer.Get("ConfigurationImportError"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private BinarizerConfiguration CreateConfiguration()
    {
        Color spacingColor = _pixelSpacingColorComboBox.SelectedItem is Color color
            ? color
            : _customPalette[0];

        return new BinarizerConfiguration
        {
            CaptureSource = _sourceComboBox.SelectedIndex == 1
                ? CaptureSourceMode.Window
                : CaptureSourceMode.Desktop,
            WindowTitle = (_windowComboBox.SelectedItem as WindowInfo)?.Title,
            InputScaleMode = (InputScaleMode)_inputScaleModeComboBox.SelectedIndex,
            OutputSizeMode = (OutputSizeMode)_outputSizeModeComboBox.SelectedIndex,
            OutputWidth = (int)_outputWidthInput.Value,
            OutputHeight = (int)_outputHeightInput.Value,
            OutputMultiplier =
                OutputMultipliers[Math.Max(0, _outputMultiplierComboBox.SelectedIndex)],
            PreviewScale = _preview.DisplayScale,
            ShowOriginalPreview = _showOriginalPreviewCheckBox.Checked,
            ColorMode = (ColorQuantizationMode)_colorModeComboBox.SelectedIndex,
            GrayscaleBits = (int)_grayscaleBitsInput.Value,
            Threshold = _thresholdTrackBar.Value,
            RgbBits = (int)_rgbBitsInput.Value,
            RedBits = (int)_redBitsInput.Value,
            GreenBits = (int)_greenBitsInput.Value,
            BlueBits = (int)_blueBitsInput.Value,
            Palette = _customPalette.Select(ConfigurationFile.ColorToHex).ToArray(),
            DitherAlgorithm = (DitherAlgorithm)_ditherAlgorithmComboBox.SelectedIndex,
            Invert = _invertCheckBox.Checked,
            EnablePixelSpacing = _pixelSpacingCheckBox.Checked,
            PixelSpacingWidth = (int)_pixelSpacingWidthInput.Value,
            PixelSpacingColor = ConfigurationFile.ColorToHex(spacingColor),
            FrameRate = GetSelectedFrameRate(),
            CaptureSystemAudio = _captureSystemAudioCheckBox.Checked,
        };
    }

    private void ApplyConfiguration(BinarizerConfiguration configuration)
    {
        ConfigurationFile.Validate(configuration);

        _inputScaleModeComboBox.SelectedIndex = (int)configuration.InputScaleMode;
        _outputWidthInput.Value = configuration.OutputWidth;
        _outputHeightInput.Value = configuration.OutputHeight;
        _outputMultiplierComboBox.SelectedIndex = FindDoubleIndex(
            OutputMultipliers,
            configuration.OutputMultiplier);
        _outputSizeModeComboBox.SelectedIndex = (int)configuration.OutputSizeMode;
        ApplyPreviewScale(configuration.PreviewScale);
        _showOriginalPreviewCheckBox.Checked = configuration.ShowOriginalPreview;

        _grayscaleBitsInput.Value = configuration.GrayscaleBits;
        _thresholdTrackBar.Value = configuration.Threshold;
        _rgbBitsInput.Value = configuration.RgbBits;
        _redBitsInput.Value = configuration.RedBits;
        _greenBitsInput.Value = configuration.GreenBits;
        _blueBitsInput.Value = configuration.BlueBits;
        _colorModeComboBox.SelectedIndex = (int)configuration.ColorMode;

        _customPalette.Clear();
        _customPalette.AddRange(configuration.Palette.Select(ConfigurationFile.ParseColor));
        _palettePreview.Colors = _customPalette.ToArray();
        _paletteCountLabel.Text = AppLocalizer.Format("ColorCount", _customPalette.Count);
        RefreshPixelSpacingColors();
        Color importedSpacingColor =
            ConfigurationFile.ParseColor(configuration.PixelSpacingColor);
        _pixelSpacingColorComboBox.SelectedIndex = _customPalette.FindIndex(
            color => color.ToArgb() == importedSpacingColor.ToArgb());

        _ditherAlgorithmComboBox.SelectedIndex =
            (int)configuration.DitherAlgorithm;
        _invertCheckBox.Checked = configuration.Invert;
        _pixelSpacingWidthInput.Value = configuration.PixelSpacingWidth;
        _pixelSpacingCheckBox.Checked = configuration.EnablePixelSpacing;
        _frameRateComboBox.SelectedIndex =
            Array.IndexOf(FrameRates, configuration.FrameRate);
        _captureSystemAudioCheckBox.Checked = configuration.CaptureSystemAudio;

        _sourceComboBox.SelectedIndex =
            configuration.CaptureSource == CaptureSourceMode.Window ? 1 : 0;
        if (configuration.CaptureSource == CaptureSourceMode.Window &&
            !string.IsNullOrWhiteSpace(configuration.WindowTitle))
        {
            int windowIndex = _windowComboBox.Items
                .Cast<WindowInfo>()
                .Select((window, index) => new { window, index })
                .FirstOrDefault(item =>
                    string.Equals(
                        item.window.Title,
                        configuration.WindowTitle,
                        StringComparison.Ordinal))
                ?.index ?? -1;
            if (windowIndex >= 0)
            {
                _windowComboBox.SelectedIndex = windowIndex;
            }
        }

        UpdateOutputControls();
        UpdateColorModeControls();
        UpdatePixelSpacingControls();
    }

    private void ApplyPreviewScale(double scale)
    {
        _previewScaleComboBox.SelectedIndex =
            FindDoubleIndex(PreviewScales, scale);
        _preview.DisplayScale = scale;
        _originalPreview.DisplayScale = scale;
    }

    private Size GetEffectiveCustomOutputSize()
    {
        return new Size(
            Math.Max(16, (int)_outputWidthInput.Value),
            Math.Max(16, (int)_outputHeightInput.Value));
    }

    private static void CommitDimensionInput(NumericUpDown input)
    {
        if (input.Value < 16)
        {
            input.Value = 16;
        }
    }

    private static int FindDoubleIndex(IReadOnlyList<double> values, double value)
    {
        int index = values
            .Select((item, index) => new { item, index })
            .FirstOrDefault(item => Math.Abs(item.item - value) < 0.000001)
            ?.index ?? -1;
        if (index < 0)
        {
            throw new InvalidDataException(AppLocalizer.Format("UnsupportedValue", value));
        }

        return index;
    }

    private void SaveCurrentFrame()
    {
        if (_latestOutputFrame is null)
        {
            MessageBox.Show(
                this,
                AppLocalizer.Get("NoFrameToSave"),
                AppLocalizer.Get("Information"));
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = AppLocalizer.Get("SaveQuantizedImage"),
            Filter = AppLocalizer.Get("ImageSaveFilter"),
            DefaultExt = "png",
            AddExtension = true,
            FileName = $"quantized-{DateTime.Now:yyyyMMdd-HHmmss}.png",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var copy = new Bitmap(_latestOutputFrame);
        ImageFormat format = dialog.FilterIndex == 2 ? ImageFormat.Bmp : ImageFormat.Png;
        copy.Save(dialog.FileName, format);
        _statusLabel.Text = AppLocalizer.Format("SavedPath", dialog.FileName);
    }

    private async void MainFormOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closeCommitted)
        {
            return;
        }

        _isClosing = true;
        _captureTimer.Stop();
        _recordingStatusTimer.Stop();

        if (_recorder.IsRecording)
        {
            e.Cancel = true;
            await StopRecordingAsync();
            _closeCommitted = true;
            Close();
        }
    }

    private int GetSelectedFrameRate()
    {
        return FrameRates[Math.Max(0, _frameRateComboBox.SelectedIndex)];
    }

    private static string GetColorModeDescription(CaptureSettings settings)
    {
        return settings.ColorMode switch
        {
            ColorQuantizationMode.Grayscale =>
                AppLocalizer.Format(
                    "ColorDescriptionGrayscale",
                    settings.GrayscaleBits),
            ColorQuantizationMode.RgbUniform =>
                AppLocalizer.Format(
                    "ColorDescriptionRgbUniform",
                    settings.RgbBits),
            ColorQuantizationMode.RgbPerChannel =>
                AppLocalizer.Format(
                    "ColorDescriptionRgbPerChannel",
                    settings.RedBits,
                    settings.GreenBits,
                    settings.BlueBits),
            ColorQuantizationMode.CustomPalette =>
                AppLocalizer.Format(
                    "ColorDescriptionPalette",
                    settings.Palette.Count),
            _ => string.Empty,
        };
    }

    private static void DrawColorComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox || e.Index < 0)
        {
            return;
        }

        e.DrawBackground();
        if (comboBox.Items[e.Index] is not Color color)
        {
            return;
        }

        var swatch = new Rectangle(
            e.Bounds.Left + 4,
            e.Bounds.Top + 3,
            30,
            Math.Max(1, e.Bounds.Height - 6));
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, swatch);
        e.Graphics.DrawRectangle(Pens.DimGray, swatch);

        TextRenderer.DrawText(
            e.Graphics,
            $"#{color.R:X2}{color.G:X2}{color.B:X2}",
            e.Font,
            new Point(swatch.Right + 8, e.Bounds.Top + 4),
            e.ForeColor);
        e.DrawFocusRectangle();
    }

    private static int GetFrameInterval(int framesPerSecond)
    {
        return Math.Max(1, (int)Math.Round(1000.0 / framesPerSecond));
    }

    private static void ConfigureComboBox(
        ComboBox comboBox,
        object[] items,
        int selectedIndex)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Items.AddRange(items);
        comboBox.SelectedIndex = selectedIndex;
        comboBox.Dock = DockStyle.Top;
    }

    private static void ConfigureLocalizedComboBox(
        ComboBox comboBox,
        string[] keys,
        int selectedIndex)
    {
        comboBox.Tag = new LocalizedItemsTag(keys);
        ConfigureComboBox(
            comboBox,
            keys.Select(AppLocalizer.Get).Cast<object>().ToArray(),
            selectedIndex);
    }

    private static void ReplaceLocalizedItems(ComboBox comboBox, string[] keys)
    {
        int selectedIndex = comboBox.SelectedIndex;
        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            comboBox.Items.AddRange(
                keys.Select(AppLocalizer.Get).Cast<object>().ToArray());
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = Math.Clamp(
                    selectedIndex,
                    0,
                    comboBox.Items.Count - 1);
            }
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    private static void SetLocalizedText(Control control, string key)
    {
        control.Tag = new LocalizedTextTag(key);
        control.Text = AppLocalizer.Get(key);
    }

    private static Control CreateLabeledControl(string labelKey, Control control)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 6, 0),
        };
        layout.Controls.Add(new Label
        {
            Text = AppLocalizer.Get(labelKey),
            Tag = new LocalizedTextTag(labelKey),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 3),
        });
        layout.Controls.Add(control);
        return layout;
    }

    private static TableLayoutPanel ConfigurePreviewContainer(
        TableLayoutPanel container,
        string titleKey,
        PixelPreview preview)
    {
        container.Dock = DockStyle.Fill;
        container.ColumnCount = 1;
        container.RowCount = 2;
        container.Margin = new Padding(3);
        container.BackColor = Color.White;
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = AppLocalizer.Get(titleKey),
            Tag = new LocalizedTextTag(titleKey),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(8, 7, 8, 7),
        };
        preview.Dock = DockStyle.Fill;
        container.Controls.Add(label, 0, 0);
        container.Controls.Add(preview, 0, 1);
        return container;
    }

    private static TableLayoutPanel CreateTwoColumnLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return layout;
    }

    private static Label CreateSectionLabel(string key)
    {
        return new Label
        {
            Text = AppLocalizer.Get(key),
            Tag = new LocalizedTextTag(key),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
        };
    }

    private static void ConfigureDimensionInput(NumericUpDown input, int value)
    {
        input.Minimum = 1;
        input.Maximum = 8192;
        input.Value = value;
        input.Increment = 16;
        input.Dock = DockStyle.Fill;
        input.TextAlign = HorizontalAlignment.Center;
        input.ThousandsSeparator = true;
    }

    private static void ConfigureBitDepthInput(NumericUpDown input, int value)
    {
        input.Minimum = 1;
        input.Maximum = 8;
        input.Value = value;
        input.Dock = DockStyle.Fill;
        input.TextAlign = HorizontalAlignment.Center;
    }

    private static void ConfigureSingleOptionPanel(
        TableLayoutPanel panel,
        string labelKey,
        Control input,
        string hintKey)
    {
        panel.Dock = DockStyle.Top;
        panel.AutoSize = true;
        panel.ColumnCount = 3;
        panel.Margin = new Padding(0, 6, 0, 0);
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = AppLocalizer.Get(labelKey),
            Tag = new LocalizedTextTag(labelKey),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0),
        }, 0, 0);
        panel.Controls.Add(input, 1, 0);
        panel.Controls.Add(new Label
        {
            Text = AppLocalizer.Get(hintKey),
            Tag = new LocalizedTextTag(hintKey),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 6, 0, 0),
        }, 2, 0);
    }

    private static Panel CreateSpacer(int height)
    {
        return new Panel
        {
            Height = height,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
    }

    private sealed record LocalizedTextTag(string Key);

    private sealed record LocalizedItemsTag(string[] Keys);

    private sealed record CaptureSettings(
        OutputSizeMode OutputSizeMode,
        Size CustomOutputSize,
        double OutputMultiplier,
        InputScaleMode InputScaleMode,
        ColorQuantizationMode ColorMode,
        int GrayscaleBits,
        int Threshold,
        int RgbBits,
        int RedBits,
        int GreenBits,
        int BlueBits,
        IReadOnlyList<Color> Palette,
        DitherAlgorithm DitherAlgorithm,
        bool Invert,
        bool EnablePixelSpacing,
        int PixelSpacingWidth,
        Color PixelSpacingColor,
        bool ShowOriginalPreview);

}
