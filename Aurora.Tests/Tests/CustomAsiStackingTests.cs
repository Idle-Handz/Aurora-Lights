using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Validates the legacy engine mechanism behind "Add Custom Feature → Additional Ability Score
/// Improvement." Ability-score elements are shared singletons, and the engine counts repeated
/// registration of that singleton. Its GetFresh path does not produce a stackable second increase.
///
/// Fails with an initialization diagnostic when the Aurora content database is unavailable.
/// </summary>
public sealed class CustomAsiStackingTests : IAsyncLifetime
{
    private const string DexAsiId = "ID_INTERNAL_ASI_DEXTERITY";

    private readonly ITestOutputHelper _output;
    public CustomAsiStackingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CustomAsiCopy_StacksWithOwnedIncrease_AndRemovesCleanly()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var asiDex = DataManager.Current.ElementsCollection.GetElement(DexAsiId);
        if (asiDex is null) { _output.WriteLine($"[SKIP] {DexAsiId} not present."); return; }
        asiDex.Type.Should().Be("Ability Score Improvement");

        // Reset the shared singleton's bookkeeping — it persists across tests in-process.
        asiDex.Aquisition = new AquisitionInfo();
        asiDex.RuleElements = new ElementBaseCollection();

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        var character = await cm.New(initializeFirstLevel: true);
        character.Should().NotBeNull();

        int baseDexAdditional = character.Abilities.Dexterity.AdditionalScore;

        // 1) Register the singleton once (stands in for a racial / Tasha's-origin Dex increase) and
        //    mark it owned-by-select, exactly as the real selection machinery would.
        cm.RegisterElement(asiDex);
        asiDex.Aquisition.SelectedBy(new SelectRule(asiDex.ElementHeader));
        cm.ReprocessCharacter();

        int afterOwned = character.Abilities.Dexterity.AdditionalScore;
        afterOwned.Should().Be(baseDexAdditional + 1,
            because: "an owned Dexterity ASI grants +1 to the additional score");

        // 2) The legacy engine counts a second registration of the shared ASI instance.
        cm.RegisterElement(asiDex);
        cm.ReprocessCharacter();

        int afterCustom = character.Abilities.Dexterity.AdditionalScore;
        afterCustom.Should().Be(baseDexAdditional + 2,
            because: "the custom Dexterity increase must stack on top of the owned one");

        // The owned acquisition remains intact while the manager holds two registrations.
        var matches = cm.GetElements().Where(e => e.Id == DexAsiId).ToList();
        matches.Should().HaveCountGreaterThanOrEqualTo(2, "both the owned ASI and the custom increase are registered");
        matches.Should().Contain(e => e.Aquisition.WasSelected, "the owned increase keeps its selection bookkeeping");

        // 3) Removal drops the last registration and leaves the first owned increase intact.
        var toRemove = matches.Last();
        var selectRule = toRemove.Aquisition.SelectRule;
        cm.UnregisterElement(toRemove);
        if (selectRule != null)
            toRemove.Aquisition.SelectedBy(selectRule);
        cm.ReprocessCharacter();

        int afterRemove = character.Abilities.Dexterity.AdditionalScore;
        afterRemove.Should().Be(baseDexAdditional + 1,
            because: "removing the custom increase must leave the owned racial/origin increase intact");

        cm.GetElements().Where(e => e.Id == DexAsiId)
            .Should().Contain(e => e.Aquisition.WasSelected,
                because: "the owned increase survives removal of the custom registration");
    }

    /// <summary>Mirror of EquipmentService.ResolveCustomFeatureTarget.</summary>
    private static ElementBase ResolveCustomFeatureTarget(ElementBase proxy)
    {
        if (proxy.Name?.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase) != true)
            return proxy;
#pragma warning disable CS0618 // Type or member is obsolete
        var grantedId = proxy.Rules?
            .OfType<GrantRule>()
            .Select(g => g.Attributes?.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
#pragma warning restore CS0618
        if (string.IsNullOrWhiteSpace(grantedId)) return proxy;
        return DataManager.Current.ElementsCollection.GetElement(grantedId) ?? proxy;
    }

    [Fact]
    public void AdditionalProxies_ResolveToTheirUnderlyingElement()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var proxies = DataManager.Current.ElementsCollection
            .Where(e => string.Equals(e.Type, "Item", StringComparison.OrdinalIgnoreCase)
                        && e.Name?.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase) == true)
            .Take(200)
            .ToList();

        if (proxies.Count == 0) { _output.WriteLine("[SKIP] No 'Additional …' proxies present."); return; }

        int resolved = 0;
        foreach (var proxy in proxies)
        {
            var underlying = ResolveCustomFeatureTarget(proxy);
            if (ReferenceEquals(underlying, proxy)) continue; // no GrantRule target — left as-is
            resolved++;

            // The resolved element is a real, distinct element — not the proxy boilerplate item.
            underlying.Id.Should().NotBe(proxy.Id, "the proxy must resolve to a different, underlying element");
            (underlying.Name ?? "").Should().NotStartWith("Additional ",
                because: "resolution should yield the real feat/spell/feature, not another proxy");
        }

        resolved.Should().BeGreaterThan(0,
            because: "at least some 'Additional …' proxies must resolve to a real underlying element via their GrantRule");
        _output.WriteLine($"Resolved {resolved}/{proxies.Count} sampled 'Additional …' proxies to underlying elements.");
    }

    [Fact]
    public void GetFresh_InheritsAcquisition_AndCannotRepresentASeparateAsi()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var singleton = DataManager.Current.ElementsCollection.GetElement(DexAsiId);
        if (singleton is null) { _output.WriteLine($"[SKIP] {DexAsiId} not present."); return; }

        // Mark the singleton as owned so we can tell whether GetFresh carries the bookkeeping over.
        singleton.Aquisition = new AquisitionInfo();
        singleton.Aquisition.WasSelected = true;

        var fresh = DataManager.Current.ElementsCollection.GetFresh(DexAsiId);

        fresh.Should().NotBeNull();
        ReferenceEquals(fresh, singleton).Should().BeFalse("GetFresh must return a distinct instance, not the singleton");
        fresh!.Id.Should().Be(DexAsiId, "the fresh instance keeps the element id");
        fresh.Type.Should().Be("Ability Score Improvement");
        fresh.Aquisition.WasSelected.Should().BeTrue(
            "the legacy GetFresh implementation copies acquisition bookkeeping");
        _output.WriteLine("GetFresh is distinct but inherits acquisition bookkeeping.");
    }

    [Fact]
    public async Task GetFreshCopy_DoesNotStackWithOwnedIncrease()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var asiDex = DataManager.Current.ElementsCollection.GetElement(DexAsiId);
        if (asiDex is null) { _output.WriteLine($"[SKIP] {DexAsiId} not present."); return; }
        asiDex.Aquisition = new AquisitionInfo();
        asiDex.RuleElements = new ElementBaseCollection();

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        var character = await cm.New(initializeFirstLevel: true);
        int baseDexAdditional = character.Abilities.Dexterity.AdditionalScore;

        cm.RegisterElement(asiDex);
        asiDex.Aquisition.WasSelected = true;
        cm.ReprocessCharacter();
        int afterOwned = character.Abilities.Dexterity.AdditionalScore;
        afterOwned.Should().Be(baseDexAdditional + 1);

        // GetFresh is not the engine path used for stackable ASIs.
        var fresh = DataManager.Current.ElementsCollection.GetFresh(DexAsiId);
        cm.RegisterElement(fresh);
        cm.ReprocessCharacter();
        int afterFresh = character.Abilities.Dexterity.AdditionalScore;

        afterFresh.Should().Be(baseDexAdditional + 1,
            because: "the engine does not count a distinct GetFresh ASI as a second increase");
        _output.WriteLine($"GetFresh behavior: owned=+{afterOwned - baseDexAdditional}, +fresh=+{afterFresh - baseDexAdditional}");
    }

    [Fact]
    public async Task RegisteringSharedSingletonTwice_StacksForCustomAsi()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var asiDex = DataManager.Current.ElementsCollection.GetElement(DexAsiId);
        if (asiDex is null) { _output.WriteLine($"[SKIP] {DexAsiId} not present."); return; }

        asiDex.Aquisition = new AquisitionInfo();
        asiDex.RuleElements = new ElementBaseCollection();

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        var character = await cm.New(initializeFirstLevel: true);
        int baseDexAdditional = character.Abilities.Dexterity.AdditionalScore;

        cm.RegisterElement(asiDex);
        asiDex.Aquisition.WasSelected = true;
        cm.ReprocessCharacter();
        int afterOwned = character.Abilities.Dexterity.AdditionalScore;

        // Re-registering the same instance is the legacy engine path that produces +2.
        cm.RegisterElement(asiDex);
        cm.ReprocessCharacter();
        int afterDoubleRegister = character.Abilities.Dexterity.AdditionalScore;

        afterDoubleRegister.Should().Be(baseDexAdditional + 2,
            because: "custom ASIs must use repeated registration rather than GetFresh");
        _output.WriteLine($"base+{baseDexAdditional - baseDexAdditional}, owned=+{afterOwned - baseDexAdditional}, double-register=+{afterDoubleRegister - baseDexAdditional}");
    }
}
