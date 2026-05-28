using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Verifies the new-multiclass mechanism behind BuildService.AddMulticlassLevelAsync. Starting a new
/// multiclass requires the correct order: NewMulticlass() first (it levels up and adds
/// ID_INTERNAL_MULTICLASS_LEVEL_N, which activates the matching &lt;select type="Multiclass"&gt; rule),
/// THEN the class is acquired through that rule (RegisterElement reads element.Aquisition.SelectRule —
/// registering a raw element throws NullReferenceException). Uses core content so it runs in the
/// harness (the EFA Artificer that triggered the original report is homebrew and not in the test DB).
/// </summary>
public sealed class MulticlassFlowTests : IAsyncLifetime
{
    private const string FighterClassId = "ID_PHB_CLASS_FIGHTER";
    private const string MulticlassPrereqGrant = "ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE";

    private readonly ITestOutputHelper _output;
    public MulticlassFlowTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NewMulticlass_ThenAcquireViaRule_AddsTheClassWithoutNullReference()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var fighter = elements.GetElement(FighterClassId);
        if (fighter is null) { _output.WriteLine("[SKIP] Fighter not present."); return; }

        var multiclass = elements.FirstOrDefault(e => e.Type == "Multiclass");
        if (multiclass is null) { _output.WriteLine("[SKIP] No Multiclass elements in content."); return; }
        _output.WriteLine($"Multiclassing into: {multiclass.Name} [{multiclass.Id}]");

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        var character = await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(fighter);

        // Meet every multiclass ability prerequisite by maxing all six scores, then reprocess so the
        // class grants ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE.
        character.Abilities.Strength.BaseScore = 15;
        character.Abilities.Dexterity.BaseScore = 15;
        character.Abilities.Constitution.BaseScore = 15;
        character.Abilities.Intelligence.BaseScore = 15;
        character.Abilities.Wisdom.BaseScore = 15;
        character.Abilities.Charisma.BaseScore = 15;
        cm.ReprocessCharacter();

        bool meetsPrereq = cm.GetElements().Any(e => e.Id == MulticlassPrereqGrant);
        if (!meetsPrereq) { _output.WriteLine("[SKIP] Prerequisite grant not present after maxing scores."); return; }

        int levelBefore = character.Level;

        // ── The fixed flow (mirrors AddMulticlassLevelAsync) ──
        cm.NewMulticlass();   // levels up + adds the multiclass-level grant → activates the rule

        var mcRule = cm.SelectionRules
            .Where(r => r.Attributes.Type == "Multiclass")
            .OrderByDescending(r => r.Attributes.RequiredLevel)
            .FirstOrDefault();
        mcRule.Should().NotBeNull(
            because: "NewMulticlass adds the multiclass-level grant that activates the Multiclass select rule");

        multiclass.Aquisition.SelectedBy(mcRule!);

        // This is the call that threw NullReferenceException with the old ordering.
        Action register = () => cm.RegisterElement(multiclass);
        register.Should().NotThrow("the class is now acquired via the select rule, so SelectRule is set");

        cm.ReprocessCharacter();

        character.Level.Should().Be(levelBefore + 1, "multiclassing spends a new character level");
        cm.ClassProgressionManagers.Any(m => m.IsMulticlass)
            .Should().BeTrue("a multiclass progression manager must exist after registering the class");
        _output.WriteLine($"Level {levelBefore} → {character.Level}; multiclass managers: " +
            cm.ClassProgressionManagers.Count(m => m.IsMulticlass));
    }

    [Fact]
    public async Task NewMulticlass_RegisteredThroughExpander_FillsTheSelectionSlot()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var previousExpander = SelectionRuleExpanderContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        try
        {
            var elements = DataManager.Current.ElementsCollection;
            var fighter = elements.GetElement(FighterClassId);
            var multiclass = elements.FirstOrDefault(e => e.Type == "Multiclass");
            if (fighter is null || multiclass is null) { _output.WriteLine("[SKIP] required elements not present."); return; }

            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();

            var cm = CharacterManager.Current;
            var character = await cm.New(initializeFirstLevel: true);
            cm.RegisterElement(fighter);

            character.Abilities.Strength.BaseScore = 15;
            character.Abilities.Dexterity.BaseScore = 15;
            character.Abilities.Constitution.BaseScore = 15;
            character.Abilities.Intelligence.BaseScore = 15;
            character.Abilities.Wisdom.BaseScore = 15;
            character.Abilities.Charisma.BaseScore = 15;
            cm.ReprocessCharacter();

            if (!cm.GetElements().Any(e => e.Id == MulticlassPrereqGrant))
            {
                _output.WriteLine("[SKIP] Prerequisite grant not present after maxing scores.");
                return;
            }

            cm.NewMulticlass();
            var mcRule = cm.SelectionRules
                .Where(r => r.Attributes.Type == "Multiclass")
                .OrderByDescending(r => r.Attributes.RequiredLevel)
                .FirstOrDefault();
            mcRule.Should().NotBeNull();

            SelectionRuleExpanderContext.Current!.SetRegisteredElement(mcRule!, multiclass.Id);

            var registered = SelectionRuleExpanderContext.Current.GetRegisteredElement(mcRule!) as ElementBase;
            registered.Should().NotBeNull("the multiclass rule should be saved as selected");
            registered!.Id.Should().Be(multiclass.Id);
            cm.ClassProgressionManagers.Any(m => m.IsMulticlass && m.ClassElement?.Id == multiclass.Id)
                .Should().BeTrue("registering through the expander should still create the class progression");
        }
        finally
        {
            SelectionRuleExpanderContext.Current = previousExpander;
        }
    }

    [Fact]
    public async Task RegisteringMulticlassRaw_WithoutSelectRule_Throws_DemonstratingTheBug()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var fighter = elements.GetElement(FighterClassId);
        var multiclass = elements.FirstOrDefault(e => e.Type == "Multiclass");
        if (fighter is null || multiclass is null) { _output.WriteLine("[SKIP] required elements not present."); return; }

        // Reset any acquisition the other test may have stamped on this shared singleton.
        multiclass.Aquisition = new AquisitionInfo();

        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(fighter);

        // Registering a raw Multiclass element (no Aquisition.SelectRule) is the old, broken order.
        Action registerRaw = () => cm.RegisterElement(multiclass);
        registerRaw.Should().Throw<NullReferenceException>(
            because: "RegisterElement's Multiclass case dereferences element.Aquisition.SelectRule — "
                   + "this is exactly the crash the reorder fix avoids");
    }

    /// <summary>
    /// The "one source of a class at a time" filter (GetAvailableMulticlassOptionsAsync) matches a
    /// multiclass option against the character's current classes BY NAME. This verifies the matching
    /// key is sound: a Multiclass option exists whose name equals the main class's name, and the
    /// current-class name set contains it — so the filter will exclude it (you can't multiclass into
    /// your own class, nor add the same class again from another source).
    /// </summary>
    [Fact]
    public async Task MulticlassOptions_ExcludeAClassTheCharacterAlreadyHas_ByName()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var fighter = elements.GetElement(FighterClassId);
        if (fighter is null) { _output.WriteLine("[SKIP] Fighter not present."); return; }

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(fighter);
        cm.ReprocessCharacter();

        // The key used by the filter: current class display names.
        var currentClassNames = cm.ClassProgressionManagers
            .Select(m => m.ClassElement?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        currentClassNames.Should().Contain("Fighter", "the registered main class is Fighter");

        // A Multiclass option must share that name (proves the by-name match aligns across the two
        // element kinds — Class vs Multiclass), so the filter can exclude it.
        var fighterMulticlass = elements.Where(e => e.Type == "Multiclass")
            .FirstOrDefault(e => string.Equals(e.Name, "Fighter", StringComparison.OrdinalIgnoreCase));
        fighterMulticlass.Should().NotBeNull(
            "a Fighter multiclass option exists and is named like the class, so name-matching excludes it");

        // Replicate the filter's keep-rule and confirm the Fighter option is dropped while other
        // (not-yet-taken) classes survive.
        bool KeepOption(ElementBase e) =>
            (cm.ClassProgressionManagers.Any(m => m.IsMulticlass && m.ClassElement?.Id == e.Id))
            || !currentClassNames.Contains(e.Name ?? "");

        var kept = elements.Where(e => e.Type == "Multiclass").Where(KeepOption).ToList();
        kept.Should().NotContain(e => string.Equals(e.Name, "Fighter", StringComparison.OrdinalIgnoreCase),
            "the character's own class is filtered out of the multiclass options");
        kept.Should().Contain(e => !string.Equals(e.Name, "Fighter", StringComparison.OrdinalIgnoreCase),
            "other classes the character doesn't have are still offered");
        _output.WriteLine($"Multiclass options kept: {kept.Count} (Fighter excluded)");
    }
}
