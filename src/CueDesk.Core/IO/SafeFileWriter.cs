namespace CueDesk.Core.IO;

/// <summary>
/// Writes a file so that a crash, full disk or power loss never leaves a truncated target:
/// content goes to a temp file in the same folder (flushed to disk), which then replaces the target.
/// Optionally the previous content is kept as "&lt;name&gt;.bak".
/// </summary>
public static class SafeFileWriter
{
    public static void Write(string path, byte[] bytes, bool keepBackup) =>
        Write(path, stream => stream.Write(bytes), keepBackup);

    public static void Write(string path, Action<Stream> writeContent, bool keepBackup)
    {
        var fullPath = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writeContent(stream);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temp, fullPath, keepBackup ? fullPath + ".bak" : null);
            }
            else
            {
                File.Move(temp, fullPath);
            }
        }
        finally
        {
            // Only reached with the temp file still present when something failed; the original is untouched.
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
