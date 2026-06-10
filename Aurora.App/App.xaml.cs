using Aurora.App.Services;
using Aurora.App.Services.Updates;

namespace Aurora.App;

public partial class App : Application
{
    private readonly UserPreferencesService _prefs;
    private readonly AppUpdateService _appUpdates;

    public App(
        UserPreferencesService prefs,
        AppUpdateService appUpdates)
    {
        DebugLogService.Instance.Info("App constructor entered.");
        _prefs          = prefs;
        _appUpdates     = appUpdates;
        InitializeComponent();
        DebugLogService.Instance.Info("App InitializeComponent completed.");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        DebugLogService.Instance.Info("App.CreateWindow entered.");
        var window = new Window(new MainPage())
        {
            Title = AppIdentityService.DefaultAppName
        };
#if WINDOWS
        window.Destroying += (_, _) => _appUpdates.TryApplyPreparedUpdateOnExit();
#endif

        // Fire-and-forget the startup release check on a background task — never block window creation
        // on network I/O, and never throw out of here. The app-update preference gates GitHub
        // release traffic, so a first-time user sees no network traffic.
        _ = Task.Run(RunStartupUpdateChecksAsync);

        DebugLogService.Instance.Info("App.CreateWindow completed.");
        return window;
    }

    private async Task RunStartupUpdateChecksAsync()
    {
        try
        {
            bool includePreReleases = _prefs.IncludePrereleasesInUpdateCheck;
            if (_prefs.StartupCheckForAppUpdates)
                await _appUpdates.CheckAsync(includePreReleases).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "App.RunStartupUpdateChecksAsync");
        }
    }
}
