using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Aurora.App.Services.Updates;

/// <summary>
/// Minimal GitHub Releases REST client. We only need read access to public releases — no auth, no
/// pagination beyond the first page (we never expect more than a handful of latest entries to matter).
/// Unauthenticated GitHub API caps at 60 req/IP/hour, which is plenty for once-on-startup + one
/// manual press of "Check now". A User-Agent header is required by GitHub.
/// </summary>
public sealed class GithubReleasesClient
{
    private readonly HttpClient _http;

    public GithubReleasesClient(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Aurora-Reflections/1.0");
        if (!_http.DefaultRequestHeaders.Accept.Any())
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>Lists releases for <c>owner/repo</c> in published order (newest first per GitHub).</summary>
    public async Task<IReadOnlyList<GithubRelease>> ListReleasesAsync(
        string owner, string repo, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=30";
        var releases = await _http.GetFromJsonAsync<GithubRelease[]>(url, ct).ConfigureAwait(false);
        return releases ?? [];
    }
}

public sealed record GithubRelease(
    [property: JsonPropertyName("tag_name")]    string TagName,
    [property: JsonPropertyName("name")]        string? Name,
    [property: JsonPropertyName("html_url")]    string HtmlUrl,
    [property: JsonPropertyName("body")]        string? Body,
    [property: JsonPropertyName("prerelease")]  bool PreRelease,
    [property: JsonPropertyName("draft")]       bool Draft,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("assets")]      GithubReleaseAsset[]? Assets);

public sealed record GithubReleaseAsset(
    [property: JsonPropertyName("name")]                  string Name,
    [property: JsonPropertyName("browser_download_url")]  string DownloadUrl,
    [property: JsonPropertyName("size")]                  long Size,
    [property: JsonPropertyName("content_type")]          string? ContentType);
