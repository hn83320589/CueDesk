using CueDesk.Presentation;

namespace CueDesk.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly MainViewModel _viewModel;

    // MainPage is resolved in CreateWindow, after InitializeComponent has loaded the shared resources it uses.
    public App(IServiceProvider services, MainViewModel viewModel)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _services = services;
        _viewModel = viewModel;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Desktop menu bars are only shown for pages hosted in a NavigationPage (or Shell).
        var window = new Window(new NavigationPage(_services.GetRequiredService<MainPage>()))
        {
            Title = _viewModel.WindowTitle,
            Width = 1400,
            Height = 900,
            MinimumWidth = 1000,
            MinimumHeight = 640,
        };
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle))
            {
                window.Title = _viewModel.WindowTitle;
            }
        };
        return window;
    }
}
