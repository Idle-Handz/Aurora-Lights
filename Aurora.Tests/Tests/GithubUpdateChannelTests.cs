using System.Net;
using System.Text.Json;
using Aurora.App.Services.Updates;

namespace Aurora.Tests.Tests;

/// <summary>
/// Tests for <see cref="GithubReleaseChannelService"/>: channel tag filtering,
/// draft/pre-release exclusion, version ordering, and state lifecycle.
///
/// <para>
/// We can't link <c>AppUpdateService</c> or <c>ContentUpdateService</c> into this project
/// because they depend on MAUI APIs (<c>AppInfo</c>, <c>FileSystem</c>).  Instead we
/// drive the shared base-class logic through two small concrete channel implementations
/// that mirror the real ones exactly.
/// </para>
/// </summary>
public class GithubUpdateChannelTests
{
    // ── Test channel implementations ─────────────────────────────────────────

    /// <summary>Mirrors AppUpdateService's filtering: accepts any tag that doesn't start with "content-".</summary>
    private sealed class AppChannel(GithubReleasesClient client, SemVer current = default)
        : GithubReleaseChannelService(client, "test-app")
    {
        protected override SemVer CurrentVersion => current;
        protected override bool TagBelongsToChannel(string tag) =>
            !tag.StartsWith("content-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Mirrors ContentUpdateService's filtering: accepts only "content-*" tags, strips prefix before parsing.</summary>
    private sealed class ContentChannel(GithubReleasesClient client, SemVer current = default)
        : GithubReleaseChannelService(client, "test-content")
    {
        private const string Prefix = "content-";
        protected override SemVer CurrentVersion => current;
        protected override bool TagBelongsToChannel(string tag) =>
            tag.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        protected override bool TryParseTagVersion(string tag, out SemVer version) =>
            SemVer.TryParse(tag[Prefix.Length..], out version);
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────

    private sealed class FakeHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private static GithubReleasesClient MakeClient(params GithubRelease[] releases)
    {
        var json = JsonSerializer.Serialize(releases);
        return new GithubReleasesClient(new HttpClient(new FakeHandler(json)));
    }

    private static GithubReleasesClient MakeFailingClient()
        => new(new HttpClient(new FailingHandler()));

    private static GithubRelease Release(
        string tag,
        bool preRelease = false,
        bool draft = false)
        => new(
            TagName:     tag,
            Name:        null,
            HtmlUrl:     $"https://github.com/example/repo/releases/tag/{tag}",
            Body:        null,
            PreRelease:  preRelease,
            Draft:       draft,
            PublishedAt: DateTimeOffset.UtcNow,
            Assets:      null);

    // ── Channel tag filtering ─────────────────────────────────────────────────

    [Fact]
    public async Task AppChannel_ignores_content_prefixed_tags()
    {
        var ch = new AppChannel(MakeClient(Release("content-v1.5.0"), Release("v0.1.0")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().NotBeNull();
        result!.LatestTag.Should().Be("v0.1.0");
    }

    [Fact]
    public async Task AppChannel_accepts_plain_version_tags()
    {
        var ch = new AppChannel(MakeClient(Release("v1.2.3")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().NotBeNull();
        result!.LatestTag.Should().Be("v1.2.3");
    }

    [Fact]
    public async Task ContentChannel_ignores_plain_version_tags()
    {
        var ch = new ContentChannel(MakeClient(Release("v1.5.0"), Release("content-v0.1.0")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().NotBeNull();
        result!.LatestTag.Should().Be("content-v0.1.0");
    }

    [Fact]
    public async Task ContentChannel_accepts_content_prefixed_tags()
    {
        var ch = new ContentChannel(MakeClient(Release("content-v2.0.0")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().NotBeNull();
        result!.LatestTag.Should().Be("content-v2.0.0");
        result.LatestVersion.Should().Be(new SemVer(2, 0, 0, null));
    }

    [Fact]
    public async Task ContentChannel_strips_prefix_before_parsing_version()
    {
        // "content-v1.2.3-rc.1" → strips "content-" → parses "v1.2.3-rc.1" → SemVer(1,2,3,"rc.1")
        var ch = new ContentChannel(MakeClient(Release("content-v1.2.3-rc.1", preRelease: true)));
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().NotBeNull();
        result!.LatestVersion.Should().Be(new SemVer(1, 2, 3, "rc.1"));
        result.IsPreRelease.Should().BeTrue();
    }

    // ── Draft / pre-release exclusion ────────────────────────────────────────

    [Fact]
    public async Task Draft_releases_are_always_excluded()
    {
        var ch = new AppChannel(MakeClient(Release("v2.0.0", draft: true), Release("v1.0.0")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result!.LatestTag.Should().Be("v1.0.0", "draft releases must be invisible even when pre-releases are included");
    }

    [Fact]
    public async Task PreRelease_excluded_when_flag_is_false()
    {
        var ch = new AppChannel(MakeClient(Release("v1.1.0-alpha", preRelease: true), Release("v1.0.0")));
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.LatestTag.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task PreRelease_included_when_flag_is_true()
    {
        var ch = new AppChannel(MakeClient(Release("v1.1.0-alpha", preRelease: true), Release("v1.0.0")));
        var result = await ch.CheckAsync(includePreReleases: true);

        result!.LatestTag.Should().Be("v1.1.0-alpha");
    }

    [Fact]
    public async Task No_eligible_releases_returns_null()
    {
        var ch = new AppChannel(MakeClient(Release("content-v1.0.0")));  // wrong channel
        var result = await ch.CheckAsync(includePreReleases: true);

        result.Should().BeNull();
        ch.Latest.Should().BeNull();
    }

    // ── Version ordering + IsNewer ────────────────────────────────────────────

    [Fact]
    public async Task IsNewer_is_true_when_latest_beats_current()
    {
        SemVer.TryParse("0.1.0", out var current).Should().BeTrue();
        var ch = new AppChannel(MakeClient(Release("v0.2.0")), current);
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.IsNewer.Should().BeTrue();
        ch.UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsNewer_is_false_when_current_equals_latest()
    {
        SemVer.TryParse("1.0.0", out var current).Should().BeTrue();
        var ch = new AppChannel(MakeClient(Release("v1.0.0")), current);
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.IsNewer.Should().BeFalse();
        ch.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsNewer_is_false_when_current_is_newer_than_latest()
    {
        SemVer.TryParse("2.0.0", out var current).Should().BeTrue();
        var ch = new AppChannel(MakeClient(Release("v1.9.9")), current);
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.IsNewer.Should().BeFalse();
    }

    [Fact]
    public async Task Picks_highest_version_when_multiple_releases_exist()
    {
        var ch = new AppChannel(MakeClient(
            Release("v1.0.0"),
            Release("v0.3.0"),
            Release("v1.2.0"),   // ← highest
            Release("v0.9.0")));
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.LatestTag.Should().Be("v1.2.0");
        result.LatestVersion.Should().Be(new SemVer(1, 2, 0, null));
    }

    [Fact]
    public async Task Default_current_version_is_zero_so_any_release_is_newer()
    {
        // When CurrentVersion returns default(SemVer) = 0.0.0, any published release wins.
        // This is the "fresh install / no version recorded" sentinel used by both channels.
        var ch = new AppChannel(MakeClient(Release("v0.0.1")));
        var result = await ch.CheckAsync(includePreReleases: false);

        result!.IsNewer.Should().BeTrue("0.0.1 > 0.0.0 (default)");
    }

    // ── State lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public async Task IsChecking_is_false_before_and_after_check()
    {
        var ch = new AppChannel(MakeClient(Release("v1.0.0")));
        ch.IsChecking.Should().BeFalse();

        await ch.CheckAsync(includePreReleases: false);

        ch.IsChecking.Should().BeFalse();
    }

    [Fact]
    public async Task LastChecked_is_set_after_successful_check()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var ch = new AppChannel(MakeClient(Release("v1.0.0")));

        await ch.CheckAsync(includePreReleases: false);

        ch.LastChecked.Should().NotBeNull();
        ch.LastChecked!.Value.Should().BeAfter(before);
    }

    [Fact]
    public async Task LastChecked_is_set_after_failed_check()
    {
        // Regression: before the fix, LastChecked was not updated on failure, so the UI
        // would permanently show "Never checked" after a network error even if the user
        // pressed "Check now". The fix adds LastChecked = UtcNow to the catch block.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var ch = new AppChannel(MakeFailingClient());

        await ch.CheckAsync(includePreReleases: false);

        ch.LastChecked.Should().NotBeNull("failed checks should still record a timestamp");
        ch.LastChecked!.Value.Should().BeAfter(before);
        ch.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LastError_is_null_on_success_and_set_on_failure()
    {
        var ok  = new AppChannel(MakeClient(Release("v1.0.0")));
        var bad = new AppChannel(MakeFailingClient());

        await ok.CheckAsync(includePreReleases: false);
        await bad.CheckAsync(includePreReleases: false);

        ok.LastError.Should().BeNull();
        bad.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Changed_fires_at_start_and_end_of_check()
    {
        var ch = new AppChannel(MakeClient(Release("v1.0.0")));
        int fires = 0;
        ch.Changed += () => fires++;

        await ch.CheckAsync(includePreReleases: false);

        fires.Should().BeGreaterThanOrEqualTo(2, "Changed must fire at start (IsChecking=true) and end (IsChecking=false)");
    }
}
