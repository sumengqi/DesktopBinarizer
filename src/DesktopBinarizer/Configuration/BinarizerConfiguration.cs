using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopBinarizer.Imaging;

namespace DesktopBinarizer.Configuration;

public enum CaptureSourceMode
{
    Desktop,
    Window,
}

public sealed record BinarizerConfiguration
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public CaptureSourceMode CaptureSource { get; init; } = CaptureSourceMode.Desktop;

    public string? WindowTitle { get; init; }

    public InputScaleMode InputScaleMode { get; init; } = InputScaleMode.Proportional;

    public OutputSizeMode OutputSizeMode { get; init; } = OutputSizeMode.Custom;

    public int OutputWidth { get; init; } = 400;

    public int OutputHeight { get; init; } = 300;

    public double OutputMultiplier { get; init; } = 1;

    public double PreviewScale { get; init; }

    public bool ShowOriginalPreview { get; init; }

    public ColorQuantizationMode ColorMode { get; init; } =
        ColorQuantizationMode.Grayscale;

    public int GrayscaleBits { get; init; } = 1;

    public int Threshold { get; init; } = 128;

    public int RgbBits { get; init; } = 4;

    public int RedBits { get; init; } = 4;

    public int GreenBits { get; init; } = 4;

    public int BlueBits { get; init; } = 4;

    public string[] Palette { get; init; } = ["#000000", "#FFFFFF"];

    public DitherAlgorithm DitherAlgorithm { get; init; } =
        DitherAlgorithm.FloydSteinberg;

    public bool Invert { get; init; }

    public bool EnablePixelSpacing { get; init; }

    public int PixelSpacingWidth { get; init; } = 1;

    public string PixelSpacingColor { get; init; } = "#000000";

    public int FrameRate { get; init; } = 30;

    public bool CaptureSystemAudio { get; init; } = true;
}

public static class ConfigurationFile
{
    private static readonly double[] SupportedMultipliers =
        [0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4];
    private static readonly double[] SupportedPreviewScales =
        [0, 0.125, 0.25, 1.0 / 3.0, 0.5, 1, 2, 3, 4, 8];
    private static readonly int[] SupportedFrameRates =
        [5, 10, 15, 20, 24, 30, 45, 60, 90, 120, 144, 165, 240];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void Save(string path, BinarizerConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Validate(configuration);
        File.WriteAllText(path, Serialize(configuration));
    }

    public static BinarizerConfiguration Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(path));
    }

    public static string Serialize(BinarizerConfiguration configuration)
    {
        Validate(configuration);
        return JsonSerializer.Serialize(configuration, JsonOptions);
    }

    public static BinarizerConfiguration Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        BinarizerConfiguration configuration =
            JsonSerializer.Deserialize<BinarizerConfiguration>(json, JsonOptions)
            ?? throw new InvalidDataException(AppLocalizer.Get("ConfigurationEmpty"));
        Validate(configuration);
        return configuration;
    }

    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static Color ParseColor(string value)
    {
        if (value is null ||
            value.Length != 7 ||
            value[0] != '#' ||
            !int.TryParse(
                value.AsSpan(1),
                System.Globalization.NumberStyles.HexNumber,
                provider: null,
                out int rgb))
        {
            throw new InvalidDataException(
                AppLocalizer.Format("InvalidHexColor", value ?? string.Empty));
        }

        return Color.FromArgb(
            (rgb >> 16) & 0xFF,
            (rgb >> 8) & 0xFF,
            rgb & 0xFF);
    }

    public static void Validate(BinarizerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Version != BinarizerConfiguration.CurrentVersion)
        {
            throw new InvalidDataException(AppLocalizer.Format(
                "UnsupportedConfigurationVersion",
                configuration.Version,
                BinarizerConfiguration.CurrentVersion));
        }

        ValidateEnum(configuration.CaptureSource, nameof(configuration.CaptureSource));
        ValidateEnum(configuration.InputScaleMode, nameof(configuration.InputScaleMode));
        ValidateEnum(configuration.OutputSizeMode, nameof(configuration.OutputSizeMode));
        ValidateEnum(configuration.ColorMode, nameof(configuration.ColorMode));
        ValidateEnum(configuration.DitherAlgorithm, nameof(configuration.DitherAlgorithm));
        ValidateRange(
            configuration.OutputWidth,
            16,
            8192,
            AppLocalizer.Get("OutputWidth"));
        ValidateRange(
            configuration.OutputHeight,
            16,
            8192,
            AppLocalizer.Get("OutputHeight"));
        ValidateSupported(
            configuration.OutputMultiplier,
            SupportedMultipliers,
            AppLocalizer.Get("OutputMultiplierName"));
        ValidateSupported(
            configuration.PreviewScale,
            SupportedPreviewScales,
            AppLocalizer.Get("PreviewScaleName"));
        ValidateRange(configuration.GrayscaleBits, 1, 8, AppLocalizer.Get("GrayscaleDepthName"));
        ValidateRange(configuration.RgbBits, 1, 8, AppLocalizer.Get("RgbDepthName"));
        ValidateRange(configuration.RedBits, 1, 8, AppLocalizer.Get("RedDepthName"));
        ValidateRange(configuration.GreenBits, 1, 8, AppLocalizer.Get("GreenDepthName"));
        ValidateRange(configuration.BlueBits, 1, 8, AppLocalizer.Get("BlueDepthName"));
        ValidateRange(configuration.Threshold, 0, 255, AppLocalizer.Get("ThresholdName"));
        ValidateRange(configuration.PixelSpacingWidth, 1, 32, AppLocalizer.Get("PixelSpacingWidthName"));

        if (!SupportedFrameRates.Contains(configuration.FrameRate))
        {
            throw new InvalidDataException(
                AppLocalizer.Format("UnsupportedFrameRate", configuration.FrameRate));
        }

        if (configuration.Palette is null ||
            configuration.Palette.Length is < 2 or > 256)
        {
            throw new InvalidDataException(AppLocalizer.Get("PaletteRange"));
        }

        Color[] palette = configuration.Palette.Select(ParseColor).ToArray();
        Color spacingColor = ParseColor(configuration.PixelSpacingColor);
        if (!palette.Any(color => color.ToArgb() == spacingColor.ToArgb()))
        {
            throw new InvalidDataException(AppLocalizer.Get("SpacingColorInPalette"));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void ValidateEnum<T>(T value, string name)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new InvalidDataException(AppLocalizer.Format("UnsupportedEnum", name));
        }
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                AppLocalizer.Format("RangeError", name, minimum, maximum));
        }
    }

    private static void ValidateSupported(
        double value,
        IReadOnlyList<double> supportedValues,
        string name)
    {
        if (!supportedValues.Any(item => Math.Abs(item - value) < 0.000001))
        {
            throw new InvalidDataException(
                AppLocalizer.Format("UnsupportedSetting", name, value));
        }
    }
}
