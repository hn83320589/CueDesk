namespace CueDesk.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        // Record crashes so they can be diagnosed; the exception is not swallowed.
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write(e.ExceptionObject as Exception);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

internal static class CrashLog
{
    public static void Write(Exception? exception)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CueDesk");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"[{DateTime.Now:O}] {exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never mask the original crash.
        }
    }
}
