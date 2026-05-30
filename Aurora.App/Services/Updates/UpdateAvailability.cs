namespace Aurora.App.Services.Updates;

/// <summary>Result of an update check against a single release channel.</summary>
/// <param name="LatestTag">Tag of the highest release seen on the channel, for example "v0.2.0-alpha". Always set.</param>
/// <param name="LatestVersion">Parsed SemVer of the latest tag.</param>
/// <param name="CurrentVersion">The currently-running build's version, as parsed from the assembly.</param>
/// <param name="IsNewer">True when LatestVersion is strictly greater than CurrentVersion.</param>
/// <param name="ReleaseUrl">Direct browser link to the release page, or empty if not provided.</param>
/// <param name="ReleaseNotes">Release notes body, possibly empty or null.</param>
/// <param name="IsPreRelease">Whether the latest release is flagged as a pre-release on GitHub.</param>
/// <param name="PublishedAt">Publish time from GitHub, if reported.</param>
public sealed record UpdateAvailability(
    string LatestTag,
    SemVer LatestVersion,
    SemVer CurrentVersion,
    bool IsNewer,
    string ReleaseUrl,
    string? ReleaseNotes,
    bool IsPreRelease,
    DateTimeOffset? PublishedAt);
