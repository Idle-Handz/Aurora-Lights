using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Sources;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Every character follows some rule about which sources it may use. A file that records its own
/// restrictions keeps them; one that records none — a new character, or an older file saved before
/// the restrictions were written — falls back to the configured defaults. What it must never do is
/// inherit whatever the character loaded before it happened to restrict.
/// </summary>
public sealed class DefaultSourceRestrictionFallbackTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private string _originalDefaults = string.Empty;

    public DefaultSourceRestrictionFallbackTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        await ContentFixture.EnsureAvailableAsync();
        _originalDefaults = ApplicationContext.Current.Settings.DefaultSourceRestrictions;
    }

    public Task DisposeAsync()
    {
        ApplicationContext.Current.Settings.DefaultSourceRestrictions = _originalDefaults;
        CharacterManager.Current.SourcesManager.ClearRestrictions();
        return Task.CompletedTask;
    }

    private static SourcesManager Sources => CharacterManager.Current.SourcesManager;

    private static IEnumerable<string> RestrictedIds =>
        Sources.RestrictedSources.Select(item => item.Source.Id);

    /// <summary>Two sources the user is allowed to switch off, taken from the loaded catalog.</summary>
    private static (SourceItem First, SourceItem Second) TwoRestrictableSources()
    {
        var candidates = Sources.SourceGroups
            .Where(group => group.AllowUnchecking)
            .SelectMany(group => group.Sources)
            .Take(2)
            .ToList();
        candidates.Should().HaveCount(2, "the catalog must offer sources that can be restricted");
        return (candidates[0], candidates[1]);
    }

    /// <summary>A copy of a fixture character with its sources node removed.</summary>
    private static string WithoutSourcesNode(string fixtureName)
    {
        string xml = File.ReadAllText(ContentFixture.GetCharacterFixturePath(fixtureName));
        int start = xml.IndexOf("<sources>", StringComparison.Ordinal);
        int end = xml.IndexOf("</sources>", StringComparison.Ordinal);
        (start >= 0 && end > start).Should().BeTrue("the fixture is expected to carry a sources node");
        xml = xml.Remove(start, end + "</sources>".Length - start);

        string path = Path.Combine(Path.GetTempPath(), $"aurora-no-sources-{Guid.NewGuid():N}.dnd5e");
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public async Task ACharacterWithNoSourcesNodeFollowsTheDefaults()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var (previousCharactersChoice, theDefault) = TwoRestrictableSources();
        ApplicationContext.Current.Settings.DefaultSourceRestrictions = theDefault.Source.Id;

        // Stand in for a character loaded earlier in the session that restricted something else.
        Sources.Load([previousCharactersChoice.Source.Id]);
        RestrictedIds.Should().Contain(previousCharactersChoice.Source.Id);

        string path = WithoutSourcesNode("prepared-paladin.dnd5e");
        try
        {
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(path).Load();
        }
        finally
        {
            File.Delete(path);
        }

        RestrictedIds.Should().Equal(theDefault.Source.Id);
    }

    [Fact]
    public async Task ACharacterThatRecordsNoRestrictionsKeepsThatChoice()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var (_, theDefault) = TwoRestrictableSources();
        ApplicationContext.Current.Settings.DefaultSourceRestrictions = theDefault.Source.Id;

        // The fixture carries <sources><restricted /></sources>: it has chosen "restrict nothing".
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e")).Load();

        RestrictedIds.Should().BeEmpty("a character's own restrictions outrank the defaults");
    }

    [Fact]
    public void LoadDefaultsWithNothingConfiguredReleasesTheStandingRestrictions()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var (previousCharactersChoice, _) = TwoRestrictableSources();
        Sources.Load([previousCharactersChoice.Source.Id]);
        ApplicationContext.Current.Settings.DefaultSourceRestrictions = string.Empty;

        Sources.LoadDefaults();

        RestrictedIds.Should().BeEmpty("no defaults means every source is allowed, not 'keep the last ones'");
    }

    [Fact]
    public async Task ANewCharacterStartsFromTheDefaults()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var (previousCharactersChoice, theDefault) = TwoRestrictableSources();
        ApplicationContext.Current.Settings.DefaultSourceRestrictions = theDefault.Source.Id;
        Sources.Load([previousCharactersChoice.Source.Id]);

        await CharacterManager.Current.New(initializeFirstLevel: false);

        RestrictedIds.Should().Equal(theDefault.Source.Id);
    }
}
