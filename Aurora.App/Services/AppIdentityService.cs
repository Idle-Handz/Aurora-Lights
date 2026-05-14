using Microsoft.Maui.ApplicationModel;

namespace Aurora.App.Services;

public sealed class AppIdentityService
{
    public const string DefaultAppName = "Aurora: Reflections";

    public string AppName => DefaultAppName;
    public string PublisherName => "Aurora Lights Project";
    public string Tagline => "Cross-platform character building for Aurora content.";
    public string RepositoryUrl => "https://github.com/Idle-Handz/Aurora-Lights";
    public string IssuesUrl => "https://github.com/Idle-Handz/Aurora-Lights/issues";

    public string VersionLabel
    {
        get
        {
            try
            {
                string version = AppInfo.Current.VersionString;
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
