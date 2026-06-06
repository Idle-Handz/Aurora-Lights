using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Integration tests for the character building flow. These tests require the Aurora
/// content database to be present and will be automatically skipped (pass-without-assert)
/// if it is not — they never fail on a machine that lacks the database.
///
/// They exercise the full pipeline: DataManager initialisation → CharacterFile.Load →
/// character state inspection → SerializeCharacter → reload → state comparison.
/// </summary>
public sealed class CharacterBuildingFlowTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    public CharacterBuildingFlowTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Data loading ─────────────────────────────────────────────────────────

    [Fact]
    public void Elements_AreLoaded_WhenDatabaseAvailable()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        DataManager.Current.ElementsCollection.Count.Should().BeGreaterThan(1000,
            because: "a full Aurora install has thousands of elements");
    }

    [Fact]
    public void ElementsCollection_ContainsCoreTypes()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var types = DataManager.Current.ElementsCollection
            .ToList()
            .Select(e => e.Type)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        types.Should().Contain("Race",       because: "PHB races must be present");
        types.Should().Contain("Class",      because: "PHB classes must be present");
        types.Should().Contain("Background", because: "PHB backgrounds must be present");
        types.Should().Contain("Spell",      because: "PHB spells must be present");
    }

    [Fact]
    public void ElementsCollection_ContainsHeroism()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var heroism = DataManager.Current.ElementsCollection.GetElement("ID_PHB_SPELL_HEROISM");
        heroism.Should().NotBeNull("Heroism is a core PHB spell and must be present");
        heroism!.Type.Should().Be("Spell");
        heroism.Name.Should().Be("Heroism");
    }

    // ── Character loading ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadCharacter_FromDisk_Succeeds()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var path = ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e");

        var file = new CharacterFile(path);
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await file.Load();

        CharacterManager.Current.Character.Should().NotBeNull(
            because: "loading a valid character file must populate CharacterManager.Current");

        var character = CharacterManager.Current.Character!;
        character.Name.Should().NotBeNullOrEmpty("every character must have a name");
        character.Level.Should().BeGreaterThan(0, "every character must have at least level 1");
    }

    [Fact]
    public async Task EquipmentItem_CustomNameAndNotes_SurviveSerializeReloadCycle()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var path = ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e");

        var file = new CharacterFile(path);
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await file.Load();

        var character = CharacterManager.Current.Character;
        character.Should().NotBeNull();

        var item = character!.Inventory.Items.FirstOrDefault();
        if (item is null) { _output.WriteLine("[SKIP] Character has no inventory items after load."); return; }

        var identifier = item.Identifier;
        var alias = $"Test Alias {Guid.NewGuid():N}";
        var notes = $"Test notes {Guid.NewGuid():N}";

        item.AlternativeName = alias;
        item.Notes = notes;

        var bytes = file.SerializeCharacter(character);
        bytes.Should().NotBeNullOrEmpty();

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_test_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        var reloadedItem = CharacterManager.Current.Character?.Inventory.Items
            .FirstOrDefault(i => i.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        reloadedItem.Should().NotBeNull("equipment item edits should be saved with the item identifier");
        reloadedItem!.AlternativeName.Should().Be(alias);
        reloadedItem.Notes.Should().Be(notes);
    }

    // ── Prepared spell round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task PreparedSpells_SurviveSerializeReloadCycle()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var path = ContentFixture.GetCharacterFixturePath("prepared-domain-cleric.dnd5e");

        // Load with a fresh handler so we capture what was stored in the file.
        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;

        var file = new CharacterFile(path);
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await file.Load();

        var character = CharacterManager.Current.Character;
        character.Should().NotBeNull();

        var spellcastingInfos = CharacterManager.Current
            .GetSpellcastingInformations()
            .ToList()
            .Where(i => i.Prepare)
            .ToList();

        if (spellcastingInfos.Count == 0)
        {
            _output.WriteLine("[SKIP] Character has no prepared-caster classes.");
            return;
        }

        var originalPrepared = spellcastingInfos
            .ToDictionary(
                i => i.Name,
                i => handler.GetPreparedIds(i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        originalPrepared.Values.Should().Contain(ids => ids.Count > 0,
            because: "at least one class should have manually prepared spells");

        // Serialize → write to temp → reload → compare.
        var bytes = file.SerializeCharacter(character!);
        bytes.Should().NotBeNull().And.NotBeEmpty();

        var handler2 = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler2;

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_test_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        foreach (var (className, originalIds) in originalPrepared)
        {
            var reloadedIds = handler2.GetPreparedIds(className);
            reloadedIds.Should().Contain(originalIds,
                because: $"prepared spells for {className} must survive a serialize/reload cycle");
        }
    }

    [Fact]
    public async Task PreparedSpell_Toggle_PersistsAfterSerialize()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var path = ContentFixture.GetCharacterFixturePath("prepared-domain-cleric.dnd5e");

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;

        var file = new CharacterFile(path);
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await file.Load();

        var character = CharacterManager.Current.Character!;
        var preparedInfo = CharacterManager.Current
            .GetSpellcastingInformations()
            .ToList()
            .FirstOrDefault(i => i.Prepare);

        if (preparedInfo is null) { _output.WriteLine("[SKIP] No prepared-caster class."); return; }

        var currentlyPrepared = handler.GetPreparedIds(preparedInfo.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unpreparedSpell = CharacterManager.Current
            .GetElements()
            .ToList()
            .Where(e => e.Type == "Spell")
            .Select(e => e.Id)
            .FirstOrDefault(id => !currentlyPrepared.Contains(id));

        if (unpreparedSpell is null) { _output.WriteLine("[SKIP] No unprepared spell available."); return; }

        // Simulate what OnPreparedChangedAsync does when the user checks a spell.
        handler.SetPrepareSpell(preparedInfo, unpreparedSpell);

        var bytes = file.SerializeCharacter(character);
        var handler2 = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler2;

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_test_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        handler2.GetPreparedIds(preparedInfo.Name)
            .Should().Contain(unpreparedSpell,
                because: "a newly prepared spell must survive serialize/reload");
    }

    // ── Character creation ────────────────────────────────────────────────────

    [Fact]
    public async Task NewCharacter_HasExpectedDefaults()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var character = await CharacterManager.Current.New(initializeFirstLevel: true);

        character.Should().NotBeNull();
        character.Level.Should().Be(1, "a fresh character starts at level 1");
    }

    // ── Full character build ──────────────────────────────────────────────────

    /// <summary>
    /// Exercises the complete build pipeline: New → register Race/Class/Background →
    /// verify derived state → serialize → reload → compare. Uses Human Fighter Soldier
    /// because these are guaranteed PHB elements present in every Aurora install.
    /// </summary>
    [Fact]
    public async Task FullBuild_HumanFighterSoldier_HasExpectedState()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var elements = DataManager.Current.ElementsCollection;

        var race       = elements.GetElement("ID_PHB_RACE_HUMAN");
        var cls        = elements.GetElement("ID_PHB_CLASS_FIGHTER");
        var background = elements.GetElement("ID_PHB_BACKGROUND_SOLDIER");

        if (race is null || cls is null || background is null)
        {
            _output.WriteLine("[SKIP] One or more required PHB elements not found (Human/Fighter/Soldier).");
            return;
        }

        var character = await CharacterManager.Current.New(initializeFirstLevel: true);
        character.Should().NotBeNull();

        CharacterManager.Current.RegisterElement(race);
        CharacterManager.Current.RegisterElement(cls);
        CharacterManager.Current.RegisterElement(background);

        var registered = CharacterManager.Current.GetElements().ToList();

        registered.Select(e => e.Type).Should().Contain("Race",       because: "Human was registered");
        registered.Select(e => e.Type).Should().Contain("Class",      because: "Fighter was registered");
        registered.Select(e => e.Type).Should().Contain("Background", because: "Soldier was registered");

        character.Level.Should().BeGreaterThanOrEqualTo(1);
        character.MaxHp.Should().BeGreaterThan(0,
            because: "Fighter with a race grants at minimum 10 HP at level 1");

        // Fighter proficiencies — must have at least one weapon or armour proficiency.
        registered.Select(e => e.Type).Should().Contain(t =>
            t.Contains("Proficiency", StringComparison.OrdinalIgnoreCase),
            because: "Fighter grants armour and weapon proficiencies");

        // Serialize → temp file → reload → verify core fields survive.
        var file  = CharacterManager.Current.File;
        var bytes = file.SerializeCharacter(character);
        bytes.Should().NotBeNullOrEmpty();

        var handler2 = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler2;

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_test_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        var reloaded = CharacterManager.Current.Character;
        reloaded.Should().NotBeNull();
        reloaded!.Level.Should().BeGreaterThanOrEqualTo(1);
        reloaded.MaxHp.Should().BeGreaterThan(0,
            because: "HP must survive a serialize/reload cycle");

        var reloadedElements = CharacterManager.Current.GetElements().ToList();
        reloadedElements.Select(e => e.Type).Should().Contain("Race",
            because: "Race element must survive serialize/reload");
        reloadedElements.Select(e => e.Type).Should().Contain("Class",
            because: "Class element must survive serialize/reload");
        reloadedElements.Select(e => e.Type).Should().Contain("Background",
            because: "Background element must survive serialize/reload");
    }
}
