namespace Aurora.App.Services.Updates;

/// <summary>
/// Shared base for "check a GitHub release channel for a newer version than the one we're running."
/// <see cref="AppUpdateService"/> uses this against the project's GitHub Releases feed.
///
/// Notify-only. Download/apply is handled by the app-update service where supported.
/// </summary>
public abstract class GithubReleaseChannelService
{
    private readonly GithubReleasesClient _client;
    private readonly string _channel;

    protected GithubReleaseChannelService(GithubReleasesClient client, string channel)
    {
        _client = client;
        _channel = channel;
    }

    /// <summary>Repo owner — defaults to the project's GitHub org.</summary>
    protected virtual string Owner => "Idle-Handz";
    /// <summary>Repo name.</summary>
    protected virtual string Repo  => "Aurora-Lights";

    /// <summary>The version this channel considers "currently installed."</summary>
    protected abstract SemVer CurrentVersion { get; }

    /// <summary>True when this tag belongs to this channel.</summary>
    protected abstract bool TagBelongsToChannel(string tag);

    /// <summary>Strip the channel prefix and parse to <see cref="SemVer"/>. Default strips nothing.</summary>
    protected virtual bool TryParseTagVersion(string tag, out SemVer version) => SemVer.TryParse(tag, out version);

    /// <summary>Fires whenever <see cref="Latest"/>, <see cref="LastChecked"/>, <see cref="LastError"/>,
    /// or <see cref="IsChecking"/> changes — so the UI can react without polling.</summary>
    public event Action? Changed;

    public UpdateAvailability? Latest      { get; private set; }
    public DateTimeOffset?     LastChecked { get; private set; }
    public string?             LastError   { get; private set; }
    public bool                IsChecking  { get; private set; }

    /// <summary>Convenience: true when we have a confirmed-newer release waiting.</summary>
    public bool UpdateAvailable => Latest?.IsNewer == true;

    /// <summary>
    /// Hits GitHub, parses the eligible releases on this channel, picks the highest version that the
    /// caller's pre-release preference allows, and updates state. Safe to call concurrently — checks
    /// overlap but the result is last-writer-wins; callers should serialize if they care about that.
    /// </summary>
    public async Task<UpdateAvailability?> CheckAsync(bool includePreReleases, CancellationToken ct = default)
    {
        IsChecking = true; LastError = null;
        Changed?.Invoke();
        try
        {
            var releases = await _client.ListReleasesAsync(Owner, Repo, ct).ConfigureAwait(false);

            // Filter to this channel + parseable tags + (optionally) pre-releases.
            var candidates = releases
                .Where(r => !r.Draft)
                .Where(r => TagBelongsToChannel(r.TagName))
                .Where(r => includePreReleases || !r.PreRelease)
                .Select(r => (Release: r, Parsed: TryParseTagVersion(r.TagName, out var v) ? v : default(SemVer?)))
                .Where(x => x.Parsed.HasValue)
                .ToList();

            if (candidates.Count == 0)
            {
                Latest = null;
                LastChecked = DateTimeOffset.UtcNow;
                DebugLogService.Instance.Info($"[update:{_channel}] no eligible releases found");
                return null;
            }

            var (top, version) = candidates
                .OrderByDescending(x => x.Parsed!.Value)
                .Select(x => (x.Release, x.Parsed!.Value))
                .First();

            var current = CurrentVersion;
            var availability = new UpdateAvailability(
                LatestTag:     top.TagName,
                LatestVersion: version,
                CurrentVersion: current,
                IsNewer:       version > current,
                ReleaseUrl:    top.HtmlUrl ?? "",
                ReleaseNotes:  top.Body,
                IsPreRelease:  top.PreRelease,
                PublishedAt:   top.PublishedAt);

            Latest = availability;
            LastChecked = DateTimeOffset.UtcNow;
            DebugLogService.Instance.Info(
                $"[update:{_channel}] checked: current={current}, latest={version}, newer={availability.IsNewer}");
            return availability;
        }
        catch (Exception ex)
        {
            LastChecked = DateTimeOffset.UtcNow;
            LastError = ex.Message;
            DebugLogService.Instance.LogException(ex, $"update:{_channel}");
            return null;
        }
        finally
        {
            IsChecking = false;
            Changed?.Invoke();
        }
    }
}
