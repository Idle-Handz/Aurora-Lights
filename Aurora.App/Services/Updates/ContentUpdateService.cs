namespace Aurora.App.Services.Updates;

/// <summary>
/// Content auto-update channel: releases whose tag starts with <c>content-</c>, e.g. <c>content-v1.2.3</c>.
/// "Currently installed content version" is tracked locally in a sentinel file under the app's data
/// directory. Until any content release is installed, the channel is surfaced as a release notice
/// rather than an installed-content update.
///
/// This is notify-only for now (parity with the legacy WPF <c>IndicesUpdateService</c> UX surface).
/// The actual download + extract + DataManager reload is a follow-up; the call site is the same shape
/// as the app channel (<see cref="CheckAsync"/>), so the UI doesn't need to change when it lands.
/// </summary>
public sealed class ContentUpdateService : GithubReleaseChannelService
{
    private const string TagPrefix = "content-";
    private readonly string _sentinelPath;

    public ContentUpdateService(GithubReleasesClient client) : base(client, "content")
    {
        var root = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
        _sentinelPath = Path.Combine(root, "content-version.txt");
    }

    public bool HasRecordedInstalledVersion
    {
        get
        {
            try { return File.Exists(_sentinelPath); }
            catch { return false; }
        }
    }

    public bool InstalledUpdateAvailable => HasRecordedInstalledVersion && UpdateAvailable;

    public bool ReleaseNoticeAvailable => !HasRecordedInstalledVersion && Latest is not null;

    protected override SemVer CurrentVersion
    {
        get
        {
            try
            {
                if (!File.Exists(_sentinelPath)) return default;
                var raw = File.ReadAllText(_sentinelPath).Trim();
                return SemVer.TryParse(raw, out var v) ? v : default;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "ContentUpdateService.CurrentVersion");
                return default;
            }
        }
    }

    /// <summary>Records the currently-installed content release version. Called after a successful release install.</summary>
    public void RecordInstalledVersion(SemVer version)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_sentinelPath)!);
            File.WriteAllText(_sentinelPath, version.ToString());
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "ContentUpdateService.RecordInstalledVersion");
        }
    }

    protected override bool TagBelongsToChannel(string tag)
        => tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase);

    // Strip the channel prefix before SemVer-parsing, e.g. "content-v1.2.3" → "v1.2.3".
    protected override bool TryParseTagVersion(string tag, out SemVer version)
        => SemVer.TryParse(tag[TagPrefix.Length..], out version);
}
