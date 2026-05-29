using Aurora.App.Services;
using Aurora.App.Services.Updates;

namespace Aurora.App;

public partial class App : Application
{
    private readonly UserPreferencesService _prefs;
    private readonly AppUpdateService _appUpdates;
    private readonly ContentUpdateService _contentUpdates;

    public App(
        UserPreferencesService prefs,
        AppUpdateService appUpdates,
        ContentUpdateService contentUpdates)
    {
        DebugLogService.Instance.Info("App constructor entered.");
        _prefs          = prefs;
        _appUpdates     = appUpdates;
        _contentUpdates = contentUpdates;
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

        // Fire-and-forget the startup update check on a background task — never block window creation
        // on network I/O, and never throw out of here. Each channel is independently gated by its own
        // preference (default off, matching the WPF), so a first-time user sees no network traffic.
        _ = Task.Run(RunStartupUpdateChecksAsync);

        DebugLogService.Instance.Info("App.CreateWindow completed.");
        return window;
    }

    private async Task RunStartupUpdateChecksAsync()
    {
        try
        {
            bool incl = _prefs.IncludePrereleasesInUpdateCheck;
            if (_prefs.StartupCheckForAppUpdates)
                await _appUpdates.CheckAsync(incl).ConfigureAwait(false);
            // This is only the notify-only GitHub Releases stream. The real content download
            // path is deferred until MainLayout renders so directories and UI subscribers exist.
            if (_prefs.StartupCheckForContentUpdates)
                await _contentUpdates.CheckAsync(incl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "App.RunStartupUpdateChecksAsync");
        }
    }
}
