using CueDesk.App.Services;
using CueDesk.Core.Settings;
using CueDesk.Presentation;
using CueDesk.Presentation.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace CueDesk.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IFileDialogService, FileDialogService>();
        builder.Services.AddSingleton<CueDesk.Presentation.Services.IFileSystem, PhysicalFileSystem>();
        builder.Services.AddSingleton<ISettingsStore>(_ => new SettingsStore(SettingsStore.DefaultPath));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows =>
            windows.OnWindowCreated(window => CloseGuard.Attach(window))));
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
