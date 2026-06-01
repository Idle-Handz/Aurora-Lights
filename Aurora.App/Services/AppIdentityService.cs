using System.Reflection;
using Microsoft.Maui.ApplicationModel;

namespace Aurora.App.Services;

public sealed class AppIdentityService
{
    public const string DefaultAppName = "Aurora: Reflections";

    public string AppName => DefaultAppName;
    public string PublisherName => "Idle Handz";
    public string Tagline => "Cross-platform character building for Aurora content.";
    public string PublisherUrl => "https://idlehandz.com";
    public string RepositoryUrl => "https://github.com/Idle-Handz/Aurora-Lights";
    public string IssuesUrl => "https://github.com/Idle-Handz/Aurora-Lights/issues";

    public string VersionLabel
    {
        get
        {
            try
            {
                string? informational = typeof(AppIdentityService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                string version = string.IsNullOrWhiteSpace(informational)
                    ? AppInfo.Current.VersionString
                    : informational;
                string build = AppInfo.Current.BuildString;
                return string.IsNullOrWhiteSpace(build) ? version : $"{version} (build {build})";
            }
            catch
            {
                return "Version unavailable";
            }
        }
    }
}
