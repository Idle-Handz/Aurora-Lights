using Aurora.App.Services;
using Builder.Presentation.Models;
using Microsoft.AspNetCore.Components;

namespace Aurora.Tests.Tests;

public sealed class CharacterLoadNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitingForTheCharacterStillOpensOverview(bool delayedWebViewNavigation)
    {
        var nav = new TestNavigationManager { DelayNavigation = delayedWebViewNavigation };
        var tabs = new CharacterTabService();
        var tab = tabs.OpenLoadingTab(File("loading"));
        using var pending = new CharacterLoadNavigation(nav, tabs, "/build");
        var loaded = new TaskCompletionSource();
        var completion = OpenAfterLoadAsync(pending, tab, loaded.Task);

        if (delayedWebViewNavigation) nav.CompleteNavigation();
        nav.Path.Should().Be("/build");
        tabs.NotifyChanged(); // Progress/snapshot updates are not a new navigation intent.
        loaded.SetResult();

        (await completion).Should().BeTrue();
        if (delayedWebViewNavigation) nav.CompleteNavigation();
        nav.Path.Should().Be("/character");
        tabs.ActiveTab.Should().BeSameAs(tab);
    }

    [Theory]
    [InlineData("/shop", false)]
    [InlineData("/", false)] // Back to the library.
    [InlineData("/shop", true)] // Returning to Build does not revive the old intent.
    public async Task ANewerNavigationSurvivesLoadCompletion(string destination, bool returnToBuild)
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var tab = tabs.OpenLoadingTab(File("loading"));
        using var pending = new CharacterLoadNavigation(nav, tabs, "/build");
        var loaded = new TaskCompletionSource();
        var completion = OpenAfterLoadAsync(pending, tab, loaded.Task);

        nav.NavigateTo(destination);
        if (returnToBuild) nav.NavigateTo("/build");
        loaded.SetResult();

        (await completion).Should().BeFalse();
        nav.Path.Should().Be(returnToBuild ? "/build" : destination);
        tabs.ActiveTab.Should().BeSameAs(tab);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchingTabsCancelsTheOldRedirectEvenIfTheUserSwitchesBack(bool switchBack)
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var other = tabs.OpenLoadingTab(File("other"));
        var tab = tabs.OpenLoadingTab(File("loading"));
        using var pending = new CharacterLoadNavigation(nav, tabs, "/build");

        tabs.ActivateTab(other);
        if (switchBack) tabs.ActivateTab(tab);

        pending.TryOpenOverview(tab).Should().BeFalse();
        tabs.ActiveTab.Should().BeSameAs(switchBack ? tab : other);
        nav.Path.Should().Be("/build");
    }

    [Fact]
    public void ClosingAndReopeningTheSameFileDoesNotRestoreTheOldLoadIntent()
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var file = File("loading");
        var oldTab = tabs.OpenLoadingTab(file);
        using var pending = new CharacterLoadNavigation(nav, tabs, "/build");

        tabs.CloseTab(oldTab);
        var newTab = tabs.OpenLoadingTab(file);

        pending.TryOpenOverview(oldTab).Should().BeFalse();
        pending.TryOpenOverview(newTab).Should().BeFalse();
        tabs.Tabs.Should().ContainSingle().Which.Should().BeSameAs(newTab);
        nav.Path.Should().Be("/build");
    }

    [Fact]
    public void ALateLoadingPageCallbackCannotReviveAnAbandonedNavigation()
    {
        var nav = new TestNavigationManager { DelayNavigation = true };
        var tabs = new CharacterTabService();
        var tab = tabs.OpenLoadingTab(File("loading"));
        using var pending = new CharacterLoadNavigation(nav, tabs, "/build");

        nav.ReportLocation("/shop");
        nav.CompleteNavigation(); // The WebView reports the earlier /build request late.

        pending.IsCurrent.Should().BeFalse();
        pending.TryOpenOverview(tab).Should().BeFalse();
        nav.Requests.Should().Equal("/build");
    }

    [Fact]
    public void AnExistingCloudTabIsActivatedOnlyIfTheUserIsStillWaitingForIt()
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var cloudTab = tabs.OpenLoadingTab(File("cloud"));
        tabs.OpenLoadingTab(File("local"));
        using var pending = new CharacterLoadNavigation(nav, tabs);

        tabs.NotifyChanged(); // Reload changes save status while another tab is active.

        pending.TryOpenOverview(cloudTab).Should().BeTrue();
        tabs.ActiveTab.Should().BeSameAs(cloudTab);
        nav.Path.Should().Be("/character");
        pending.TryOpenOverview(cloudTab).Should().BeFalse("a completion can navigate only once");
    }

    [Fact]
    public void NavigatingAwayWhileDriveDownloadsDoesNotActivateTheRequestedTab()
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var cloudTab = tabs.OpenLoadingTab(File("cloud"));
        var localTab = tabs.OpenLoadingTab(File("local"));
        using var pending = new CharacterLoadNavigation(nav, tabs);

        nav.NavigateTo("/shop");

        pending.IsCurrent.Should().BeFalse();
        pending.TryOpenOverview(cloudTab).Should().BeFalse();
        tabs.ActiveTab.Should().BeSameAs(localTab);
        nav.Path.Should().Be("/shop");
    }

    [Fact]
    public void DisposingAnAbandonedLoadPreventsItsRedirect()
    {
        var nav = new TestNavigationManager();
        var tabs = new CharacterTabService();
        var tab = tabs.OpenLoadingTab(File("loading"));
        var pending = new CharacterLoadNavigation(nav, tabs, "/build");

        pending.Dispose();

        pending.TryOpenOverview(tab).Should().BeFalse();
        nav.Path.Should().Be("/build");
    }

    private static CharacterFile File(string name) =>
        new(Path.Combine(Path.GetTempPath(), "Aurora.NavigationTests", name + ".dnd5e"));

    private static async Task<bool> OpenAfterLoadAsync(CharacterLoadNavigation navigation, CharacterTab tab, Task load)
    {
        await load;
        return navigation.TryOpenOverview(tab);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        private string? _pendingLocation;
        public bool DelayNavigation { get; init; }
        public List<string> Requests { get; } = [];
        public string Path => ToAbsoluteUri(Uri).AbsolutePath;

        public TestNavigationManager() => Initialize("https://aurora.test/", "https://aurora.test/");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Requests.Add(uri);
            if (DelayNavigation) _pendingLocation = uri;
            else ReportLocation(uri);
        }

        public void CompleteNavigation()
        {
            string location = _pendingLocation ?? throw new InvalidOperationException("No navigation is pending.");
            _pendingLocation = null;
            ReportLocation(location);
        }

        public void ReportLocation(string uri)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }
}
