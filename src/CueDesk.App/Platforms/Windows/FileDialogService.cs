using CueDesk.Core.Export;
using CueDesk.Presentation.Services;
using Microsoft.Maui.Platform;
using Windows.Storage.Pickers;

namespace CueDesk.App.Services;

/// <summary>
/// WinRT file pickers. Unpackaged apps must associate each picker with the window handle
/// (InitializeWithWindow), otherwise the picker throws.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    public async Task<string?> PickOpenFileAsync()
    {
        var picker = new FileOpenPicker();
        AttachToWindow(picker);
        picker.FileTypeFilter.Add(".srt");
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, ExportFormat format)
    {
        var picker = new FileSavePicker();
        AttachToWindow(picker);
        var (label, extension) = format switch
        {
            ExportFormat.Srt => ("SRT 字幕", ".srt"),
            ExportFormat.WebVtt => ("WebVTT 字幕", ".vtt"),
            _ => ("純文字逐字稿", ".txt"),
        };
        picker.FileTypeChoices.Add(label, [extension]);
        picker.SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName);
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static void AttachToWindow(object picker)
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow
            ?? throw new InvalidOperationException("The main window is not available for the file picker.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, window.WindowHandle);
    }
}
