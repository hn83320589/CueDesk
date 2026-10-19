using CueDesk.Core.IO;

namespace CueDesk.Core.Tests.IO;

public sealed class SafeFileWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CueDeskTests", Guid.NewGuid().ToString("N"));

    public SafeFileWriterTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string P(string name) => Path.Combine(_dir, name);

    [Fact]
    public void Writes_new_file_without_backup()
    {
        SafeFileWriter.Write(P("a.srt"), [1, 2, 3], keepBackup: true);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(P("a.srt")));
        Assert.False(File.Exists(P("a.srt.bak")));
    }

    [Fact]
    public void Replacing_keeps_previous_content_as_bak()
    {
        File.WriteAllBytes(P("a.srt"), [9, 9]);
        SafeFileWriter.Write(P("a.srt"), [1, 2, 3], keepBackup: true);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(P("a.srt")));
        Assert.Equal([9, 9], File.ReadAllBytes(P("a.srt.bak")));
    }

    [Fact]
    public void Replacing_without_backup_leaves_no_bak()
    {
        File.WriteAllBytes(P("a.srt"), [9, 9]);
        SafeFileWriter.Write(P("a.srt"), [1], keepBackup: false);
        Assert.Equal([1], File.ReadAllBytes(P("a.srt")));
        Assert.False(File.Exists(P("a.srt.bak")));
    }

    [Fact]
    public void Failure_while_writing_leaves_original_untouched_and_no_temp_file()
    {
        File.WriteAllBytes(P("a.srt"), [9, 9]);

        var ex = Assert.Throws<IOException>(() => SafeFileWriter.Write(P("a.srt"), stream =>
        {
            stream.Write([1, 2, 3]);
            throw new IOException("磁碟已滿（模擬）");
        }, keepBackup: true));

        Assert.Contains("模擬", ex.Message);
        Assert.Equal([9, 9], File.ReadAllBytes(P("a.srt")));
        Assert.Equal(["a.srt"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }
}
