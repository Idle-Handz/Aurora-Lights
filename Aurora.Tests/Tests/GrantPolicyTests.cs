using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// A character must not receive content from a source it restricts, even when another book's rule
/// grants it, and clearing the restriction must give that content back.
/// </summary>
public sealed class GrantPolicyTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public GrantPolicyTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync()
    {
        GrantPolicyContext.Current = null;
        return Task.CompletedTask;
    }

    private sealed class SuppressById(string id) : IGrantPolicy
    {
        public bool IsSuppressed(ElementBase granted, GrantRule rule) =>
            string.Equals(granted.Id, id, StringComparison.Ordinal);
    }

    private sealed class ThrowingPolicy : IGrantPolicy
    {
        public bool IsSuppressed(ElementBase granted, GrantRule rule) => throw new InvalidOperationException("policy failure");
    }

    [Fact]
    public void NoPolicyGrantsEverything()
    {
        GrantPolicyContext.Current = null;
        GrantPolicyContext.IsSuppressed(new ElementBase { ElementHeader = new ElementHeader("X", "Feat", "Test", "ID_X") }, TestRule())
            .Should().BeFalse();
    }

    [Fact]
    public void AFailingPolicyNeverStripsContent()
    {
        GrantPolicyContext.Current = new ThrowingPolicy();
        GrantPolicyContext.IsSuppressed(new ElementBase { ElementHeader = new ElementHeader("X", "Feat", "Test", "ID_X") }, TestRule())
            .Should().BeFalse("a broken host policy must not remove a character's content");
    }

    [Fact]
    public async Task SuppressedGrantLeavesTheCharacterAndComesBackWhenAllowedAgain()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e")).Load();

        var manager = CharacterManager.Current;
        ElementBase granted = manager.GetElements().FirstOrDefault(e => e.Aquisition is { WasGranted: true })
            ?? throw new InvalidOperationException("The fixture character has no granted element to suppress.");
        string grantedId = granted.Id;

        GrantPolicyContext.Current = new SuppressById(grantedId);
        manager.ReprocessCharacter();
        manager.GetElements().Select(e => e.Id).Should().NotContain(grantedId,
            "a suppressed grant must not reach the character");

        GrantPolicyContext.Current = null;
        manager.ReprocessCharacter();
        manager.GetElements().Select(e => e.Id).Should().Contain(grantedId,
            "allowing the source again must re-grant it");
    }

    [Fact]
    public async Task RestrictingASourceSuppressesItsGrants()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e")).Load();

        var policy = new RestrictedSourceGrantPolicy();
        SourcesManager sources = CharacterManager.Current.SourcesManager;
        // Infrastructure sources are deliberately not restrictable, so pick a grant from a book that is.
        var restrictable = sources.SourceGroups.SelectMany(group => group.Sources)
            .Where(item => item.AllowUnchecking)
            .Select(item => item.Source.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ElementBase granted = CharacterManager.Current.GetElements()
            .FirstOrDefault(e => e.Aquisition is { WasGranted: true } && restrictable.Contains(e.Source))
            ?? throw new InvalidOperationException("The fixture character has no grant from a restrictable source.");

        policy.IsSuppressed(granted, TestRule()).Should().BeFalse("nothing is restricted yet");

        SourceItemFor(sources, granted.Source).SetIsChecked(false, updateChildren: true, updateParent: true);
        sources.ApplyRestrictions();
        try
        {
            policy.IsSuppressed(granted, TestRule())
                .Should().BeTrue($"'{granted.Source}' is restricted for this character");
        }
        finally
        {
            SourceItemFor(sources, granted.Source).SetIsChecked(true, updateChildren: true, updateParent: true);
            sources.ApplyRestrictions();
        }

        policy.IsSuppressed(granted, TestRule()).Should().BeFalse("the restriction was cleared");
    }

    private static GrantRule TestRule() => new(new ElementHeader("Granter", "Feat", "Test", "ID_TEST_GRANTER"));

    private static Builder.Presentation.Models.Sources.SourceItem SourceItemFor(SourcesManager sources, string name) =>
        sources.SourceGroups.SelectMany(group => group.Sources)
            .FirstOrDefault(item => string.Equals(item.Source.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Source '{name}' is not offered for restriction.");
}
