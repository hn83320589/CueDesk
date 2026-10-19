namespace CueDesk.Core.Model;

/// <summary>
/// One subtitle entry. Times are integer milliseconds; <see cref="Text"/> uses "\n" between lines
/// and keeps inline tags (&lt;i&gt;, {\an8}, …) verbatim.
/// The sequence number is not stored: it is the 1-based position in the sorted list.
/// </summary>
public sealed record Cue(int Id, long StartMs, long EndMs, string Text)
{
    public long DurationMs => EndMs - StartMs;

    public string[] Lines => Text.Split('\n');
}
