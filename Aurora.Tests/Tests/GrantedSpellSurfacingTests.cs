using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Diagnoses how feature-granted spells surface — the mechanism behind the Magic page's
/// "Granted by Features" read-only list (CharacterSnapshot.CollectGrantedSpells). A feat/racial
/// trait that grants an always-prepared spell (e.g. EFA Mark of Shadow, SRD Tiefling's Infernal
/// Legacy → Thaumaturgy) must show up with a non-class parent, including for non-casters.
///
/// Uses SRD Tiefling Fighter (a non-caster with an innate granted cantrip) because it's present in
/// every Aurora install and uses the same grant mechanism as the EFA dragonmark feats.
/// </summary>
public sealed class GrantedSpellSurfacingTests : IAsyncLifetime
{
    // SRD Tiefling → Infernal Legacy (Racial Trait) → grants Thaumaturgy cantrip.
    private const string TieflingRaceId = "ID_RACE_TIEFLING";
    private const string FighterClassId = "ID_PHB_CLASS_FIGHTER";
    private const string SorcererClassId = "ID_PHB_CLASS_SORCERER";
    private const string InfernalLegacyId = "ID_RACIAL_TRAIT_INFERNAL_LEGACY";
    private const string ThaumaturgyId = "ID_PHB_SPELL_THAUMATURGY";

    private static readonly HashSet<string> NonClassParentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Feat", "Feat Feature", "Item", "Magic Item", "Racial Trait", "Race",
        "Sub Race", "Background", "Background Feature", "Companion", "Dragonmark",
    };

    private readonly ITestOutputHelper _output;
    public GrantedSpellSurfacingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GrantedSpell_FromRacialTrait_SurfacesWithNonClassParent()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var race = elements.GetElement(TieflingRaceId);
        var fighter = elements.GetElement(FighterClassId);
        if (race is null || fighter is null)
        {
            _output.WriteLine("[SKIP] Tiefling or Fighter not present.");
            return;
        }

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(fighter); // non-caster class
        cm.RegisterElement(race);
        cm.ReprocessCharacter();

        // 1) Is the granted cantrip even present in the element set?
        var spells = cm.GetElements().Where(e => e.Type == "Spell").ToList();
        _output.WriteLine($"Spells in element set: {spells.Count}");
        foreach (var s in spells)
        {
            ElementHeader? parent = null;
            try { parent = s.Aquisition.GetParentHeader(); } catch { }
            _output.WriteLine($"  {s.Name} [{s.Id}] parent={(parent is null ? "<null>" : $"{parent.Type} / {parent.Name}")}");
        }

        var thaumaturgy = spells.FirstOrDefault(s => s.Id == ThaumaturgyId);
        thaumaturgy.Should().NotBeNull(
            "SRD Tiefling's Infernal Legacy grants Thaumaturgy, which must appear in the element set");

        // 2) Does GetParentHeader resolve to a non-class parent (Racial Trait), as CollectGrantedSpells expects?
        ElementHeader? thParent = null;
        try { thParent = thaumaturgy!.Aquisition.GetParentHeader(); } catch (Exception ex) { _output.WriteLine($"GetParentHeader threw: {ex.Message}"); }

        thParent.Should().NotBeNull("a granted spell must expose the granting element via GetParentHeader");
        _output.WriteLine($"Thaumaturgy parent: {thParent!.Type} / {thParent.Name}");
        NonClassParentTypes.Should().Contain(thParent.Type,
            because: "the grantor (Infernal Legacy = Racial Trait) must be recognised as a non-class parent");
    }

    /// <summary>
    /// Replica of CharacterSnapshot.CollectGrantedSpells (post-fix: no section dedup) so the Magic
    /// page's "Granted by Features" behaviour can be exercised end-to-end against the real engine.
    /// </summary>
    private static List<(string Name, string Source)> CollectGrantedSpells(CharacterManager cm)
    {
        var result = new List<(string, string)>();
        foreach (var e in cm.GetElements().Where(e => e.Type == "Spell"))
        {
            if (string.IsNullOrWhiteSpace(e.Name)) continue;
            try
            {
                var parent = e.Aquisition.GetParentHeader();
                if (parent != null && NonClassParentTypes.Contains(parent.Type ?? ""))
                    result.Add((e.Name!, parent.Name ?? ""));
            }
            catch { }
        }
        return result;
    }

    [Theory]
    [InlineData(FighterClassId, "non-caster")]
    [InlineData(SorcererClassId, "caster")]
    public async Task DirectlyRegisteredGrantor_SurfacesGrantedSpell_ForCasterAndNonCaster(string classId, string label)
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var cls = elements.GetElement(classId);
        // The custom-feature path registers the resolved underlying element directly. Use a GetFresh
        // instance so this test doesn't mutate the shared singleton's acquisition across runs.
        var grantor = elements.GetFresh(InfernalLegacyId);
        if (cls is null || grantor is null) { _output.WriteLine("[SKIP] required elements not present."); return; }

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(cls);
        cm.RegisterElement(grantor);   // ← mirrors AddCustomFeatureAsync registering the resolved element
        cm.ReprocessCharacter();

        var granted = CollectGrantedSpells(cm);
        _output.WriteLine($"[{label}] granted spells: {string.Join(", ", granted.Select(g => $"{g.Name} ({g.Source})"))}");

        granted.Should().Contain(g => g.Name == "Thaumaturgy",
            because: $"a directly-registered grantor's always-known spell must surface for a {label}");
    }

    /// <summary>
    /// Reproduces the custom-feature persistence bug: a directly-registered grantor (as
    /// AddCustomFeatureAsync registers it) is NOT part of the standard build CharacterFile.Save
    /// rebuilds, so after a serialize→reload cycle the grantor — and its granted spells — are gone.
    /// This is why custom features must be explicitly re-applied on load.
    /// </summary>
    [Fact]
    public async Task DirectlyRegisteredGrantor_IsLostAfterSerializeReload()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var fighter = elements.GetElement(FighterClassId);
        var grantor = elements.GetFresh(InfernalLegacyId);
        if (fighter is null || grantor is null) { _output.WriteLine("[SKIP] required elements not present."); return; }

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        var character = await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(fighter);
        cm.RegisterElement(grantor);
        cm.ReprocessCharacter();

        CollectGrantedSpells(cm).Should().Contain(g => g.Name == "Thaumaturgy",
            because: "the grantor's spell is present before saving");

        var file = cm.File;
        var bytes = file.SerializeCharacter(character);

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_test_{Guid.NewGuid():N}.dnd5e");
        bool stillThere;
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
            stillThere = CollectGrantedSpells(CharacterManager.Current).Any(g => g.Name == "Thaumaturgy");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        _output.WriteLine($"Thaumaturgy survives serialize/reload of a directly-registered grantor: {stillThere}");
        // Documents the behaviour: a directly-registered grantor does NOT survive the standard
        // serialize/reload. If this ever starts passing (engine serializes it), the explicit
        // re-application on load becomes redundant and can be revisited.
        stillThere.Should().BeFalse(
            because: "the standard save rebuilds only the build; custom features need explicit re-application on load");
    }
}
