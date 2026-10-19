using CueDesk.Core.Encoding;
using CueDesk.Core.Export;
using CueDesk.Core.Quality;
using CueDesk.Core.Settings;
using CueDesk.Presentation.Services;

namespace CueDesk.Presentation.Tests;

public class MainViewModelTests
{
    // ---- open / encoding / errors ----

    [Fact]
    public async Task Open_loads_cues_and_shows_detected_encoding()
    {
        var h = new Harness();
        await h.OpenAsync(bytes: EncodingDetector.Big5.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\n中文字幕\r\n"));
        Assert.Equal("Big5", h.Vm.EncodingName);
        Assert.Equal("中文字幕", h.Vm.Cues[0].Text);
        Assert.False(h.Vm.IsModified);
        Assert.Equal([@"C:\subs\demo.srt"], h.Settings.Saved!.RecentFiles);
    }

    [Fact]
    public async Task Open_with_malformed_blocks_lists_line_numbers_and_loads_rest()
    {
        var h = new Harness();
        await h.OpenAsync("1\n00:00:01,000 --> 00:00:02,000\nok\n\nbroken\n\n3\n00:00:03,000 --> 00:00:04,000\nok2\n");
        Assert.Equal(2, h.Vm.Cues.Length);
        Assert.Equal(5, Assert.Single(h.Vm.ParseErrors).LineNumber);
        Assert.Contains("第 5 行", Assert.Single(h.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Dropping_non_subtitle_file_shows_clear_message()
    {
        var h = new Harness();
        await h.Vm.OpenDroppedAsync([@"C:\video.mp4"]);
        var alert = Assert.Single(h.Dialogs.Alerts);
        Assert.Contains(".srt", alert.Message);
        Assert.Contains("video.mp4", alert.Message);
        Assert.Empty(h.Vm.Cues);
    }

    [Fact]
    public async Task Dropping_srt_opens_it()
    {
        var h = new Harness();
        h.Files.Files[@"C:\d.srt"] = System.Text.Encoding.UTF8.GetBytes(Harness.ThreeCues);
        await h.Vm.OpenDroppedAsync([@"C:\readme.txt", @"C:\d.srt"]);
        Assert.Equal(3, h.Vm.Cues.Length);
        Assert.Equal(@"C:\d.srt", h.Vm.FilePath);
    }

    [Fact]
    public async Task Open_recent_missing_file_is_removed_from_list()
    {
        var h = new Harness(AppSettings.Default.WithRecentFile(@"C:\gone.srt"));
        await h.Vm.OpenRecentCommand.ExecuteAsync(@"C:\gone.srt");
        Assert.Single(h.Dialogs.Alerts);
        Assert.Empty(h.Settings.Saved!.RecentFiles);
    }

    [Fact]
    public void Settings_warning_is_exposed_for_startup_notice()
    {
        var h = new Harness(settingsWarning: "設定檔已損毀");
        Assert.Equal("設定檔已損毀", h.Vm.SettingsWarning);
    }

    // ---- unsaved changes ----

    [Fact]
    public async Task Opening_another_file_with_unsaved_changes_asks_and_cancel_keeps_document()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        await h.Vm.DeleteCommand.ExecuteAsync(null);
        Assert.True(h.Vm.IsModified);

        h.Dialogs.SaveChoices.Enqueue(SaveChoice.Cancel);
        await h.Vm.OpenCommand.ExecuteAsync(null);
        Assert.Equal(1, h.Dialogs.SaveQuestions);
        Assert.Equal(2, h.Vm.Cues.Length);
    }

    [Fact]
    public async Task Close_confirmation_save_choice_writes_file()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        await h.Vm.DeleteCommand.ExecuteAsync(null);

        h.Dialogs.SaveChoices.Enqueue(SaveChoice.Save);
        Assert.True(await h.Vm.ConfirmDiscardChangesAsync());
        Assert.False(h.Vm.IsModified);
        var saved = System.Text.Encoding.UTF8.GetString(h.Files.Files[@"C:\subs\demo.srt"]);
        Assert.DoesNotContain("first", saved);
    }

    [Fact]
    public async Task No_question_when_nothing_changed()
    {
        var h = new Harness();
        await h.OpenAsync();
        Assert.True(await h.Vm.ConfirmDiscardChangesAsync());
        Assert.Equal(0, h.Dialogs.SaveQuestions);
    }

    [Fact]
    public async Task Undo_back_to_saved_state_clears_modified()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        await h.Vm.DeleteCommand.ExecuteAsync(null);
        h.Vm.UndoCommand.Execute(null);
        Assert.False(h.Vm.IsModified);
        Assert.DoesNotContain("*", h.Vm.WindowTitle);
    }

    // ---- save / export ----

    [Fact]
    public async Task Save_respects_bom_setting_and_uses_crlf()
    {
        var h = new Harness(AppSettings.Default with { WriteBom = false });
        await h.OpenAsync();
        await h.Vm.SaveCommand.ExecuteAsync(null);
        var bytes = h.Files.Files[@"C:\subs\demo.srt"];
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("\r\n", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Saving_big5_source_asks_to_convert_to_utf8()
    {
        var h = new Harness();
        await h.OpenAsync(bytes: EncodingDetector.Big5.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\n中文\r\n"));
        h.Dialogs.ConfirmAnswer = false;
        await h.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("UTF-8", Assert.Single(h.Dialogs.Confirms));
        Assert.Equal(SubtitleEncoding.Big5, EncodingDetector.Decode(h.Files.Files[@"C:\subs\demo.srt"]).Encoding);
    }

    [Fact]
    public async Task Export_vtt_writes_chosen_path_and_keeps_modified_state()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.FileDialogs.SaveResults.Enqueue(@"C:\out\demo.vtt");
        await h.Vm.ExportCommand.ExecuteAsync(ExportFormat.WebVtt);
        Assert.StartsWith("\uFEFFWEBVTT", System.Text.Encoding.UTF8.GetString(h.Files.Files[@"C:\out\demo.vtt"]));
        Assert.Equal(@"C:\subs\demo.srt", h.Vm.FilePath);
    }

    // ---- editing ----

    [Fact]
    public async Task Apply_edit_changes_time_and_text_in_one_undo_step_and_resorts()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        h.Vm.EditStart = "00:00:10,000";
        h.Vm.EditEnd = "00:00:11,000";
        h.Vm.EditText = "moved";
        await h.Vm.ApplyEditCommand.ExecuteAsync(null);

        Assert.Equal(["second", "third", "moved"], h.Vm.Rows.Select(r => r.Cue.Text));
        Assert.Equal([1, 2, 3], h.Vm.Rows.Select(r => r.Sequence));
        h.Vm.UndoCommand.Execute(null);
        Assert.Equal("first", h.Vm.Cues[0].Text);
    }

    [Fact]
    public async Task Invalid_edit_is_rejected_with_message()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        h.Vm.EditEnd = "00:00:00,500";
        await h.Vm.ApplyEditCommand.ExecuteAsync(null);
        Assert.Single(h.Dialogs.Alerts);
        Assert.False(h.Vm.IsModified);
    }

    [Fact]
    public async Task Selecting_a_cue_fills_edit_panel_and_default_split_time()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(2);
        Assert.Equal("00:00:03,000", h.Vm.EditStart);
        Assert.Equal("second", h.Vm.EditText);
        Assert.Equal("00:00:04,000", h.Vm.SplitTime);
    }

    [Fact]
    public async Task Split_merge_insert_delete_keep_sequence_consistent()
    {
        var h = new Harness();
        await h.OpenAsync();

        h.Select(2);
        await h.Vm.SplitCommand.ExecuteAsync(null);
        Assert.Equal(4, h.Vm.Cues.Length);

        h.Select(2, 3);
        await h.Vm.MergeCommand.ExecuteAsync(null);
        Assert.Equal(3, h.Vm.Cues.Length);
        Assert.Equal((3_000L, 5_000L), (h.Vm.Cues[1].StartMs, h.Vm.Cues[1].EndMs));

        h.Select(3);
        await h.Vm.InsertCommand.ExecuteAsync(null);
        Assert.Equal(4, h.Vm.Cues.Length);
        Assert.Equal(9_040, h.Vm.Cues[3].StartMs);
        Assert.Equal(h.Vm.Cues[3].Id, Assert.Single(h.Vm.SelectedIds));

        await h.Vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(3, h.Vm.Cues.Length);
        Assert.Equal([1, 2, 3], h.Vm.Rows.Select(r => r.Sequence));
    }

    [Fact]
    public async Task Merge_of_non_adjacent_selection_is_rejected()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1, 3);
        await h.Vm.MergeCommand.ExecuteAsync(null);
        Assert.Contains("相鄰", Assert.Single(h.Dialogs.Alerts).Message);
        Assert.Equal(3, h.Vm.Cues.Length);
    }

    [Fact]
    public async Task New_edit_after_undo_clears_redo()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        await h.Vm.DeleteCommand.ExecuteAsync(null);
        h.Vm.UndoCommand.Execute(null);
        Assert.True(h.Vm.CanRedo);
        h.Select(2);
        await h.Vm.DeleteCommand.ExecuteAsync(null);
        Assert.False(h.Vm.CanRedo);
    }

    // ---- time operations ----

    [Fact]
    public async Task Shift_preview_reports_counts_before_applying()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Vm.ShiftOffset = "-3500";
        Assert.Contains("對象 3 筆", h.Vm.ShiftPreview);
        Assert.Contains("截為 0：1 筆", h.Vm.ShiftPreview);   // 3000–5000 → -500–1500 → 0–1500
        Assert.Contains("將移除：1 筆", h.Vm.ShiftPreview);   // 1000–2000 → before 0

        h.Vm.ShiftSelectedOnly = true;
        h.Select(3);
        Assert.Contains("對象 1 筆", h.Vm.ShiftPreview);
    }

    [Fact]
    public async Task Shift_applies_after_confirmation_as_one_step()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Vm.ShiftOffset = "500";
        await h.Vm.ApplyShiftCommand.ExecuteAsync(null);
        Assert.Contains("對象 3 筆", Assert.Single(h.Dialogs.Confirms));
        Assert.Equal([1_500L, 3_500L, 8_500L], h.Vm.Cues.Select(c => c.StartMs));
        h.Vm.UndoCommand.Execute(null);
        Assert.Equal([1_000L, 3_000L, 8_000L], h.Vm.Cues.Select(c => c.StartMs));
    }

    [Fact]
    public async Task Declined_confirmation_leaves_document_unchanged()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Dialogs.ConfirmAnswer = false;
        h.Vm.ShiftOffset = "500";
        await h.Vm.ApplyShiftCommand.ExecuteAsync(null);
        Assert.False(h.Vm.IsModified);
    }

    [Fact]
    public async Task Two_point_sync_from_selection_and_typed_targets()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Select(1);
        h.Vm.UseSelectionAsAnchorACommand.Execute(null);
        h.Select(3);
        h.Vm.UseSelectionAsAnchorBCommand.Execute(null);
        Assert.Equal(("1", "00:00:01,000"), (h.Vm.AnchorASequence, h.Vm.AnchorATime));
        h.Vm.AnchorATime = "00:00:02,000";
        h.Vm.AnchorBTime = "00:00:16,000";
        await h.Vm.ApplyTwoPointCommand.ExecuteAsync(null);
        // slope (16000−2000)/(8000−1000) = 2 → 3000 ↦ 2000 + 2000×2 = 6000
        Assert.Equal([2_000L, 6_000L, 16_000L], h.Vm.Cues.Select(c => c.StartMs));
    }

    [Fact]
    public async Task Two_point_with_reversed_targets_is_rejected()
    {
        var h = new Harness();
        await h.OpenAsync();
        (h.Vm.AnchorASequence, h.Vm.AnchorATime) = ("1", "00:00:10,000");
        (h.Vm.AnchorBSequence, h.Vm.AnchorBTime) = ("3", "00:00:05,000");
        await h.Vm.ApplyTwoPointCommand.ExecuteAsync(null);
        Assert.Contains("先後順序", Assert.Single(h.Dialogs.Alerts).Message);
        Assert.False(h.Vm.IsModified);
    }

    [Fact]
    public async Task Frame_rate_conversion_applies_and_remembers_choice()
    {
        var h = new Harness();
        await h.OpenAsync();
        h.Vm.SourceFrameRate = h.Vm.FrameRates.Single(f => f.Name == "25");
        h.Vm.TargetFrameRate = h.Vm.FrameRates.Single(f => f.Name == "24");
        await h.Vm.ApplyFrameRateCommand.ExecuteAsync(null);
        Assert.Equal(1_042, h.Vm.Cues[0].StartMs); // 1000 × 25 / 24 = 1041.67
        Assert.Equal(("25", "24"), (h.Settings.Saved!.SourceFrameRate, h.Settings.Saved.TargetFrameRate));
    }

    // ---- quality ----

    private const string Overlapping =
        "1\n00:00:01,000 --> 00:00:03,000\nOne\n\n" +
        "2\n00:00:02,500 --> 00:00:04,000\nTwo\n\n" +
        "3\n00:00:05,000 --> 00:00:07,000\nThree\n\n" +
        "4\n00:00:05,000 --> 00:00:06,000\nFour\n";

    [Fact]
    public async Task Issues_update_after_open_and_mark_unfixable_ones()
    {
        var h = new Harness();
        await h.OpenAsync(Overlapping);
        var overlaps = h.Vm.Issues.Where(i => i.Type == IssueType.Overlap).ToList();
        Assert.Equal(2, overlaps.Count);
        Assert.Null(overlaps[0].FixFailure);
        Assert.NotNull(overlaps[1].FixFailure);
        Assert.True(h.Vm.Rows[1].HasIssue);
    }

    [Fact]
    public async Task Fix_gaps_is_one_undo_step()
    {
        var h = new Harness();
        await h.OpenAsync(Overlapping);
        await h.Vm.FixGapsCommand.ExecuteAsync(null);
        Assert.Equal(2_460, h.Vm.Cues[0].EndMs);
        Assert.Single(h.Vm.Issues, i => i.Type == IssueType.Overlap);
        Assert.Contains("無法修正", h.Vm.StatusMessage);
        h.Vm.UndoCommand.Execute(null);
        Assert.Equal(3_000, h.Vm.Cues[0].EndMs);
        Assert.False(h.Vm.CanUndo);
    }

    [Fact]
    public async Task Selecting_issue_requests_scroll_to_cue()
    {
        var h = new Harness();
        await h.OpenAsync(Overlapping);
        int? scrolled = null;
        h.Vm.ScrollToCueRequested += id => scrolled = id;
        var issue = h.Vm.Issues[0];
        h.Vm.SelectIssueCommand.Execute(issue);
        Assert.Equal(issue.CueId, scrolled);
        Assert.Equal(issue.CueId, h.Vm.EditingCue!.Id);
    }

    [Fact]
    public async Task Changing_thresholds_recomputes_issues_and_saves_settings()
    {
        var h = new Harness();
        await h.OpenAsync();
        Assert.Empty(h.Vm.Issues);
        h.Vm.MinDurationMs = "1500";
        await h.Vm.ApplyThresholdsCommand.ExecuteAsync(null);
        Assert.Equal(2, h.Vm.Issues.Count(i => i.Type == IssueType.TooShort));
        Assert.Equal(1_500, h.Settings.Saved!.Thresholds.MinDurationMs);
    }

    [Fact]
    public async Task Invalid_threshold_input_is_rejected()
    {
        var h = new Harness();
        h.Vm.MaxCharsPerSecond = "abc";
        await h.Vm.ApplyThresholdsCommand.ExecuteAsync(null);
        Assert.Single(h.Dialogs.Alerts);
        Assert.Null(h.Settings.Saved);
    }
}
