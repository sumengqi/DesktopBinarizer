namespace DesktopBinarizer.Capture;

public sealed record WindowInfo(nint Handle, string Title, string ProcessName)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(ProcessName)
            ? Title
            : $"{Title}  [{ProcessName}]";
    }
}
