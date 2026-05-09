using Aurora.App.Services;

namespace Aurora.App;

public partial class App : Application
{
    public App()
    {
        DebugLogService.Instance.Info("App constructor entered.");
        InitializeComponent();
        DebugLogService.Instance.Info("App InitializeComponent completed.");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        DebugLogService.Instance.Info("App.CreateWindow entered.");
        var window = new Window(new MainPage());
        DebugLogService.Instance.Info("App.CreateWindow completed.");
        return window;
    }
}
