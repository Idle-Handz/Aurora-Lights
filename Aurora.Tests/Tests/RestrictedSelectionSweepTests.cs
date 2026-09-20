using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Sources;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Restricting a source for a character must take back the picks that source supplied, so they come
/// back as choices to make again rather than staying as content the character may no longer use.
/// </summary>
public sealed class RestrictedSelectionSweepTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public RestrictedSelectionSweepTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void NothingIsClearedWhenNoSourceIsRestricted()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        RestrictedSelectionSweep.ClearRestrictedSelections().Should().BeEmpty();
    }

    [Fact]
    public async Task RestrictingASourceClearsThePicksItSupplied()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e")).Load();

        var manager = CharacterManager.Current;
        SourcesManager sources = manager.SourcesManager;

        // Find a registered pick whose source the character is allowed to restrict.
        var restrictable = sources.SourceGroups.SelectMany(group => group.Sources)
            .Where(item => item.AllowUnchecking)
            .ToDictionary(item => item.Source.Name, item => item, StringComparer.OrdinalIgnoreCase);
        (string Id, string Source)? pick = null;
        foreach (var rule in manager.SelectionRules.ToList())
        {
            for (int n = 1; n <= rule.Attributes.Number && pick is null; n++)
            {
                if (SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n) is ElementBase registered
                    && !string.IsNullOrWhiteSpace(registered.Source)
                    && restrictable.ContainsKey(registered.Source))
                {
                    pick = (registered.Id, registered.Source);
                }
            }
            if (pick is not null) break;
        }
        if (pick is null)
        {
            _output.WriteLine("The fixture character has no pick from a restrictable source.");
            return;
        }

        SourceItem item = restrictable[pick.Value.Source];
        item.SetIsChecked(false, updateChildren: true, updateParent: true);
        sources.ApplyRestrictions();
        try
        {
            IReadOnlyList<string> cleared = RestrictedSelectionSweep.ClearRestrictedSelections();

            cleared.Should().NotBeEmpty("the pick came from the source that is now restricted");
            manager.GetElements().Select(e => e.Id).Should().NotContain(pick.Value.Id,
                "a restricted pick must leave the character so it can be made again");
        }
        finally
        {
            item.SetIsChecked(true, updateChildren: true, updateParent: true);
            sources.ApplyRestrictions();
        }
    }
}
