using Aurora.Content;
using Aurora.Content.Preparation;
using Aurora.PdfImport;
using Aurora.Reflections.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class BundledCharacterImportTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
    private string _previousRoot = "";
    private string[] _previousAdditional = [];
    private string Custom => Path.Combine(_root, "custom");
    private string Database => Path.Combine(Custom, ContentDatabaseService.DatabaseFileName);

    public async Task InitializeAsync()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        _previousRoot = settings.DocumentsRootDirectory;
        _previousAdditional = settings.AdditionalCustomDirectories.ToArray();
        Directory.CreateDirectory(Custom);
        settings.DocumentsRootDirectory = _root;
        settings.AdditionalCustomDirectories.Clear();
        DataManager.Current.InitializeDirectories();
        DataManager.Current.UserDocumentsCustomElementsDirectory.Should().Be(Custom);
        await ContentImport.ImportAsync(Custom, Database);
    }

    [Fact]
    public async Task FreshInstallJsonImportFindsBundledClericAndSpellsAndReportsOptionalContent()
    {
        string json = Path.Combine(_root, "character.json");
        await File.WriteAllTextAsync(json, """
            {"name":"Tester","race":{"fullName":"Variant Human"},
             "classes":[{"id":11,"definition":{"name":"Cleric"},"level":1,"subclassDefinition":{"name":"War Domain"}}],
             "background":{"definition":{"name":"Athlete"}},
             "feats":[{"definition":{"name":"War Caster"}}],
             "classSpells":[{"characterClassId":11,"spells":[{"definition":{"name":"Guidance"}},
                                 {"definition":{"name":"Light"}},
                                 {"definition":{"name":"Heal"}},
                                 {"definition":{"name":"Dawn"}},
                                 {"definition":{"name":"Revivify"}}]}]}
            """);
        var imported = await new PdfImportService(new ContentDatabaseService()).ImportFromJsonAsync(json);
        imported.Should().NotBeNull();
        imported!.Value.Sheet.Spells.Should().HaveCount(5);
        var result = imported!.Value.Result;
        foreach (string name in new[] { "Cleric", "Guidance", "Light", "Heal", "Revivify" })
        {
            result.Elements.Should().Contain(element => element.Name == name);
            result.Warnings.Should().NotContain(warning => warning.Item == name);
        }
        result.Warnings.Select(warning => warning.Item).Should()
            .Contain(["Variant Human", "War Domain", "Athlete", "War Caster", "Dawn"]);
        using var connection = ContentDatabase.OpenReadableConnection(Database);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM elements";
        Convert.ToInt32(command.ExecuteScalar()).Should().Be(0, "bundled content is composed at runtime");
    }

    [Fact]
    public void BundledSubraceIncludesItsBundledParent()
    {
        var result = PdfImportService.CreateInferenceEngine(Database).Infer(new ParsedCharacterSheet { Species = "High Elf" });
        result.Elements.Should().Contain(element => element.TypeName == "Sub Race" && element.Name == "High Elf");
        result.Elements.Should().Contain(element => element.TypeName == "Race" && element.Name == "Elf");
        result.Warnings.Should().NotContain(warning => warning.Category == "Race");
    }

    [Fact]
    public async Task ImportHonorsEffectiveOverridesAndDoesNotRestoreUnavailableBundledIds()
    {
        await File.WriteAllTextAsync(Path.Combine(Custom, "spell.xml"), """
            <elements><element id="ID_PHB_SPELL_GUIDANCE" name="Test Guidance" type="Spell" source="Test" /></elements>
            """);
        await ContentImport.ImportAsync(Custom, Database);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO content_unavailable_elements(aurora_id, detail) VALUES ('ID_PHB_SPELL_LIGHT', 'Test conflict')";
            command.ExecuteNonQuery();
        }
        var result = PdfImportService.CreateInferenceEngine(Database).Infer(new ParsedCharacterSheet
        {
            Spells = [new ParsedSpell { Name = "Test Guidance" }, new ParsedSpell { Name = "Light" }]
        });
        result.Elements.Should().Contain(element => element.AuroraId == "ID_PHB_SPELL_GUIDANCE" && element.Name == "Test Guidance");
        result.Elements.Should().NotContain(element => element.AuroraId == "ID_PHB_SPELL_LIGHT");
        result.Warnings.Should().Contain(warning => warning.Item == "Light");
    }

    [Fact]
    public async Task EffectiveFeatGrantsKeepFreeFeatsOutOfAsiAccounting()
    {
        await File.WriteAllTextAsync(Path.Combine(Custom, "race.xml"), """
            <elements>
              <element id="ID_IMPORT_TEST_RACE" name="Test Race" type="Race" source="Test">
                <rules><grant type="Feat" id="ID_IMPORT_TEST_GIFT" /></rules>
              </element>
              <element id="ID_IMPORT_TEST_GIFT" name="Test Gift" type="Feat" source="Test" />
            </elements>
            """);
        await ContentImport.ImportAsync(Custom, Database);
        var result = PdfImportService.CreateInferenceEngine(Database).Infer(new ParsedCharacterSheet
        {
            Species = "Test Race", ClassName = "Fighter", ClassLevel = 4,
            Feats = [new ParsedFeature { Name = "Test Gift" }]
        });
        result.Warnings.Should().ContainSingle(warning => warning.Category == "ASI",
            "the granted feat does not consume the level-four ability improvement");
    }

    public Task DisposeAsync()
    {
        var settings = ApplicationContext.Current.Settings;
        settings.DocumentsRootDirectory = _previousRoot;
        settings.AdditionalCustomDirectories.Clear();
        settings.AdditionalCustomDirectories.AddRange(_previousAdditional);
        DataManager.Current.InitializeDirectories();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }
}
