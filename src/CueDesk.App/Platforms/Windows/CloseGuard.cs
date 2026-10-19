using CueDesk.Presentation;

namespace CueDesk.App.Services;

/// <summary>
/// Intercepts the window close button so unsaved changes can be saved or discarded first.
/// MAUI has no cancellable close event on Windows, so this hooks WinUI's AppWindow.Closing.
/// </summary>
internal static class CloseGuard
{
    private static bool _closeConfirmed;
    private static bool _asking;

    public static void Attach(Microsoft.UI.Xaml.Window window)
    {
        window.AppWindow.Closing += async (_, args) =>
        {
            if (_closeConfirmed)
            {
                return;
            }
            // Cancel synchronously; the decision is made after the (async) dialog.
            args.Cancel = true;
            if (_asking)
            {
                return;
            }
            _asking = true;
            try
            {
                var viewModel = IPlatformApplication.Current!.Services.GetRequiredService<MainViewModel>();
                if (await viewModel.ConfirmDiscardChangesAsync())
                {
                    _closeConfirmed = true;
                    Application.Current?.Quit();
                }
            }
            finally
            {
                _asking = false;
            }
        };
    }
}
