using System.Xml;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.ElementParsers;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CasterSpellTransferTests
{
    public enum Learning { List, Known, Book }

    private const string Legacy = "ID_WOTC_PHB_CLASS_CLERIC";
    private const string Revised = "ID_WOTC_PHB24_CLASS_CLERIC";
    private static readonly string[] Cantrips = ["BOLSTER", "PACIFY", "WORD_OF_RADIANCE_2024", "DOUSE"];
    private static readonly string[] Prepared = ["CURE_WOUNDS_2024", "HEALING_WORD_2024", "SNOW_FORT"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EditionSwitchRetainsExactSpellChoicesThroughSaveAndReload(bool reverse) =>
        WithCharacter(async (manager, tab, handler) =>
        {
            string target = reverse ? Legacy : Revised;
            var otherProfile = new SpellcastingInformation(new ElementHeader("Other", "Feat", "Test", "OTHER_PROFILE"))
                { Name = "Other" };
            handler.SetPrepareSpell(otherProfile, "UNRELATED_PREPARATION");
            tab.Snapshot!.InventoryEquipmentText = "Keep pending notes";

            (await Switch(tab, target)).Should().BeEmpty();

            AssertSpells(tab, target, Cantrips, Prepared);
            manager.GetElements().Should().NotContain(e => e.Id == "DOMAIN_RAY" || e.Id == "DOMAIN_WIND",
                "removed subclass grants must not become ordinary class selections");
            manager.GetElements().Should().Contain(e => e.Id == "FEAT_CANTRIP");
            handler.GetPreparedIds(otherProfile).Should().ContainSingle().Which.Should().Be("UNRELATED_PREPARATION");
            tab.Snapshot!.InventoryEquipmentText.Should().Be("Keep pending notes");

            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertSpells(tab, target, Cantrips, Prepared);
            tab.Snapshot!.InventoryEquipmentText.Should().Be("Keep pending notes");
        }, reverse: reverse);

    [Fact]
    public Task SwitchRejectsIneligibleSpellsAndHonorsTheNewLimits() =>
        WithCharacter(async (manager, tab, handler) =>
        {
            var source = manager.GetSpellcastingInformations().Single(p => !p.IsExtension);
            foreach (string id in new[] { "OFF_LIST", "TOO_HIGH", "BLOCKED" })
                handler.SetPrepareSpell(source, id);
            // A smaller destination list must not reappear from the old UI snapshot on save.
            BuildService.ResnapTab(tab);
            var errors = await Switch(tab, Revised);

            errors.Should().Contain("Cleric cantrip: DOUSE");
            errors.Should().Contain("Prepared Cleric spell: OFF_LIST");
            errors.Should().Contain("Prepared Cleric spell: TOO_HIGH");
            errors.Should().Contain("Prepared Cleric spell: BLOCKED");
            errors.Should().Contain("Prepared Cleric spell: SNOW_FORT");
            AssertSpells(tab, Revised, Cantrips.Take(3), Prepared.Take(2));
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertSpells(tab, Revised, Cantrips.Take(3), Prepared.Take(2));
        }, reduced: true);

    [Fact]
    public Task SwitchingBackUsesCurrentPreparationRatherThanOldProfileState() =>
        WithCharacter(async (manager, tab, handler) =>
        {
            (await Switch(tab, Revised)).Should().BeEmpty();
            var profile = manager.GetSpellcastingInformations().Single(p => !p.IsExtension);
            handler.UnsetPrepareSpell(profile, Prepared[0]);
            handler.SetPrepareSpell(profile, "REPLACEMENT");
            BuildService.ResnapTab(tab);

            (await Switch(tab, Legacy)).Should().BeEmpty();

            AssertSpells(tab, Legacy, Cantrips, [Prepared[1], Prepared[2], "REPLACEMENT"]);
            handler.GetPreparedIds(profile).Should().BeEmpty();
        });

    [Fact]
    public Task ChangingToADifferentCasterTransfersEligibleSelections() =>
        WithCharacter(async (_, tab, _) =>
        {
            (await Switch(tab, "OTHER_CLASS")).Should().BeEmpty();
            AssertSpells(tab, "OTHER_CLASS", Cantrips, Prepared);
            tab.Snapshot!.SpellcastingSections.Single().Name.Should().Be("Other");
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertSpells(tab, "OTHER_CLASS", Cantrips, Prepared);
        });

    [Theory]
    [InlineData(Learning.List, Learning.List)]
    [InlineData(Learning.List, Learning.Known)]
    [InlineData(Learning.List, Learning.Book)]
    [InlineData(Learning.Known, Learning.List)]
    [InlineData(Learning.Known, Learning.Known)]
    [InlineData(Learning.Known, Learning.Book)]
    [InlineData(Learning.Book, Learning.List)]
    [InlineData(Learning.Book, Learning.Known)]
    [InlineData(Learning.Book, Learning.Book)]
    public Task DifferentCastingModelsPreserveChoicesThroughReload(Learning source, Learning target) =>
        WithCharacter(async (_, tab, _) =>
        {
            var errors = await Switch(tab, Revised);
            if (source == Learning.Book && target == Learning.Known)
                errors.Should().BeEquivalentTo(["Cleric spell: UNPREPARED"]);
            else
                errors.Should().BeEmpty();

            void AssertChoices()
            {
                AssertSpells(tab, Revised, Cantrips, Prepared);
                var section = tab.Snapshot!.SpellcastingSections.Single();
                section.Name.Should().Be("Destination");
                if (target != Learning.List)
                    section.SpellLevels.SelectMany(l => l.Spells).Select(s => s.Id).Should().BeEquivalentTo(
                        source == Learning.Book && target == Learning.Book ? Prepared.Concat(["UNPREPARED"]) : Prepared);
                if (source == Learning.Book && target != Learning.Known)
                    section.SpellLevels.SelectMany(l => l.Spells).Single(s => s.Id == "UNPREPARED").IsPrepared.Should().BeFalse();
            }

            AssertChoices();
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertChoices();
        }, sourceLearning: source, targetLearning: target, targetName: "Destination");

    [Fact]
    public Task KnownCasterHonorsTheNewListAndChoiceLimits() =>
        WithCharacter(async (manager, tab, handler) =>
        {
            var profile = manager.GetSpellcastingInformations().Single(p => !p.IsExtension);
            foreach (string id in new[] { "OFF_LIST", "TOO_HIGH", "BLOCKED" }) handler.SetPrepareSpell(profile, id);
            BuildService.ResnapTab(tab);

            var errors = await Switch(tab, Revised);

            errors.Should().BeEquivalentTo(new[] { "Cleric cantrip: DOUSE" }
                .Concat(new[] { "OFF_LIST", "TOO_HIGH", "BLOCKED", "SNOW_FORT" }.Select(id => $"Prepared Cleric spell: {id}")));
            AssertSpells(tab, Revised, Cantrips.Take(3), Prepared.Take(2));
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertSpells(tab, Revised, Cantrips.Take(3), Prepared.Take(2));
        }, reduced: true, targetLearning: Learning.Known, targetName: "Destination");

    [Fact]
    public Task FlexibleChoicesDoNotBlockSpellsThatHaveOnlyOneLegalSlot() =>
        WithCharacter(async (_, tab, _) =>
        {
            (await Switch(tab, Revised)).Should().BeEquivalentTo(["Prepared Cleric spell: SNOW_FORT"]);
            AssertSpells(tab, Revised, Cantrips, Prepared.Take(2));
        }, targetLearning: Learning.Known, targetName: "Destination", constrainedChoices: true);

    [Fact]
    public Task ChangingToANonCasterDoesNotRestoreOrRetainPreparationState() =>
        WithCharacter(async (manager, tab, handler) =>
        {
            var profile = manager.GetSpellcastingInformations().Single(p => !p.IsExtension);
            (await Switch(tab, "NONCASTER")).Should().BeEmpty();
            tab.Snapshot!.SpellcastingSections.Should().BeEmpty();
            handler.GetPreparedIds(profile).Should().BeEmpty();
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            tab.Snapshot!.SpellcastingSections.Should().BeEmpty();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SwitchingEitherClassPreservesMulticlassSpellOwnership(bool replaceSecondary) =>
        WithCharacter(async (manager, tab, handler) =>
        {
            const string secondaryId = "SECONDARY_CLASS";
            const string targetId = "SECONDARY_TARGET";
            foreach (string id in new[] { secondaryId, targetId })
            {
                string name = id == secondaryId ? "Other" : "OtherDestination";
                var caster = Parse(id, "Multiclass", $"""
                    <spellcasting name='{name}' ability='Wisdom' prepare='true'><list known='true'>Other</list></spellcasting>
                    <rules>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:slots:1' value='2'/>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:prepare' value='2'/>
                      <select type='Spell' name='Cantrip' supports='Other,0' spellcasting='{name}'/>
                    </rules>
                    """, name).Construct<Multiclass>();
                caster.HitDice = "d8";
                DataManager.Current.ElementsCollection.Add(caster);
            }
            var multiclassRule = manager.SelectionRules.Single(r => r.Attributes.Type == "Multiclass");
            SelectionRuleExpanderContext.Current!.SetRegisteredElement(multiclassRule, secondaryId);
            var cantripRule = manager.SelectionRules.Single(r => r.ElementHeader.Id == secondaryId && r.Attributes.Type == "Spell");
            // The same spell can belong independently to two classes.
            SelectionRuleExpanderContext.Current.SetRegisteredElement(cantripRule, Cantrips[0]);
            var secondaryProfile = manager.GetSpellcastingInformations().Single(p => p.ElementHeader.Id == secondaryId);
            handler.SetPrepareSpell(secondaryProfile, "REPLACEMENT");
            BuildService.ResnapTab(tab);
            (await BuildService.SaveTabAsync(tab)).Should().BeNull();

            var errors = replaceSecondary
                ? await BuildService.ApplySelectionAndSaveAsync(multiclassRule, targetId, 1, tab, tab.File)
                : await Switch(tab, Revised);
            errors.Should().BeEmpty();

            void AssertOwnership()
            {
                tab.Snapshot!.SpellcastingSections.Should().HaveCount(2);
                var primary = tab.Snapshot.SpellcastingSections.Single(s => s.SourceId == (replaceSecondary ? Legacy : Revised) + "_SPELLCASTING");
                primary.Cantrips.Select(s => s.Id).Should().BeEquivalentTo(replaceSecondary ? Cantrips.Concat(["DOMAIN_RAY"]) : Cantrips);
                primary.SpellLevels.SelectMany(l => l.Spells).Where(s => s.IsPrepared).Select(s => s.Id)
                    .Should().BeEquivalentTo(replaceSecondary ? Prepared.Concat(["DOMAIN_WIND"]) : Prepared);
                var secondary = tab.Snapshot.SpellcastingSections.Single(s => s.SourceId == (replaceSecondary ? targetId : secondaryId));
                secondary.Cantrips.Select(s => s.Id).Should().BeEquivalentTo([Cantrips[0]]);
                secondary.SpellLevels.SelectMany(l => l.Spells).Where(s => s.IsPrepared).Select(s => s.Id).Should().BeEquivalentTo(["REPLACEMENT"]);
            }

            AssertOwnership();
            await CharacterContext.ReloadFromDiskAsync(tab);
            BuildService.ResnapTab(tab);
            AssertOwnership();
        }, withMulticlass: true);

    private static Task<IReadOnlyList<string>> Switch(CharacterTab tab, string target) =>
        BuildService.ApplySelectionAndSaveAsync(
            CharacterManager.Current.SelectionRules.Single(r => r.Attributes.Type == "Class"), target, 1, tab, tab.File);

    private static void AssertSpells(CharacterTab tab, string classId, IEnumerable<string> cantrips, IEnumerable<string> prepared)
    {
        var section = tab.Snapshot!.SpellcastingSections.Single();
        section.SourceId.Should().Be(classId + "_SPELLCASTING");
        section.Cantrips.Select(s => s.Id).Should().BeEquivalentTo(cantrips);
        section.SpellLevels.SelectMany(l => l.Spells).Where(s => s.IsPrepared).Select(s => s.Id)
            .Should().BeEquivalentTo(prepared);
        var saved = XDocument.Load(tab.File.FilePath).Root!.Element("build")!.Element("magic")!
            .Elements("spellcasting").Single();
        saved.Element("cantrips")!.Elements("spell").Select(e => (string)e.Attribute("id")!)
            .Should().BeEquivalentTo(cantrips);
        saved.Element("spells")!.Elements("spell").Where(e => !section.IsPreparedCaster || (string?)e.Attribute("prepared") == "true")
            .Select(e => (string)e.Attribute("id")!).Should().BeEquivalentTo(prepared);
    }

    private static async Task WithCharacter(
        Func<CharacterManager, CharacterTab, MauiSpellcastingSectionHandler, Task> test,
        bool reverse = false, bool reduced = false,
        Learning sourceLearning = Learning.List, Learning targetLearning = Learning.List,
        string targetName = "Cleric", bool constrainedChoices = false, bool withMulticlass = false)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var manager = CharacterManager.Current;
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var previousSelection = SelectionRuleExpanderContext.Current;
        var previousSpells = SpellcastingSectionContext.Current;
        var selections = new TestSelectionRuleExpanderHandler();
        var spells = new MauiSpellcastingSectionHandler();
        SelectionRuleExpanderContext.Current = selections;
        SpellcastingSectionContext.Current = spells;
        string directory = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "cleric-switch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await manager.New(false);
            manager.Status.IsLoaded = false;
            catalog.Clear();
            for (int n = 1; n <= 7; n++)
            {
                string choices = n == 1 ? "<rules><select type='Class' name='Class'/></rules>"
                    : n == 2 && withMulticlass ? "<rules><select type='Multiclass' name='Additional Class' level='2'/></rules>" : "";
                var level = Parse($"ID_LEVEL_{n}", "Level", choices, n.ToString())
                    .Construct<LevelElement>();
                level.Level = n;
                level.RequiredExperience = (n - 1) * 300;
                level.ElementSetters.Add(new("Level", n.ToString()));
                catalog.Add(level);
                if (n <= 6) manager.RegisterElement(level);
            }
            string start = reverse ? Revised : Legacy;
            foreach (string id in new[] { Legacy, Revised, "OTHER_CLASS", "NONCASTER" })
            {
                string name = id == "OTHER_CLASS" ? "Other" : id == start ? "Cleric" : targetName;
                var learning = id == start ? sourceLearning : targetLearning;
                var @class = Parse(id, "Class", id == "NONCASTER" ? "" : $"<rules><grant type='Class Feature' id='{id}_SPELLCASTING'/></rules>", name).Construct<Class>();
                @class.HitDice = "d8";
                catalog.Add(@class);
                if (id == "NONCASTER") continue;
                string exclusion = reduced && id == Revised ? ",!LegacyOnly" : "";
                int limit = reduced && id == Revised ? 2 : 9;
                string list = learning == Learning.Book ? "" : $"<list known='{(learning == Learning.List ? "true" : "false")}'>{name}</list>";
                string supports = learning == Learning.Book ? name : "$(spellcasting:list)";
                int known = learning == Learning.Book ? 4 : reduced && id == Revised ? 2 : 3;
                bool constrained = constrainedChoices && id != start;
                string learned = learning == Learning.List ? "" : $"""
                    <select type='Spell' name='Learned Spell' supports='{supports},$(spellcasting:slots){exclusion}' number='{(constrained ? 1 : known)}' spellcasting='{name}'/>
                    """;
                if (constrained)
                    learned += $"<select type='Spell' name='Restricted Spell' supports='{supports},1,RestrictedChoice' spellcasting='{name}'/>";
                catalog.Add(Parse(id + "_SPELLCASTING", "Class Feature", $"""
                    <spellcasting name='{name}' ability='Wisdom' prepare='{(learning == Learning.Known ? "false" : "true")}'>{list}</spellcasting>
                    <rules>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:slots:1' value='4'/>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:slots:2' value='3'/>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:slots:3' value='3'/>
                      <stat name='{name.ToLowerInvariant()}:spellcasting:prepare' value='{limit}'/>
                      <select type='Spell' name='Cantrip' supports='{supports},0{exclusion}' number='3' spellcasting='{name}'/>
                      <select type='Spell' name='Cantrip' supports='{supports},0{exclusion}' level='4' spellcasting='{name}'/>
                      {learned}
                    </rules>
                    """).Construct<ClassFeature>());
            }
            string lists = $"Cleric,{targetName},Other";
            foreach (string id in Cantrips) catalog.Add(Spell(id, 0, $"{lists},0" + (id == "DOUSE" ? ",LegacyOnly" : "")));
            foreach (string id in Prepared.Concat(["REPLACEMENT", "BLOCKED", "UNPREPARED"]))
                catalog.Add(Spell(id, 1, $"{lists},1" + (id == Prepared[0] ? ",RestrictedChoice" : "")));
            catalog.Add(Spell("OFF_LIST", 1, "Wizard,1"));
            catalog.Add(Spell("TOO_HIGH", 9, "Cleric,9"));
            catalog.Add(Spell("DOMAIN_RAY", 0, "Wizard,0"));
            catalog.Add(Spell("DOMAIN_WIND", 1, "Cleric,1"));
            catalog.Add(Spell("FEAT_CANTRIP", 0, "Cleric,0"));
            catalog.Add(Parse("FEAT_OWNER", "Feat", "<rules><grant type='Spell' id='FEAT_CANTRIP'/></rules>").Construct<Feat>());
            selections.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), start);
            var rules = manager.SelectionRules.Where(r => r.Attributes.Name == "Cantrip").OrderBy(r => r.Attributes.RequiredLevel).ToArray();
            for (int n = 0; n < 3; n++) selections.SetRegisteredElement(rules[0], Cantrips[n], n + 1);
            selections.SetRegisteredElement(rules[1], Cantrips[3]);
            if (sourceLearning != Learning.List)
            {
                var learned = manager.SelectionRules.Single(r => r.Attributes.Name == "Learned Spell");
                for (int n = 0; n < Prepared.Length; n++) selections.SetRegisteredElement(learned, Prepared[n], n + 1);
                if (sourceLearning == Learning.Book) selections.SetRegisteredElement(learned, "UNPREPARED", 4);
            }
            var profile = manager.GetSpellcastingInformations().Single(p => !p.IsExtension);
            if (sourceLearning != Learning.Known)
                foreach (string id in Prepared) spells.SetPrepareSpell(profile, id);
            // A class-owned domain choice and its automatic spells disappear with the old class.
            var domain = Parse("DOMAIN", "Archetype", "<rules><grant type='Spell' id='DOMAIN_RAY' spellcasting='Cleric'/><grant type='Spell' id='DOMAIN_WIND' spellcasting='Cleric' prepared='true'/></rules>").Construct<Archetype>();
            catalog.Add(domain);
            var domainRule = new SelectRule(catalog.GetElement(start).ElementHeader);
            domainRule.Attributes.Type = "Archetype";
            domainRule.Attributes.Name = "Domain";
            catalog.GetElement(start).Rules.Add(domainRule);
            manager.ReprocessCharacter();
            selections.SetRegisteredElement(domainRule, domain.Id);
            spells.SetPrepareSpell(profile, "DOMAIN_WIND");
            manager.RegisterElement(catalog.GetElement("FEAT_OWNER"));

            manager.Character.Name = "Spell transfer test";
            var file = new CharacterFile(Path.Combine(directory, "character.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file) { Character = manager.Character, Snapshot = CharacterSnapshot.From(manager.Character) };
            CharacterContext.ClaimAfterLoad(tab);
            // Explicitly restricted content must not survive the migration.
            if (reduced)
                manager.SourcesManager.RestrictedSources.Add(new Builder.Presentation.Models.Sources.SourceItem(
                    Parse("BLOCKED_SOURCE", "Source", "").Construct<Source>())
                {
                    Elements = [catalog.GetElement("BLOCKED").ElementHeader]
                });
            await test(manager, tab, spells);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            manager.Status.IsLoaded = false;
            await manager.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SelectionRuleExpanderContext.Current = previousSelection;
            SpellcastingSectionContext.Current = previousSpells;
            Directory.Delete(directory, true);
        }
    }

    private static ElementBase Parse(string id, string type, string body, string? name = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<element id='{id}' name='{name ?? id}' type='{type}' source='Internal'><description><p>{id}</p></description>{body}</element>");
        return new ElementParser().ParseElement(doc.DocumentElement!);
    }

    private static ElementBase Spell(string id, int level, string supports)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<element id='{id}' name='{id}' type='Spell' source='Internal'><supports>{supports}</supports><setters><set name='level'>{level}</set><set name='school'>Evocation</set><set name='time'>1 action</set><set name='range'>60 feet</set><set name='duration'>Instantaneous</set></setters></element>");
        return new SpellElementParser().ParseElement(doc.DocumentElement!);
    }
}
