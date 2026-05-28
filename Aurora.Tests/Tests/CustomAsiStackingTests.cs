using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Validates the engine-level mechanism behind the "Add Custom Feature → Additional Ability Score
/// Improvement" fix (BuildService.MakeStackableCopy). Ability-score elements such as
/// ID_INTERNAL_ASI_DEXTERITY are shared singletons that races / Tasha's origins / level-up ASIs also
/// register; each instance has a single Aquisition record. Registering the same instance a second
/// time clobbers the other source's bookkeeping (the two increases cancel). The fix registers a
/// shallow copy with fresh, blank acquisition so the bonuses stack — and the blank acquisition lets
/// removal tell the copy apart from the owned original.
///
/// Skipped (pass-without-assert) when the Aurora content database is unavailable.
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
        asiDex.Aquisition.WasSelected = true;
        cm.ReprocessCharacter();

        int afterOwned = character.Abilities.Dexterity.AdditionalScore;
        afterOwned.Should().Be(baseDexAdditional + 1,
            because: "an owned Dexterity ASI grants +1 to the additional score");

        // 2) The fix: a custom "Additional ASI, Dexterity" registers a fresh engine instance
        //    (ElementBaseCollection.GetFresh), NOT the owned singleton again — so the two +1s stack
        //    instead of cancelling. This is the same mechanism the legacy app uses to stack ASIs.
        var copy = DataManager.Current.ElementsCollection.GetFresh(DexAsiId)!;
        copy.Aquisition.WasSelected.Should().BeFalse("a fresh instance starts with blank acquisition");
        copy.Aquisition.WasGranted.Should().BeFalse();

        cm.RegisterElement(copy);
        cm.ReprocessCharacter();

        int afterCustom = character.Abilities.Dexterity.AdditionalScore;
        afterCustom.Should().Be(baseDexAdditional + 2,
            because: "the custom Dexterity increase must stack on top of the owned one");

        // The owned original must still be present and still owned (its bookkeeping wasn't clobbered).
        var matches = cm.GetElements().Where(e => e.Id == DexAsiId).ToList();
        matches.Should().HaveCountGreaterThanOrEqualTo(2, "both the owned ASI and the custom copy are registered");
        matches.Should().Contain(e => e.Aquisition.WasSelected, "the owned increase keeps its selection bookkeeping");
        matches.Should().Contain(e => !e.Aquisition.WasSelected && !e.Aquisition.WasGranted, "the custom copy has blank bookkeeping");

        // 3) Removal targets the blank-acquisition copy (RemoveCustomFeatureAsync's disambiguation),
        //    leaving the owned racial/origin increase intact.
        var toRemove = matches.FirstOrDefault(e => !e.Aquisition.WasGranted && !e.Aquisition.WasSelected)
                       ?? matches.First();
        cm.UnregisterElement(toRemove);
        cm.ReprocessCharacter();

        int afterRemove = character.Abilities.Dexterity.AdditionalScore;
        afterRemove.Should().Be(baseDexAdditional + 1,
            because: "removing the custom increase must leave the owned racial/origin increase intact");

        cm.GetElements().Where(e => e.Id == DexAsiId)
            .Should().Contain(e => e.Aquisition.WasSelected,
                because: "the owned increase survives removal of the custom copy");
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
    public void GetFresh_ReturnsADistinctInstance_WithBlankAcquisition()
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
        fresh.Aquisition.WasSelected.Should().BeFalse("a fresh instance must not inherit the singleton's acquisition bookkeeping");
        fresh.Aquisition.WasGranted.Should().BeFalse();
        _output.WriteLine("GetFresh returns a distinct instance with blank acquisition.");
    }

    [Fact]
    public async Task GetFreshCopy_StacksWithOwnedIncrease()
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

        // The engine-native instancing path: a fresh instance stacks on top of the owned one.
        var fresh = DataManager.Current.ElementsCollection.GetFresh(DexAsiId);
        cm.RegisterElement(fresh);
        cm.ReprocessCharacter();
        int afterFresh = character.Abilities.Dexterity.AdditionalScore;

        afterFresh.Should().Be(baseDexAdditional + 2,
            because: "an engine-fresh ASI instance must stack with the owned increase");
        _output.WriteLine($"GetFresh stacking: owned=+{afterOwned - baseDexAdditional}, +fresh=+{afterFresh - baseDexAdditional}");
    }

    [Fact]
    public async Task RegisteringSharedSingletonTwice_DoesNotStack_DemonstratingTheBug()
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

        // Re-registering the SAME instance (the old, broken behaviour) must NOT produce +2 — the
        // shared instance can't represent two independent increases. This guards the premise that the
        // copy-based fix is necessary (if this ever starts stacking, the fix can be simplified away).
        cm.RegisterElement(asiDex);
        cm.ReprocessCharacter();
        int afterDoubleRegister = character.Abilities.Dexterity.AdditionalScore;

        afterDoubleRegister.Should().NotBe(baseDexAdditional + 2,
            because: "re-registering the shared singleton can't stack — proving MakeStackableCopy is required");
        _output.WriteLine($"base+{baseDexAdditional - baseDexAdditional}, owned=+{afterOwned - baseDexAdditional}, double-register=+{afterDoubleRegister - baseDexAdditional}");
    }
}
