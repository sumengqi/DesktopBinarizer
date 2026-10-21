namespace DesktopBinarizer.Recording;

public sealed record RecordingOptions(
    string OutputPath,
    int Width,
    int Height,
    int FramesPerSecond,
    bool CaptureSystemAudio);
