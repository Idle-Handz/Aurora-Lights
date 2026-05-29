namespace Aurora.App.Services.Updates;

/// <summary>Result of an update check against a single release channel.</summary>
public sealed record UpdateAvailability(
    /// <summary>Tag of the highest release seen on the channel (e.g. "v0.2.0-alpha"). Always set.</summary>
    string LatestTag,
    /// <summary>Parsed SemVer of <see cref="LatestTag"/>.</summary>
    SemVer LatestVersion,
    /// <summary>The currently-running build's version (as parsed from the assembly).</summary>
    SemVer CurrentVersion,
    /// <summary><c>true</c> when <see cref="LatestVersion"/> is strictly greater than <see cref="CurrentVersion"/>.</summary>
    bool IsNewer,
    /// <summary>Direct browser link to the release page (or empty if not provided).</summary>
    string ReleaseUrl,
    /// <summary>Release notes body (markdown, possibly empty/null).</summary>
    string? ReleaseNotes,
    /// <summary>Whether the latest release is flagged as a pre-release on GitHub.</summary>
    bool IsPreRelease,
    /// <summary>Publish time from GitHub, if reported.</summary>
    DateTimeOffset? PublishedAt);
