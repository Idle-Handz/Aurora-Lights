using Aurora.App.Services;
using Aurora.Content;
using Aurora.PdfImport;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class MulticlassImportTests
{
    [Theory]
    [InlineData("archived")]
    [InlineData("legacy")]
    public async Task SuffixedClassOwnershipKeepsSubclassThroughImportAndReload(string suffix)
    {
        await WithCatalog(async (directory, database) =>
        {
            var sheet = DndBeyondJsonImporter.ParseJson($$$"""
                {"name":"Imported hero","race":{"fullName":"Human"},"classes":[
                  {"id":11,"definition":{"name":"Fighter"},"level":1,"isStartingClass":true},
                  {"id":12,"definition":{"name":"Wizard ({{{suffix}}})"},"level":5,
                   "subclassDefinition":{"name":"Evocation"}}]}
                """);
            var result = new CharacterInferenceEngine(database).Infer(sheet);
            result.Classes[1].Name.Should().Be($"Wizard ({suffix})", "the exported display name must remain intact");
            foreach (var element in DataManager.Current.ElementsCollection.Where(element => element.Type is "Class" or "Multiclass"))
                element.SpellcastingInformation = new SpellcastingInformation(element.ElementHeader)
                {
                    Name = element.Name, AbilityName = "Intelligence", Prepare = true,
                    InitialSupportedSpellsExpression = new SpellcastingInformation.SpellcastingList(element.Name)
                };
            var spell = Parse("<element name='Fixture spell' type='Spell' source='Test' id='ID_IMPORT_PREPARED_SPELL' />")
                .Construct<Spell>();
            spell.Level = 1;
            DataManager.Current.ElementsCollection.Add(spell);
            sheet.Spells.Add(new ParsedSpell { Name = spell.Name, Source = $"Wizard ({suffix})", Level = 1, IsPrepared = true });
            result.Elements.Add(new ResolvedElement
            {
                TypeName = "Spell", Name = spell.Name, AuroraId = spell.Id, OwnerClass = $"Wizard ({suffix})"
            });
            string output = Path.Combine(directory, "suffixed-import.dnd5e");

            await new PdfImportService(null!).WriteCharacterFileAsync(sheet, result, output);
            AssertDistribution(fighterStarts: true);
            var spellHandler = (TestSpellHandler)SpellcastingSectionContext.Current!;
            spellHandler.GetPreparedIds("Wizard").Should().ContainSingle().Which.Should().Be(spell.Id);
            spellHandler.GetPreparedIds("Fighter").Should().BeEmpty("an exported Wizard spell must not migrate to another class");

            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var loaded = await new Builder.Presentation.Models.CharacterFile(output).Load();
            loaded.Success.Should().BeTrue();
            loaded.Missing.Should().BeEmpty();
            AssertDistribution(fighterStarts: true);
        });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ClassDistributionStartingClassAndSubclassesSurviveImportAndReload(bool startingFirst, bool fighterStarts)
    {
        await WithCatalog(async (directory, database) =>
        {
            string fighter = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = 11, definition = new { name = "Fighter" }, level = fighterStarts ? 1 : 4,
                isStartingClass = fighterStarts, subclassDefinition = fighterStarts ? null : new { name = "Champion" }
            });
            string wizard = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = 12, definition = new { name = "Wizard" }, level = fighterStarts ? 5 : 2,
                isStartingClass = !fighterStarts, subclassDefinition = new { name = "Evocation" }
            });
            string starting = fighterStarts ? fighter : wizard;
            string secondary = fighterStarts ? wizard : fighter;
            string classes = startingFirst ? starting + "," + secondary : secondary + "," + starting;
            var sheet = DndBeyondJsonImporter.ParseJson("{\"name\":\"Imported hero\",\"race\":{\"fullName\":\"Human\"},\"classes\":[" + classes + "]}");
            var result = new CharacterInferenceEngine(database).Infer(sheet);
            result.Level.Should().Be(6);
            result.Classes[0].Name.Should().Be(fighterStarts ? "Fighter" : "Wizard");
            result.Elements.Should().Contain(element => element.TypeName == "Archetype"
                && element.OwnerClass == "Wizard" && element.AuroraId == "ID_IMPORT_EVOCATION");
            string output = Path.Combine(directory, "import.dnd5e");
            await new PdfImportService(null!).WriteCharacterFileAsync(sheet, result, output);
            AssertDistribution(fighterStarts);

            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var loaded = await new Builder.Presentation.Models.CharacterFile(output).Load();
            loaded.Success.Should().BeTrue();
            loaded.Missing.Should().BeEmpty();
            AssertDistribution(fighterStarts);
        });
    }

    [Fact]
    public async Task UnresolvedSubclassCannotReplaceExistingCharacterOrLeaveCommittedPortrait()
    {
        await WithCatalog(async (directory, database) =>
        {
            var sheet = DndBeyondJsonImporter.ParseJson("""
                {"name":"Hero","race":{"fullName":"Human"},"classes":[
                {"definition":{"name":"Fighter"},"level":1,"isStartingClass":true},
                {"definition":{"name":"Wizard"},"level":5,"subclassDefinition":{"name":"Missing subclass"}}]}
                """);
            using var portrait = await StagedImportPortrait.CreateAsync([1, 2, 3], ".png");
            sheet.StagedPortrait = portrait;
            sheet.PortraitLocalPath = portrait.Path;
            var result = new CharacterInferenceEngine(database).Infer(sheet);
            string output = Path.Combine(directory, "existing.dnd5e");
            File.WriteAllText(output, "existing character bytes");
            Func<Task> import = () => new PdfImportService(null!).WriteCharacterFileAsync(sheet, result, output);
            await import.Should().ThrowAsync<InvalidOperationException>().WithMessage("*subclass*");
            File.ReadAllText(output).Should().Be("existing character bytes");
            sheet.PortraitLocalPath.Should().Be(portrait.Path);
            File.Exists(portrait.Path).Should().BeTrue("the preview remains retryable");
            Directory.GetFiles(directory, ".aurora-import-*.dnd5e").Should().BeEmpty();
        });
    }

    private static void AssertDistribution(bool fighterStarts)
    {
        var cm = CharacterManager.Current;
        cm.Character.Level.Should().Be(6);
        var main = cm.ClassProgressionManagers.Single(progression => progression.IsMainClass);
        main.ClassElement.Name.Should().Be(fighterStarts ? "Fighter" : "Wizard");
        main.ProgressionLevel.Should().Be(fighterStarts ? 1 : 2);
        var other = cm.ClassProgressionManagers.Single(progression => progression.IsMulticlass);
        other.ClassElement.Name.Should().Be(fighterStarts ? "Wizard" : "Fighter");
        other.ProgressionLevel.Should().Be(fighterStarts ? 5 : 4);
        cm.GetElements().Should().Contain(element => element.Id == "ID_IMPORT_EVOCATION");
        if (!fighterStarts) cm.GetElements().Should().Contain(element => element.Id == "ID_IMPORT_CHAMPION");
        foreach (var progression in cm.ClassProgressionManagers)
        {
            string expected = progression.ClassElement.Name == "Wizard" ? "ID_IMPORT_EVOCATION" : "ID_IMPORT_CHAMPION";
            if (progression.ProgressionLevel >= (progression.ClassElement.Name == "Wizard" ? 2 : 3))
                progression.GetElements().Should().Contain(element => element.Id == expected);
        }
    }

    private static async Task WithCatalog(Func<string, string, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var oldSelections = SelectionRuleExpanderContext.Current;
        var oldSpells = SpellcastingSectionContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        string directory = Path.Combine(Path.GetTempPath(), "Aurora.Tests", $"multiclass-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var cm = CharacterManager.Current;
        try
        {
            await cm.New(false);
            catalog.Clear();
            var document = new XmlDocument();
            document.LoadXml("""
                <elements>
                  <element name="Human" type="Race" source="Test" id="ID_IMPORT_HUMAN" />
                  <element name="Fighter" type="Class" source="Test" id="ID_IMPORT_FIGHTER"><rules><select type="Archetype" name="Fighter subclass" supports="Fighter" level="3" /></rules></element>
                  <element name="Wizard" type="Class" source="Test" id="ID_IMPORT_WIZARD"><rules><select type="Archetype" name="Wizard subclass" supports="Wizard" level="2" /></rules></element>
                  <element name="Fighter" type="Multiclass" source="Test" id="ID_IMPORT_MC_FIGHTER"><rules><select type="Archetype" name="Fighter subclass" supports="Fighter" level="3" /></rules></element>
                  <element name="Wizard" type="Multiclass" source="Test" id="ID_IMPORT_MC_WIZARD"><rules><select type="Archetype" name="Wizard subclass" supports="Wizard" level="2" /></rules></element>
                  <element name="Champion" type="Archetype" source="Test" id="ID_IMPORT_CHAMPION"><supports>Fighter</supports></element>
                  <element name="Evocation" type="Archetype" source="Test" id="ID_IMPORT_EVOCATION"><supports>Wizard</supports></element>
                </elements>
                """);
            foreach (XmlElement node in document.DocumentElement!.ChildNodes)
            {
                var element = new ElementParser().ParseElement(node);
                ElementBase typed = element.Type switch
                {
                    "Class" => element.Construct<Class>(), "Multiclass" => element.Construct<Multiclass>(),
                    "Race" => element.Construct<Race>(), "Archetype" => element.Construct<Archetype>(), _ => element
                };
                if (typed is Class classElement) classElement.HitDice = "d8";
                catalog.Add(typed);
            }
            document.Save(Path.Combine(directory, "catalog.xml"));
            string database = Path.Combine(directory, "catalog.sqlite");
            await ContentImport.ImportAsync(directory, database);
            for (int number = 1; number <= 20; number++)
            {
                string rules = number == 1 ? "<select type='Race' name='Race'/><select type='Class' name='Class'/>"
                    : $"<select type='Multiclass' name='Multiclass' level='{number}' requirements='ID_INTERNAL_MULTICLASS_LEVEL_{number}'/>";
                var level = Parse($"<element name='{number}' type='Level' source='Internal' id='ID_IMPORT_LEVEL_{number}'><rules>{rules}</rules></element>").Construct<LevelElement>();
                level.Level = number;
                level.RequiredExperience = (number - 1) * 300;
                level.ElementSetters.Add(new("Level", number.ToString()));
                catalog.Add(level);
                catalog.Add(Parse($"<element name='Multiclass level {number}' type='Grants' source='Internal' id='ID_INTERNAL_MULTICLASS_LEVEL_{number}'/>").Construct<Grants>());
            }
            string optionId = Builder.Data.Strings.InternalOptions.AllowMulticlassing;
            catalog.Add(Parse($"<element name='Multiclassing' type='Option' source='Internal' id='{optionId}'/>").Construct<Option>());
            await test(directory, database);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await cm.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SelectionRuleExpanderContext.Current = oldSelections;
            SpellcastingSectionContext.Current = oldSpells;
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    private static ElementBase Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return new ElementParser().ParseElement(document.DocumentElement!);
    }
}
