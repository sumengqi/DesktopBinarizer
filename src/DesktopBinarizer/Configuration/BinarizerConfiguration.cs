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
            ?? throw new InvalidDataException("配置文件内容为空。");
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
            throw new InvalidDataException($"颜色“{value}”不是有效的 #RRGGBB 格式。");
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
            throw new InvalidDataException(
                $"不支持配置版本 {configuration.Version}，当前版本为 " +
                $"{BinarizerConfiguration.CurrentVersion}。");
        }

        ValidateEnum(configuration.CaptureSource, nameof(configuration.CaptureSource));
        ValidateEnum(configuration.InputScaleMode, nameof(configuration.InputScaleMode));
        ValidateEnum(configuration.OutputSizeMode, nameof(configuration.OutputSizeMode));
        ValidateEnum(configuration.ColorMode, nameof(configuration.ColorMode));
        ValidateEnum(configuration.DitherAlgorithm, nameof(configuration.DitherAlgorithm));
        ValidateRange(configuration.OutputWidth, 16, 8192, "输出宽度");
        ValidateRange(configuration.OutputHeight, 16, 8192, "输出高度");
        ValidateSupported(
            configuration.OutputMultiplier,
            SupportedMultipliers,
            "输出倍数");
        ValidateSupported(
            configuration.PreviewScale,
            SupportedPreviewScales,
            "预览缩放");
        ValidateRange(configuration.GrayscaleBits, 1, 8, "灰度位深");
        ValidateRange(configuration.RgbBits, 1, 8, "RGB 位深");
        ValidateRange(configuration.RedBits, 1, 8, "红色位深");
        ValidateRange(configuration.GreenBits, 1, 8, "绿色位深");
        ValidateRange(configuration.BlueBits, 1, 8, "蓝色位深");
        ValidateRange(configuration.Threshold, 0, 255, "二值化阈值");
        ValidateRange(configuration.PixelSpacingWidth, 1, 32, "像素间距宽度");

        if (!SupportedFrameRates.Contains(configuration.FrameRate))
        {
            throw new InvalidDataException(
                $"帧率 {configuration.FrameRate} 不在支持列表中。");
        }

        if (configuration.Palette is null ||
            configuration.Palette.Length is < 2 or > 256)
        {
            throw new InvalidDataException("调色板必须包含 2–256 种颜色。");
        }

        Color[] palette = configuration.Palette.Select(ParseColor).ToArray();
        Color spacingColor = ParseColor(configuration.PixelSpacingColor);
        if (!palette.Any(color => color.ToArgb() == spacingColor.ToArgb()))
        {
            throw new InvalidDataException("像素间距颜色必须存在于自定义调色板中。");
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
            throw new InvalidDataException($"{name} 包含不支持的值。");
        }
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                $"{name}必须在 {minimum}–{maximum} 范围内。");
        }
    }

    private static void ValidateSupported(
        double value,
        IReadOnlyList<double> supportedValues,
        string name)
    {
        if (!supportedValues.Any(item => Math.Abs(item - value) < 0.000001))
        {
            throw new InvalidDataException($"{name} {value} 不受支持。");
        }
    }
}
