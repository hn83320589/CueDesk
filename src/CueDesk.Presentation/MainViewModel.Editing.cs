using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CueDesk.Core.Editing;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Presentation;

public sealed partial class MainViewModel
{
    /// <summary>The single selected cue being edited, or null when zero or several are selected.</summary>
    public Cue? EditingCue => _selectedIds.Count == 1 ? Cues.FirstOrDefault(c => _selectedIds.Contains(c.Id)) : null;

    public bool HasEditingCue => EditingCue is not null;

    public bool HasSelection => _selectedIds.Count > 0;

    public string SelectionText => _selectedIds.Count switch
    {
        0 => "未選取",
        1 => $"已選取第 {CueList.IndexOf(Cues, _selectedIds.First()) + 1} 筆",
        var n => $"已選取 {n} 筆",
    };

    [ObservableProperty]
    private string _editStart = string.Empty;

    [ObservableProperty]
    private string _editEnd = string.Empty;

    [ObservableProperty]
    private string _editText = string.Empty;

    /// <summary>Split point for the selected cue; defaults to its midpoint.</summary>
    [ObservableProperty]
    private string _splitTime = string.Empty;

    private void OnSelectionChanged()
    {
        if (EditingCue is { } cue)
        {
            EditStart = TimeFormat.Format(cue.StartMs);
            EditEnd = TimeFormat.Format(cue.EndMs);
            EditText = cue.Text;
            SplitTime = TimeFormat.Format(cue.StartMs + cue.DurationMs / 2);
        }
        OnPropertyChanged(nameof(EditingCue));
        OnPropertyChanged(nameof(HasEditingCue));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionText));
        UpdateShiftPreview();
    }

    /// <summary>Applies the edit panel (times and text) to the selected cue as one undo step.</summary>
    [RelayCommand]
    private async Task ApplyEditAsync()
    {
        if (EditingCue is not { } cue)
        {
            return;
        }
        if (!TimeFormat.TryParse(EditStart, out var start, out var error) || !TimeFormat.TryParse(EditEnd, out var end, out error))
        {
            await _dialogs.AlertAsync("時間格式錯誤", $"{error}。請使用 00:00:00,000 格式。");
            return;
        }
        var text = CueEditor.NormalizeText(EditText);
        if (start == cue.StartMs && end == cue.EndMs && text == cue.Text)
        {
            return;
        }
        var seq = CueList.IndexOf(Cues, cue.Id) + 1;
        await TryEditAsync(() =>
        {
            var cues = CueEditor.UpdateTimes(Cues, cue.Id, start, end);
            Commit(CueEditor.UpdateText(cues, cue.Id, text), $"編輯第 {seq} 筆");
        });
    }

    [RelayCommand]
    private async Task SplitAsync()
    {
        if (EditingCue is not { } cue)
        {
            await _dialogs.AlertAsync("分割", "請先選取一筆字幕。");
            return;
        }
        if (!TimeFormat.TryParse(SplitTime, out var at, out var error))
        {
            await _dialogs.AlertAsync("時間格式錯誤", error!);
            return;
        }
        var seq = CueList.IndexOf(Cues, cue.Id) + 1;
        await TryEditAsync(() => Commit(CueEditor.Split(Cues, cue.Id, at), $"分割第 {seq} 筆", [cue.Id]));
    }

    /// <summary>Merges two selected adjacent cues, or the single selected cue with the next one.</summary>
    [RelayCommand]
    private async Task MergeAsync()
    {
        var selected = SelectedCues;
        if (selected.Count is 0 or > 2)
        {
            await _dialogs.AlertAsync("合併", "請選取一筆（與下一筆合併）或相鄰的兩筆字幕。");
            return;
        }
        var first = CueList.IndexOf(Cues, selected[0].Id);
        if (selected.Count == 2 && CueList.IndexOf(Cues, selected[1].Id) != first + 1)
        {
            await _dialogs.AlertAsync("合併", "只能合併相鄰的兩筆字幕。");
            return;
        }
        await TryEditAsync(() =>
            Commit(CueEditor.MergeWithNext(Cues, selected[0].Id), $"合併第 {first + 1}、{first + 2} 筆", [selected[0].Id]));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var ids = _selectedIds.ToList();
        await TryEditAsync(() => Commit(CueEditor.Delete(Cues, ids), $"刪除 {ids.Count} 筆", []));
    }

    /// <summary>Inserts an empty cue after the last selected cue (or at the start when nothing is selected).</summary>
    [RelayCommand]
    private async Task InsertAsync()
    {
        int? after = SelectedCues.Count > 0 ? SelectedCues[^1].Id : null;
        await TryEditAsync(() =>
        {
            var (cues, newId) = CueEditor.InsertAfter(Cues, after, Settings.Thresholds.MinGapMs);
            Commit(cues, "插入字幕", [newId]);
            ScrollToCueRequested?.Invoke(newId);
        });
    }
}
