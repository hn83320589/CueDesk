using CueDesk.Presentation.Services;

namespace CueDesk.App.Services;

/// <summary>MAUI dialogs shown on the main window's page.</summary>
public sealed class DialogService : IDialogService
{
    private const string SaveLabel = "儲存";
    private const string DiscardLabel = "不儲存";
    private const string CancelLabel = "取消";

    private static Page CurrentPage =>
        Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No window is open to show a dialog on.");

    public Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlert(title, message, "確定"));

    public Task<bool> ConfirmAsync(string title, string message, string accept = "確定", string cancel = "取消") =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlert(title, message, accept, cancel));

    public async Task<SaveChoice> AskSaveChangesAsync(string message)
    {
        var answer = await MainThread.InvokeOnMainThreadAsync(() =>
            CurrentPage.DisplayActionSheet(message, CancelLabel, null, SaveLabel, DiscardLabel));
        return answer switch
        {
            SaveLabel => SaveChoice.Save,
            DiscardLabel => SaveChoice.Discard,
            _ => SaveChoice.Cancel,
        };
    }
}
