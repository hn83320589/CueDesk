using System.ComponentModel;
using CueDesk.Core.Quality;
using CueDesk.Presentation;

namespace CueDesk.App;

/// <summary>
/// View only: forwards selection, drag-and-drop and scroll requests between the
/// controls and <see cref="MainViewModel"/>; no subtitle logic lives here.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private bool _syncingSelection;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.ScrollToCueRequested += ScrollToCue;
        BuildRecentMenu();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel.SettingsWarningNeedsDialog)
        {
            await DisplayAlert("設定檔", _viewModel.SettingsWarning, "確定");
        }
        // "Open with" / CueDesk.App.exe file.srt
        var args = Environment.GetCommandLineArgs().Skip(1).ToList();
        if (args.Count > 0)
        {
            await _viewModel.OpenDroppedAsync(args);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Rows):
                ApplySelectionFromViewModel();
                break;
            case nameof(MainViewModel.RecentFiles):
                BuildRecentMenu();
                break;
        }
    }

    private void OnCueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }
        _viewModel.SetSelection(CueList.SelectedItems.OfType<CueRowViewModel>().Select(r => r.Id));
    }

    /// <summary>Rows are rebuilt on every change, so the selection is re-applied from the view model.</summary>
    private void ApplySelectionFromViewModel()
    {
        var selected = _viewModel.SelectedIds;
        _syncingSelection = true;
        try
        {
            CueList.SelectedItems = _viewModel.Rows.Where(r => selected.Contains(r.Id)).Cast<object>().ToList();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void ScrollToCue(int cueId)
    {
        ApplySelectionFromViewModel();
        var row = _viewModel.Rows.FirstOrDefault(r => r.Id == cueId);
        if (row is not null)
        {
            CueList.ScrollTo(row, position: ScrollToPosition.Center, animate: false);
        }
    }

    private void OnIssueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is QualityIssue issue)
        {
            _viewModel.SelectIssueCommand.Execute(issue);
            // Clear so clicking the same issue again scrolls again.
            IssueList.SelectedItem = null;
        }
    }

    private void BuildRecentMenu()
    {
        RecentMenu.Clear();
        if (_viewModel.RecentFiles.Count == 0)
        {
            RecentMenu.Add(new MenuFlyoutItem { Text = "（沒有最近開啟的檔案）", IsEnabled = false });
            return;
        }
        foreach (var path in _viewModel.RecentFiles)
        {
            RecentMenu.Add(new MenuFlyoutItem { Text = path, Command = _viewModel.OpenRecentCommand, CommandParameter = path });
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
#if WINDOWS
        if (e.PlatformArgs?.DragEventArgs is { } args)
        {
            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
#endif
    }

    private async void OnDrop(object? sender, DropEventArgs e)
    {
        var paths = new List<string>();
#if WINDOWS
        var view = e.PlatformArgs?.DragEventArgs.DataView;
        if (view is not null && view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            foreach (var item in await view.GetStorageItemsAsync())
            {
                paths.Add(item.Path);
            }
        }
#endif
        await _viewModel.OpenDroppedAsync(paths);
    }
}
