using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CueDesk.Core.Editing;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Presentation;

public sealed partial class MainViewModel
{
    public IReadOnlyList<FrameRate> FrameRates { get; } = FrameRate.All;

    // ---- Shift ----

    [ObservableProperty]
    private string _shiftOffset = "0";

    [ObservableProperty]
    private bool _shiftSelectedOnly;

    [ObservableProperty]
    private string _shiftPreview = string.Empty;

    partial void OnShiftOffsetChanged(string value) => UpdateShiftPreview();

    partial void OnShiftSelectedOnlyChanged(bool value) => UpdateShiftPreview();

    private bool TryGetShift(out long offset, out IReadOnlyCollection<int>? ids)
    {
        ids = ShiftSelectedOnly ? _selectedIds.ToList() : null;
        return long.TryParse(ShiftOffset.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out offset);
    }

    /// <summary>Recomputes the summary shown before applying a shift.</summary>
    private void UpdateShiftPreview()
    {
        if (!TryGetShift(out var offset, out var ids))
        {
            ShiftPreview = "請輸入整數毫秒，例如 -1500 或 2000";
            return;
        }
        try
        {
            ShiftPreview = Describe(TimeOperations.Shift(Cues, offset, ids));
        }
        catch (EditRejectedException ex)
        {
            ShiftPreview = ex.Message;
        }
    }

    public static string Describe(TimeOperationResult r)
    {
        var text = $"對象 {r.TargetCount} 筆，時間變動 {r.ChangedCount} 筆";
        if (r.ClampedCount > 0)
        {
            text += $"，跨越 0 而起始截為 0：{r.ClampedCount} 筆";
        }
        if (r.RemovedCount > 0)
        {
            text += $"，完全落在 0 之前將移除：{r.RemovedCount} 筆";
        }
        return text;
    }

    [RelayCommand]
    private async Task ApplyShiftAsync()
    {
        if (!TryGetShift(out var offset, out var ids))
        {
            await _dialogs.AlertAsync("整體位移", "位移量必須是整數毫秒。");
            return;
        }
        if (ids is { Count: 0 })
        {
            await _dialogs.AlertAsync("整體位移", "已勾選「只套用到選取的字幕」，但目前沒有選取任何字幕。");
            return;
        }
        await RunTimeOperationAsync("整體位移", () => TimeOperations.Shift(Cues, offset, ids), $"位移 {offset:+#;-#;0} ms");
    }

    // ---- Two-point sync ----

    [ObservableProperty]
    private string _anchorASequence = string.Empty;

    [ObservableProperty]
    private string _anchorATime = string.Empty;

    [ObservableProperty]
    private string _anchorBSequence = string.Empty;

    [ObservableProperty]
    private string _anchorBTime = string.Empty;

    /// <summary>Fills anchor A from the selected cue (sequence and its current start).</summary>
    [RelayCommand]
    private void UseSelectionAsAnchorA() => FillAnchor(isA: true);

    [RelayCommand]
    private void UseSelectionAsAnchorB() => FillAnchor(isA: false);

    private void FillAnchor(bool isA)
    {
        if (EditingCue is not { } cue)
        {
            return;
        }
        var seq = (CueList.IndexOf(Cues, cue.Id) + 1).ToString(CultureInfo.InvariantCulture);
        var time = TimeFormat.Format(cue.StartMs);
        if (isA)
        {
            (AnchorASequence, AnchorATime) = (seq, time);
        }
        else
        {
            (AnchorBSequence, AnchorBTime) = (seq, time);
        }
    }

    [RelayCommand]
    private async Task ApplyTwoPointAsync()
    {
        if (!TryReadAnchor(AnchorASequence, AnchorATime, out var a, out var aTime, out var error) ||
            !TryReadAnchor(AnchorBSequence, AnchorBTime, out var b, out var bTime, out error))
        {
            await _dialogs.AlertAsync("兩點校正", error);
            return;
        }
        await RunTimeOperationAsync("兩點校正", () => TimeOperations.TwoPointSync(Cues, a, aTime, b, bTime),
            $"兩點校正（#{AnchorASequence.Trim()} → {AnchorATime.Trim()}，#{AnchorBSequence.Trim()} → {AnchorBTime.Trim()}）");
    }

    private bool TryReadAnchor(string sequence, string time, out int id, out long ms, out string error)
    {
        id = 0;
        ms = 0;
        if (!int.TryParse(sequence.Trim(), CultureInfo.InvariantCulture, out var seq) || seq < 1 || seq > Cues.Length)
        {
            error = $"錨點序號必須介於 1 與 {Cues.Length} 之間";
            return false;
        }
        if (!TimeFormat.TryParse(time, out ms, out var timeError))
        {
            error = $"錨點目標時間格式錯誤：{timeError}";
            return false;
        }
        id = Cues[seq - 1].Id;
        error = string.Empty;
        return true;
    }

    // ---- Frame rate ----

    [ObservableProperty]
    private FrameRate _sourceFrameRate;

    [ObservableProperty]
    private FrameRate _targetFrameRate;

    [RelayCommand]
    private async Task ApplyFrameRateAsync()
    {
        if (SourceFrameRate == TargetFrameRate)
        {
            await _dialogs.AlertAsync("幀率轉換", "來源與目標幀率相同，不需要轉換。");
            return;
        }
        SaveSettings(Settings with { SourceFrameRate = SourceFrameRate.Name, TargetFrameRate = TargetFrameRate.Name });
        await RunTimeOperationAsync("幀率轉換", () => TimeOperations.ConvertFrameRate(Cues, SourceFrameRate, TargetFrameRate),
            $"幀率轉換 {SourceFrameRate} → {TargetFrameRate}");
    }

    /// <summary>Computes the result, asks for confirmation with the preview summary, then commits it as one step.</summary>
    private async Task RunTimeOperationAsync(string title, Func<TimeOperationResult> operation, string description)
    {
        if (Cues.IsEmpty)
        {
            await _dialogs.AlertAsync(title, "目前沒有字幕。");
            return;
        }
        TimeOperationResult result;
        try
        {
            result = operation();
        }
        catch (EditRejectedException ex)
        {
            await _dialogs.AlertAsync(title, ex.Message);
            return;
        }
        if (!await _dialogs.ConfirmAsync(title, $"{Describe(result)}。\n確定套用？", "套用"))
        {
            return;
        }
        Commit(result.Cues, description);
    }
}
