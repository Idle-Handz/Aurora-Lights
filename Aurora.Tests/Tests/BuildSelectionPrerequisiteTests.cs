using System.Xml;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class BuildSelectionPrerequisiteTests : IAsyncLifetime
{
    private Action _restore = () => { };
    private CharacterManager Manager => CharacterManager.Current;
    private SelectRule Rule(string name) => Manager.SelectionRules.Single(r => r.Attributes.Name == name);
    private ElementBase Element(string id) => DataManager.Current.ElementsCollection.GetElement("ID_TEST_" + id)!;
    private IReadOnlyList<string> Options(string name) => BuildService.GetOptions(Rule(name)).Select(o => o.Id).ToArray();
    private void Select(string name, string id)
    {
        SelectionRuleExpanderContext.Current.SetRegisteredElement(Rule(name), "ID_TEST_" + id);
        Manager.ReprocessCharacter();
    }
    private CharacterTab Tab()
    {
        var tab = new CharacterTab(new CharacterFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dnd5e")))
            { Character = Manager.Character };
        CharacterContext.ClaimAfterLoad(tab);
        BuildService.ResnapTab(tab);
        return tab;
    }

    public async Task InitializeAsync()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var original = catalog.ToArray();
        var handler = SelectionRuleExpanderContext.Current;
        var spells = SpellcastingSectionContext.Current;
        _restore = () =>
        {
            catalog.Clear(); foreach (var element in original) catalog.Add(element);
            SelectionRuleExpanderContext.Current = handler;
            SpellcastingSectionContext.Current = spells;
        };
        await CharacterContext.InvalidateAsync();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        await Manager.New(false);
        catalog.Clear();
        var document = new XmlDocument(); document.LoadXml(Content);
        var defaultParser = new ElementParser();
        var parsers = ElementParserFactory.GetParsers().ToArray();
        foreach (XmlNode node in document.DocumentElement!.SelectNodes("element")!)
        {
            var header = defaultParser.ParseElementHeader(node);
            catalog.Add((parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? defaultParser).ParseElement(node));
        }
        for (int number = 1; number <= 20; number++)
        {
            var level = new Builder.Data.Elements.LevelElement
            {
                ElementHeader = new ElementHeader("Level " + number, "Level", "Test", "ID_TEST_LEVEL_" + number),
                Level = number, RequiredExperience = (number - 1) * 1000
            };
            level.ElementSetters.Add(new ElementSetters.Setter("Level", number.ToString()));
            catalog.Add(level);
        }
        Manager.RegisterElement(Element("LEVEL_1"));
        Manager.RegisterElement(Element("OWNER"));
        Manager.ReprocessCharacter();
    }

    public async Task DisposeAsync()
    {
        await CharacterContext.InvalidateAsync();
        try { await Manager.New(false); }
        finally { _restore(); }
    }

    [Fact]
    public void RacialFeatsAreFilteredBeforeTheRacialFollowupIsSelected()
    {
        Select("Race", "RACE_HUMAN");
        SelectionRuleExpanderContext.Current.GetRegisteredElement(Rule("Heritage")).Should().BeNull();
        Options("Feat").Should().BeEquivalentTo("ID_TEST_FEAT_GENERAL", "ID_TEST_FEAT_HUMAN");
        Select("Heritage", "HERITAGE_HUMAN");
        Options("Feat").Should().BeEquivalentTo("ID_TEST_FEAT_GENERAL", "ID_TEST_FEAT_HUMAN", "ID_TEST_FEAT_HERITAGE");
        Select("Race", "RACE_ELF");
        Options("Feat").Should().BeEquivalentTo("ID_TEST_FEAT_GENERAL", "ID_TEST_FEAT_ELF");
    }

    [Fact]
    public void ExpertiseOnlyOffersSkillsWithAnOwnedProficiencyAndRefreshesWhenItChanges()
    {
        Options("Expertise").Should().BeEmpty();
        Select("Skill", "SKILL_ARCANA");
        Options("Expertise").Should().Equal("ID_TEST_EXPERTISE_ARCANA");
        Select("Skill", "SKILL_PERCEPTION");
        Options("Expertise").Should().Equal("ID_TEST_EXPERTISE_PERCEPTION");
    }

    [Theory]
    [InlineData("Feat", "FEAT_ELF")]
    [InlineData("Expertise", "EXPERTISE_ARCANA")]
    public async Task ApplyingAnIneligibleChoiceIsRejectedWithoutChangingTheCharacter(string ruleName, string candidate)
    {
        Select("Race", "RACE_HUMAN");
        var tab = Tab();
        var before = Manager.GetElements().Select(e => e.Id).ToArray();
        var result = await BuildService.ApplySelectionAndSaveAsync(Rule(ruleName), "ID_TEST_" + candidate, 1, tab, tab.File, saveToFile: false);
        result.Should().Contain(message => message.StartsWith("[Error:"));
        SelectionRuleExpanderContext.Current.GetRegisteredElement(Rule(ruleName)).Should().BeNull();
        Manager.GetElements().Select(e => e.Id).Should().BeEquivalentTo(before);
    }

    [Fact]
    public void AbilityPrerequisitesUseTheCurrentFinalScore()
    {
        var feat = new ElementBase("Clever", "Feat", "Test", "ID_TEST_FEAT_CLEVER") { Requirements = "[int:13]" };
        DataManager.Current.ElementsCollection.Add(feat);
        Manager.Character.Abilities.Intelligence.BaseScore = 12;
        Manager.ReprocessCharacter();
        Options("Feat").Should().NotContain(feat.Id);
        Manager.Character.Abilities.Intelligence.FinalScore.Should().Be(12);
        Manager.RegisterElement(Element("ABILITY_BONUS"));
        Manager.ReprocessCharacter();
        Manager.Character.Abilities.Intelligence.FinalScore.Should().Be(13);
        Options("Feat").Should().Contain(feat.Id);
        Manager.UnregisterElement(Element("ABILITY_BONUS"));
        Manager.ReprocessCharacter();
        Options("Feat").Should().NotContain(feat.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Recovered && (")]
    public void FallbackOptionsMustMeetPrerequisitesToo(string supports)
    {
        var rule = new SelectRule(Element("OWNER").ElementHeader);
        rule.Attributes.Type = "Recovered Option"; rule.Attributes.Supports = supports;
        var allowed = new ElementBase("Allowed", rule.Attributes.Type, "Test", "ID_TEST_RECOVERED_ALLOWED");
        var blocked = new ElementBase("Blocked", rule.Attributes.Type, "Test", "ID_TEST_RECOVERED_BLOCKED") { Requirements = "ID_TEST_RACE_ELF" };
        var options = BuildSelectionOptionQueryService.Query(rule, hostSettings: new()
        {
            ElementFallbackProvider = _ => new[] { allowed, blocked }
        });
        options.Select(o => o.Id).Should().Equal(allowed.Id);
    }

    [Fact]
    public void MalformedPrerequisitesDoNotExposeTheCandidateOrHideOtherOptions()
    {
        Element("FEAT_ELF").Requirements = "ID_TEST_RACE_ELF && (";
        Options("Feat").Should().Equal("ID_TEST_FEAT_GENERAL");
    }

    [Theory]
    [InlineData("Race", "RACE_HUMAN", "RACE_ELF", "Feat", "FEAT_HUMAN")]
    [InlineData("Skill", "SKILL_ARCANA", "SKILL_PERCEPTION", "Expertise", "EXPERTISE_ARCANA")]
    public async Task LosingTheOnlyEligibleOptionClearsTheOldChoice(string prerequisiteRule, string original, string replacement, string choiceRule, string choice)
    {
        Select(prerequisiteRule, original);
        Element(choice).Supports.Add("Only candidate");
        Rule(choiceRule).Attributes.Supports = "Only candidate";
        Select(choiceRule, choice);
        var tab = Tab();
        var result = await BuildService.ApplySelectionAndSaveAsync(Rule(prerequisiteRule), "ID_TEST_" + replacement, 1, tab, tab.File, saveToFile: false);
        result.Should().NotContain(message => message.StartsWith("[Error:"));
        result.Should().Contain(choiceRule);
        SelectionRuleExpanderContext.Current.GetRegisteredElement(Rule(choiceRule)).Should().BeNull();
        Manager.GetElements().Should().NotContain(e => e.Id == "ID_TEST_" + choice);
    }

    [Fact]
    public void EquivalentFirearmProficienciesStillQualifyInThePicker()
    {
        var proficiency = new ElementBase("Firearms", "Proficiency", "Test", SelectionRequirementCompatibility.FirearmsProficiencyId);
        var feat = new ElementBase("Firearm training", "Feat", "Test", "ID_TEST_FEAT_FIREARMS")
            { Requirements = SelectionRequirementCompatibility.Pistol2024ProficiencyId };
        DataManager.Current.ElementsCollection.Add(proficiency); DataManager.Current.ElementsCollection.Add(feat);
        Options("Feat").Should().NotContain(feat.Id);
        Manager.RegisterElement(proficiency); Manager.ReprocessCharacter();
        Options("Feat").Should().Contain(feat.Id);
    }

    private const string Content = """
        <elements>
          <element name="Choices" type="Class Feature" source="Test" id="ID_TEST_OWNER">
            <rules>
              <stat name="strength:max" value="20" /><stat name="dexterity:max" value="20" />
              <stat name="constitution:max" value="20" /><stat name="intelligence:max" value="20" />
              <stat name="wisdom:max" value="20" /><stat name="charisma:max" value="20" />
              <select type="Race" name="Race" />
              <select type="Feat" name="Feat" />
              <select type="Proficiency" name="Skill" supports="Skill" />
              <select type="Proficiency" name="Expertise" supports="Expertise" />
            </rules>
          </element>
          <element name="Human" type="Race" source="Test" id="ID_TEST_RACE_HUMAN">
            <rules><select type="Racial Trait" name="Heritage" supports="Heritage" /></rules>
          </element>
          <element name="Elf" type="Race" source="Test" id="ID_TEST_RACE_ELF" />
          <element name="Human heritage" type="Racial Trait" source="Test" id="ID_TEST_HERITAGE_HUMAN"><supports>Heritage</supports></element>
          <element name="Intelligence bonus" type="Proficiency" source="Test" id="ID_TEST_ABILITY_BONUS"><rules><stat name="intelligence" value="1" /></rules></element>
          <element name="General feat" type="Feat" source="Test" id="ID_TEST_FEAT_GENERAL" />
          <element name="Human feat" type="Feat" source="Test" id="ID_TEST_FEAT_HUMAN"><requirements>ID_TEST_RACE_HUMAN</requirements></element>
          <element name="Elf feat" type="Feat" source="Test" id="ID_TEST_FEAT_ELF"><requirements>ID_TEST_RACE_ELF</requirements></element>
          <element name="Heritage feat" type="Feat" source="Test" id="ID_TEST_FEAT_HERITAGE"><requirements>ID_TEST_HERITAGE_HUMAN</requirements></element>
          <element name="Arcana" type="Proficiency" source="Test" id="ID_TEST_SKILL_ARCANA"><supports>Skill</supports></element>
          <element name="Perception" type="Proficiency" source="Test" id="ID_TEST_SKILL_PERCEPTION"><supports>Skill</supports></element>
          <element name="Arcana expertise" type="Proficiency" source="Test" id="ID_TEST_EXPERTISE_ARCANA"><supports>Expertise</supports><requirements>ID_TEST_SKILL_ARCANA</requirements></element>
          <element name="Perception expertise" type="Proficiency" source="Test" id="ID_TEST_EXPERTISE_PERCEPTION"><supports>Expertise</supports><requirements>ID_TEST_SKILL_PERCEPTION</requirements></element>
        </elements>
        """;
}
