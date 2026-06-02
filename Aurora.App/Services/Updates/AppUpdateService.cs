using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Maui.ApplicationModel;

#if WINDOWS
using Velopack;
using Velopack.Sources;
#endif

namespace Aurora.App.Services.Updates;

/// <summary>
/// App-binary update channel: GitHub releases whose tag parses as SemVer (with or without a leading
/// <c>v</c>) and is NOT prefixed by a sub-channel keyword (e.g. <c>content-</c>). The current version
/// comes from assembly informational version, so package display versions can remain numeric while
/// the updater still compares the full release tag, including pre-release suffixes.
/// </summary>
public sealed class AppUpdateService : GithubReleaseChannelService
{
    public AppUpdateService(GithubReleasesClient client) : base(client, "app") { }

    protected override SemVer CurrentVersion
    {
        get
        {
            try
            {
                string? informational = typeof(AppUpdateService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

                if (SemVer.TryParse(informational, out var v))
                    return v;

                if (SemVer.TryParse(AppInfo.Current.VersionString, out v))
                    return v;
            }
            catch { }
            return default; // 0.0.0 — anything newer wins, which fails safe ("update available").
        }
    }

    // App-binary tags: anything that parses as SemVer and doesn't start with a sub-channel prefix.
    protected override bool TagBelongsToChannel(string tag)
        => !tag.StartsWith("content-", StringComparison.OrdinalIgnoreCase);

#if WINDOWS
    // ── Velopack apply (Windows only) ────────────────────────────────────────

    private bool? _isVelopackInstall;
    private bool _applyOnExitScheduled;

    /// <summary>
    /// True after an update has been downloaded and scheduled to install when the main window closes.
    /// </summary>
    public bool ApplyOnExitScheduled => _applyOnExitScheduled;

    /// <summary>
    /// True when the app was installed by Velopack and can apply updates in-place.
    /// Uses Velopack's own <see cref="UpdateManager.IsInstalled"/> which checks for
    /// the bootstrapper layout. ZIP-extracted copies return false, so the
    /// "Install and restart" button is hidden for them.
    /// Result is cached for the lifetime of the service — install layout never changes
    /// while the app is running.
    /// </summary>
    public bool IsVelopackInstall
    {
        get
        {
            if (_isVelopackInstall.HasValue) return _isVelopackInstall.Value;
            try
            {
                var mgr = CreateUpdateManager(includePreReleases: false);
                _isVelopackInstall = mgr.IsInstalled;
            }
            catch { _isVelopackInstall = false; }
            return _isVelopackInstall.Value;
        }
    }

    /// <summary>
    /// Downloads the pending update and restarts the app into the new version.
    /// Progress is 0–100. This method does NOT return on success — Velopack terminates
    /// the process and relaunches via the bootstrapper.
    /// Returns an error message string on failure, or null if the restart was initiated.
    /// </summary>
    public async Task<string?> ApplyAsync(
        bool includePreReleases,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var manager = CreateUpdateManager(includePreReleases);

            var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (updateInfo == null)
                return $"No applicable Velopack update was found for the {WindowsChannel} channel.";

            await manager.DownloadUpdatesAsync(updateInfo, p => progress?.Report(p), ct)
                         .ConfigureAwait(false);

            // Terminates the process and relaunches — nothing below here runs on success.
            manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
            return "The update was downloaded, but automatic restart did not begin. Close and reopen the app to finish applying it.";
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "AppUpdateService.ApplyAsync");
            return ex.Message;
        }
    }

    /// <summary>
    /// Downloads an available update now and remembers to install it when the main window closes.
    /// The Velopack helper itself is launched from <see cref="TryApplyPreparedUpdateOnExit"/> so its
    /// 60-second graceful-exit timeout starts only when the application is actually closing.
    /// </summary>
    public async Task<string?> PrepareApplyOnExitAsync(
        bool includePreReleases,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (_applyOnExitScheduled)
            return null;

        try
        {
            var manager = CreateUpdateManager(includePreReleases);
            var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (updateInfo == null)
                return $"No applicable Velopack update was found for the {WindowsChannel} channel.";

            await manager.DownloadUpdatesAsync(updateInfo, p => progress?.Report(p), ct)
                         .ConfigureAwait(false);

            _applyOnExitScheduled = true;
            DebugLogService.Instance.Info(
                $"[update:app] downloaded {updateInfo.TargetFullRelease.Version}; will install when the app closes");
            return null;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "AppUpdateService.PrepareApplyOnExitAsync");
            return ex.Message;
        }
    }

    /// <summary>
    /// Starts Velopack's graceful-exit updater after a prepared update has been requested.
    /// Call this while the main window is closing so Velopack does not exhaust its wait timeout.
    /// </summary>
    public void TryApplyPreparedUpdateOnExit()
    {
        if (!_applyOnExitScheduled)
            return;

        try
        {
            var manager = CreateUpdateManager(includePreReleases: true);
            var pending = manager.UpdatePendingRestart;
            if (pending == null)
            {
                DebugLogService.Instance.Warn(
                    "[update:app] install-on-close was requested, but no prepared update was found");
                return;
            }

            manager.WaitExitThenApplyUpdates(
                pending,
                silent: false,
                restart: false,
                restartArgs: []);
            _applyOnExitScheduled = false;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "AppUpdateService.TryApplyPreparedUpdateOnExit");
        }
    }

    private static string WindowsChannel =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "win-arm64"
            : "win-x64";

    private static UpdateManager CreateUpdateManager(bool includePreReleases)
    {
        // The channel matches the RID suffix used when packing in CI so the manager fetches
        // the right RELEASES-{channel} manifest and the correct nupkg variant.
        var source = new GithubSource(
            "https://github.com/Idle-Handz/Aurora-Lights",
            accessToken: null,
            prerelease: includePreReleases,
            downloader: null);

        return new UpdateManager(source, new UpdateOptions { ExplicitChannel = WindowsChannel });
    }
#endif
}
