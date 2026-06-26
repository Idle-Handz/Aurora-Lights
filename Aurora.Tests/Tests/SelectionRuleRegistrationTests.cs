using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Xml;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Covers the registration path shared by the MAUI picker and fixture harness. These
/// checks exercise a real selection rule, replacement, XML serialization, and reload.
/// </summary>
public sealed class SelectionRuleRegistrationTests : IAsyncLifetime
{
    private const string HumanRaceId = "ID_RACE_HUMAN";
    private const string CustomizedLanguageOptionId = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_LANGUAGE";
    private const string AcolyteBackgroundId = "ID_BACKGROUND_ACOLYTE";
    private const string DruidClassId = "ID_WOTC_PHB24_CLASS_DRUID";
    private const string FighterClassId = "ID_PHB_CLASS_FIGHTER";
    private const string WarlockClassId = "ID_WOTC_PHB24_CLASS_WARLOCK";
    private const string EldritchBlastId = "ID_WOTC_PHB24_SPELL_ELDRITCH_BLAST";
    private const string AgonizingBlastId = "ID_WOTC_PHB24_CLASS_FEATURE_ELDRITCH_INVOCATION_AGONIZING_BLAST";
    private const string ElvishLanguageId = "ID_LANGUAGE_ELVISH";
    private const string DwarvishLanguageId = "ID_LANGUAGE_DWARVISH";

    private readonly ITestOutputHelper _output;

    public SelectionRuleRegistrationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Replacement_SaveReload_PreservesOnlyTheReplacementInItsOriginalSlot()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateHumanLanguageCharacterAsync();
        var rule = FindHumanLanguageRule();

        handler.SetRegisteredElement(rule, ElvishLanguageId);
        CharacterManager.Current.ReprocessCharacter();
        handler.SetRegisteredElement(rule, DwarvishLanguageId);
        CharacterManager.Current.ReprocessCharacter();

        var selected = handler.GetRegisteredElement(rule) as ElementBase;
        selected.Should().NotBeNull();
        selected!.Id.Should().Be(DwarvishLanguageId);
        CharacterManager.Current.GetElements().Should().Contain(element => element.Id == DwarvishLanguageId);
        CharacterManager.Current.GetElements().Should().NotContain(element => element.Id == ElvishLanguageId);

        byte[] bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        var document = new XmlDocument();
        using (var stream = new MemoryStream(bytes))
            document.Load(stream);
        XmlNodeList? persistedSelections = document.SelectNodes(
            $"//select[@type='Language'][@registered='{DwarvishLanguageId}']");
        persistedSelections.Should().NotBeNull();
        persistedSelections!.Count.Should().BeGreaterThan(0);

        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_selection_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);

            var reloadedHandler = new TestSelectionRuleExpanderHandler();
            SelectionRuleExpanderContext.Current = reloadedHandler;
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloadedRule = FindHumanLanguageRule();
            var reloaded = reloadedHandler.GetRegisteredElement(reloadedRule) as ElementBase;
            reloaded.Should().NotBeNull();
            reloaded!.Id.Should().Be(DwarvishLanguageId);
            CharacterManager.Current.GetElements().Should().Contain(element => element.Id == DwarvishLanguageId);
            CharacterManager.Current.GetElements().Should().NotContain(element => element.Id == ElvishLanguageId);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task DirectRegistration_RejectsAnOwnedNonRepeatableSelectionWithoutReplacingTheCurrentSlot()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateHumanLanguageCharacterAsync();
        var rule = FindHumanLanguageRule();

        handler.SetRegisteredElement(rule, ElvishLanguageId);
        CharacterManager.Current.ReprocessCharacter();

        Action duplicateSelection = () => handler.SetRegisteredElement(rule, ElvishLanguageId, number: 2);
        duplicateSelection.Should().Throw<InvalidOperationException>()
            .WithMessage("*already selected*");

        (handler.GetRegisteredElement(rule) as ElementBase)?.Id.Should().Be(ElvishLanguageId);
        handler.GetRegisteredElement(rule, number: 2).Should().BeNull();
        CharacterManager.Current.GetElements().Count(element => element.Id == ElvishLanguageId).Should().Be(1);
    }

    [Fact]
    public async Task ListSelection_SaveReloadAndClear_KeepTheBackgroundOwnerInSync()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await CharacterManager.Current.New(initializeFirstLevel: true);

        var background = DataManager.Current.ElementsCollection.GetElement(AcolyteBackgroundId);
        if (background is null)
        {
            _output.WriteLine("[SKIP] Acolyte background is not available in the loaded content.");
            return;
        }

        CharacterManager.Current.RegisterElement(background);
        var rule = CharacterManager.Current.SelectionRules.FirstOrDefault(candidate =>
            candidate.ElementHeader?.Id == AcolyteBackgroundId &&
            candidate.Attributes.IsList &&
            candidate.Attributes.ListItems?.Count >= 2);
        if (rule is null)
        {
            _output.WriteLine("[SKIP] Acolyte list selection data is not available in the loaded content.");
            return;
        }

        var selectedItem = rule.Attributes.ListItems![1];
        handler.SetRegisteredElement(rule, selectedItem.ID.ToString());
        CharacterManager.Current.ReprocessCharacter();

        byte[] bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_list_selection_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);

            var reloadedHandler = new TestSelectionRuleExpanderHandler();
            SelectionRuleExpanderContext.Current = reloadedHandler;
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloadedRule = CharacterManager.Current.SelectionRules.First(candidate =>
                candidate.ElementHeader?.Id == AcolyteBackgroundId &&
                candidate.Attributes.IsList &&
                candidate.Attributes.Name == rule.Attributes.Name);
            var reloadedItem = reloadedHandler.GetRegisteredElement(reloadedRule) as SelectionRuleListItem;
            reloadedItem.Should().NotBeNull();
            reloadedItem!.ID.Should().Be(selectedItem.ID);

            reloadedHandler.ClearRegisteredElement(reloadedRule);
            var owner = CharacterManager.Current.GetElements()
                .First(element => element.Id == AcolyteBackgroundId);
            owner.SelectionRuleListItems.Should().NotContainKey($"{reloadedRule.Attributes.Name}:1");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SpellSelection_SaveReload_PreservesTheSelectedSpellRuleSlot()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await CharacterManager.Current.New(initializeFirstLevel: true);

        var druid = DataManager.Current.ElementsCollection.GetElement(DruidClassId);
        if (druid is null)
        {
            _output.WriteLine("[SKIP] 2024 Druid is not available in the loaded content.");
            return;
        }

        CharacterManager.Current.RegisterElement(druid);
        CharacterManager.Current.ReprocessCharacter();

        var selection = FindSupportedElementSelection("Spell");
        if (selection is null)
        {
            _output.WriteLine("[SKIP] No evaluable Druid spell selection rule is available in the loaded content.");
            return;
        }

        handler.SetRegisteredElement(selection.Value.Rule, selection.Value.Element.Id);
        CharacterManager.Current.ReprocessCharacter();

        byte[] bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_spell_selection_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);

            var reloadedHandler = new TestSelectionRuleExpanderHandler();
            SelectionRuleExpanderContext.Current = reloadedHandler;
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloadedSpellIds = CharacterManager.Current.SelectionRules
                .Where(rule => rule.Attributes.Type.Equals("Spell", StringComparison.OrdinalIgnoreCase))
                .SelectMany(rule => Enumerable.Range(1, Math.Max(1, rule.Attributes.Number))
                    .Select(number => reloadedHandler.GetRegisteredElement(rule, number) as ElementBase))
                .Where(spell => spell is not null)
                .Select(spell => spell!.Id)
                .ToList();

            reloadedSpellIds.Should().Contain(selection.Value.Element.Id);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ProficiencySelection_SaveReload_PreservesTheSelectedRuleSlot()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await CharacterManager.Current.New(initializeFirstLevel: true);

        var fighter = DataManager.Current.ElementsCollection.GetElement(FighterClassId);
        if (fighter is null)
        {
            _output.WriteLine("[SKIP] Fighter is not available in the loaded content.");
            return;
        }

        CharacterManager.Current.RegisterElement(fighter);
        CharacterManager.Current.ReprocessCharacter();

        var selection = FindSupportedElementSelection("Proficiency");
        if (selection is null)
        {
            _output.WriteLine("[SKIP] No evaluable Fighter proficiency selection rule is available in the loaded content.");
            return;
        }

        handler.SetRegisteredElement(selection.Value.Rule, selection.Value.Element.Id);
        CharacterManager.Current.ReprocessCharacter();

        byte[] bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_proficiency_selection_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);

            var reloadedHandler = new TestSelectionRuleExpanderHandler();
            SelectionRuleExpanderContext.Current = reloadedHandler;
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloadedProficiencyIds = CharacterManager.Current.SelectionRules
                .Where(rule => rule.Attributes.Type.Equals("Proficiency", StringComparison.OrdinalIgnoreCase))
                .SelectMany(rule => Enumerable.Range(1, Math.Max(1, rule.Attributes.Number))
                    .Select(number => reloadedHandler.GetRegisteredElement(rule, number) as ElementBase))
                .Where(proficiency => proficiency is not null)
                .Select(proficiency => proficiency!.Id)
                .ToList();

            reloadedProficiencyIds.Should().Contain(selection.Value.Element.Id);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task RepeatableInvocation_UsesDistinctInstancesAndSurvivesSaveReload()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await CharacterManager.Current.New(initializeFirstLevel: true);

        var warlock = DataManager.Current.ElementsCollection.GetElement(WarlockClassId);
        var agonizingBlast = DataManager.Current.ElementsCollection.GetElement(AgonizingBlastId);
        if (warlock is null || agonizingBlast is null)
        {
            _output.WriteLine("[SKIP] 2024 Warlock repeatable invocation content is not available.");
            return;
        }

        agonizingBlast.AllowDuplicate.Should().BeTrue(
            "Agonizing Blast is a source-marked repeatable Eldritch Invocation");

        CharacterManager.Current.RegisterElement(warlock);
        CharacterManager.Current.ReprocessCharacter();

        var cantripRule = CharacterManager.Current.SelectionRules.FirstOrDefault(rule =>
            rule.Attributes.Type.Equals("Spell", StringComparison.OrdinalIgnoreCase) &&
            rule.Attributes.Name.Equals("Cantrip (Warlock)", StringComparison.OrdinalIgnoreCase) &&
            rule.Attributes.RequiredLevel == 1);
        if (cantripRule is null || DataManager.Current.ElementsCollection.GetElement(EldritchBlastId) is null)
        {
            _output.WriteLine("[SKIP] Warlock cantrip selection data is not available.");
            return;
        }

        handler.SetRegisteredElement(cantripRule, EldritchBlastId, number: 1);
        CharacterManager.Current.ReprocessCharacter();
        CharacterManager.Current.LevelUpMain();
        CharacterManager.Current.ReprocessCharacter();

        var invocationRule = FindWarlockLevelTwoInvocationRule();
        handler.SetRegisteredElement(invocationRule, AgonizingBlastId, number: 1);
        handler.SetRegisteredElement(invocationRule, AgonizingBlastId, number: 2);
        CharacterManager.Current.ReprocessCharacter();

        var first = handler.GetRegisteredElement(invocationRule, 1) as ElementBase;
        var second = handler.GetRegisteredElement(invocationRule, 2) as ElementBase;
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.Id.Should().Be(AgonizingBlastId);
        second!.Id.Should().Be(AgonizingBlastId);
        second.Should().NotBeSameAs(first);
        CharacterManager.Current.GetElements().Count(element => element.Id == AgonizingBlastId).Should().Be(2);

        byte[] bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_repeatable_selection_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);

            var reloadedHandler = new TestSelectionRuleExpanderHandler();
            SelectionRuleExpanderContext.Current = reloadedHandler;
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloadedRule = FindWarlockLevelTwoInvocationRule();
            var reloadedFirst = reloadedHandler.GetRegisteredElement(reloadedRule, 1) as ElementBase;
            var reloadedSecond = reloadedHandler.GetRegisteredElement(reloadedRule, 2) as ElementBase;
            reloadedFirst.Should().NotBeNull();
            reloadedSecond.Should().NotBeNull();
            reloadedFirst!.Id.Should().Be(AgonizingBlastId);
            reloadedSecond!.Id.Should().Be(AgonizingBlastId);
            reloadedSecond.Should().NotBeSameAs(reloadedFirst);
            CharacterManager.Current.GetElements().Count(element => element.Id == AgonizingBlastId).Should().Be(2);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private async Task<TestSelectionRuleExpanderHandler> CreateHumanLanguageCharacterAsync()
    {
        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        await CharacterManager.Current.New(initializeFirstLevel: true);

        var customLanguageOption = DataManager.Current.ElementsCollection.GetElement(CustomizedLanguageOptionId);
        var human = DataManager.Current.ElementsCollection.GetElement(HumanRaceId);
        var elvish = DataManager.Current.ElementsCollection.GetElement(ElvishLanguageId);
        var dwarvish = DataManager.Current.ElementsCollection.GetElement(DwarvishLanguageId);

        if (customLanguageOption is null || human is null || elvish is null || dwarvish is null)
        {
            throw new InvalidOperationException(
                "The shared content fixture does not contain the expected Human language-selection elements.");
        }

        CharacterManager.Current.RegisterElement(customLanguageOption);
        CharacterManager.Current.RegisterElement(human);
        return handler;
    }

    private static SelectRule FindHumanLanguageRule() =>
        CharacterManager.Current.SelectionRules.Should().ContainSingle(rule =>
            rule.Attributes.Type == "Language" &&
            rule.ElementHeader != null &&
            rule.ElementHeader.Id == HumanRaceId).Subject;

    private static (SelectRule Rule, ElementBase Element)? FindSupportedElementSelection(string type)
    {
        var allElements = DataManager.Current.ElementsCollection
            .Where(element => element.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var rule in CharacterManager.Current.SelectionRules
                     .Where(candidate =>
                         candidate.Attributes.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
                         candidate.Attributes.RequiredLevel <= CharacterManager.Current.Character.Level))
        {
            try
            {
                var interpreter = new ExpressionInterpreter();
                interpreter.InitializeWithSelectionRule(rule);
                var candidates = rule.Attributes.ContainsSupports()
                    ? interpreter.EvaluateSupportsExpression<ElementBase>(
                        rule.Attributes.Supports,
                        allElements,
                        rule.Attributes.SupportsElementIdRange())
                    : allElements;
                var element = candidates.FirstOrDefault();
                if (element is not null)
                    return (rule, element);
            }
            catch
            {
                // Some content uses spellcasting macros that are intentionally handled by
                // the App fallback. Continue until a directly evaluable rule is found.
            }
        }

        return null;
    }

    private static SelectRule FindWarlockLevelTwoInvocationRule() =>
        CharacterManager.Current.SelectionRules.Should().ContainSingle(rule =>
            rule.Attributes.Type.Equals("Class Feature", StringComparison.OrdinalIgnoreCase) &&
            rule.Attributes.Name.Equals("Eldritch Invocation (Warlock 2)", StringComparison.OrdinalIgnoreCase) &&
            rule.Attributes.RequiredLevel == 2 &&
            rule.Attributes.Number == 2).Subject;
}
