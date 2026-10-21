namespace DesktopBinarizer.Imaging;

public sealed record ImageProcessingOptions(
    int OutputWidth = 400,
    int OutputHeight = 300,
    ColorQuantizationMode ColorMode = ColorQuantizationMode.Grayscale,
    int GrayscaleBits = 1,
    int Threshold = 128,
    int RgbBits = 4,
    int RedBits = 4,
    int GreenBits = 4,
    int BlueBits = 4,
    IReadOnlyList<Color>? Palette = null,
    DitherAlgorithm DitherAlgorithm = DitherAlgorithm.FloydSteinberg,
    bool Invert = false,
    InputScaleMode InputScaleMode = InputScaleMode.Proportional,
    bool EnablePixelSpacing = false,
    int PixelSpacingWidth = 1,
    Color? PixelSpacingColor = null);
