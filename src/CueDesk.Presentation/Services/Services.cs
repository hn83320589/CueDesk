using CueDesk.Core.Export;

namespace CueDesk.Presentation.Services;

public enum SaveChoice
{
    Save,
    Discard,
    Cancel,
}

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept = "確定", string cancel = "取消");

    /// <summary>Asks what to do with unsaved changes: save, discard, or cancel the pending action.</summary>
    Task<SaveChoice> AskSaveChangesAsync(string message);
}

public interface IFileDialogService
{
    Task<string?> PickOpenFileAsync();

    Task<string?> PickSaveFileAsync(string suggestedFileName, ExportFormat format);
}

public interface IFileSystem
{
    bool Exists(string path);

    long GetLength(string path);

    byte[] ReadAllBytes(string path);

    void WriteAllBytes(string path, byte[] bytes);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public long GetLength(string path) => new FileInfo(path).Length;

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
}
