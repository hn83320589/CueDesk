using System.Globalization;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Presentation;

/// <summary>Read-only row for the subtitle list; rebuilt whenever the document changes.</summary>
public sealed class CueRowViewModel(Cue cue, int sequence, bool hasIssue)
{
    public Cue Cue { get; } = cue;

    public int Id => Cue.Id;

    public int Sequence { get; } = sequence;

    public string Start => TimeFormat.Format(Cue.StartMs);

    public string End => TimeFormat.Format(Cue.EndMs);

    public string Duration => (Cue.DurationMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>Text on a single line; line breaks are shown as " ⏎ ".</summary>
    public string TextPreview => Cue.Text.Replace("\n", " ⏎ ");

    public bool HasIssue { get; } = hasIssue;
}
