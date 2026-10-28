namespace DesktopBinarizer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppLocalizer.Language = AppPreferencesFile.LoadOrDefault().Language;
        Application.Run(new MainForm());
    }
}
