using CueDesk.Core.Export;
using CueDesk.Core.Settings;
using CueDesk.Presentation;
using CueDesk.Presentation.Services;

namespace CueDesk.Presentation.Tests;

public sealed class FakeDialogs : IDialogService
{
    public List<(string Title, string Message)> Alerts { get; } = [];

    public List<string> Confirms { get; } = [];

    public bool ConfirmAnswer { get; set; } = true;

    public Queue<SaveChoice> SaveChoices { get; } = new();

    public int SaveQuestions { get; private set; }

    public Task AlertAsync(string title, string message)
    {
        Alerts.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept = "確定", string cancel = "取消")
    {
        Confirms.Add(message);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<SaveChoice> AskSaveChangesAsync(string message)
    {
        SaveQuestions++;
        return Task.FromResult(SaveChoices.Count > 0 ? SaveChoices.Dequeue() : SaveChoice.Cancel);
    }
}

public sealed class FakeFileDialogs : IFileDialogService
{
    public Queue<string?> OpenResults { get; } = new();

    public Queue<string?> SaveResults { get; } = new();

    public Task<string?> PickOpenFileAsync() => Task.FromResult(OpenResults.Count > 0 ? OpenResults.Dequeue() : null);

    public Task<string?> PickSaveFileAsync(string suggestedFileName, ExportFormat format) =>
        Task.FromResult(SaveResults.Count > 0 ? SaveResults.Dequeue() : null);
}

public sealed class InMemoryFileSystem : IFileSystem
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Exists(string path) => Files.ContainsKey(path);

    public long GetLength(string path) => Files.TryGetValue(path, out var b) ? b.Length : throw new FileNotFoundException(path);

    public byte[] ReadAllBytes(string path) => Files.TryGetValue(path, out var b) ? b : throw new FileNotFoundException(path);

    public void WriteAllBytes(string path, byte[] bytes) => Files[path] = bytes;
}

public sealed class InMemorySettingsStore(AppSettings? initial = null, string? warning = null) : ISettingsStore
{
    public AppSettings? Saved { get; private set; }

    public int SaveCount { get; private set; }

    public SettingsLoadResult Load() => new(Saved ?? initial ?? AppSettings.Default, warning);

    public void Save(AppSettings settings)
    {
        Saved = settings;
        SaveCount++;
    }
}

/// <summary>Bundles a view model with its fakes.</summary>
public sealed class Harness
{
    public FakeDialogs Dialogs { get; } = new();

    public FakeFileDialogs FileDialogs { get; } = new();

    public InMemoryFileSystem Files { get; } = new();

    public InMemorySettingsStore Settings { get; }

    public MainViewModel Vm { get; }

    public Harness(AppSettings? settings = null, string? settingsWarning = null)
    {
        Settings = new InMemorySettingsStore(settings, settingsWarning);
        Vm = new MainViewModel(Dialogs, FileDialogs, Files, Settings);
    }

    public const string ThreeCues =
        "1\r\n00:00:01,000 --> 00:00:02,000\r\nfirst\r\n\r\n" +
        "2\r\n00:00:03,000 --> 00:00:05,000\r\nsecond\r\n\r\n" +
        "3\r\n00:00:08,000 --> 00:00:09,000\r\nthird\r\n";

    /// <summary>Puts a file in the fake file system and opens it through the Open command.</summary>
    public async Task OpenAsync(string content = ThreeCues, string path = @"C:\subs\demo.srt", byte[]? bytes = null)
    {
        Files.Files[path] = bytes ?? new System.Text.UTF8Encoding(false).GetBytes(content);
        FileDialogs.OpenResults.Enqueue(path);
        await Vm.OpenCommand.ExecuteAsync(null);
    }

    public void Select(params int[] sequences) => Vm.SetSelection(sequences.Select(s => Vm.Cues[s - 1].Id));
}
