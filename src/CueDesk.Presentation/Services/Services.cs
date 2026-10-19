using CueDesk.Core.Export;
using CueDesk.Core.IO;

namespace CueDesk.Presentation.Services;

public enum SaveChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Answer when saving over a source file whose malformed blocks were not loaded.</summary>
public enum OverwriteChoice
{
    SaveAs,
    Overwrite,
    Cancel,
}

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept = "確定", string cancel = "取消");

    /// <summary>Asks what to do with unsaved changes: save, discard, or cancel the pending action.</summary>
    Task<SaveChoice> AskSaveChangesAsync(string message);

    /// <summary>Warns that overwriting will permanently drop blocks that could not be loaded.</summary>
    Task<OverwriteChoice> AskOverwriteDroppingBlocksAsync(string message);
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

    /// <summary>Writes via a temp file and atomic replace; optionally keeps the old content as &lt;path&gt;.bak.</summary>
    void WriteAllBytesSafely(string path, byte[] bytes, bool keepBackup);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public long GetLength(string path) => new FileInfo(path).Length;

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytesSafely(string path, byte[] bytes, bool keepBackup) => SafeFileWriter.Write(path, bytes, keepBackup);
}
