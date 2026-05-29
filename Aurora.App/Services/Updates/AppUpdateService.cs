using System.Reflection;
using Microsoft.Maui.ApplicationModel;

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
}
