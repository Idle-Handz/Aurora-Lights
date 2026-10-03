using Aurora.Content;
using Aurora.PdfImport;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class PdfInferenceCatalogTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "content.sqlite");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "catalog.xml"), """
            <elements>
              <element name="Catalog Feat" type="Feat" source="Test Book" id="ID_PDF_CATALOG_FEAT" />
              <element name="Catalog Elf" type="Race" source="Test Book" id="ID_PDF_CATALOG_ELF" />
              <element name="Catalog High Elf" type="Sub Race" source="Test Book" id="ID_PDF_CATALOG_HIGH_ELF">
                <supports>Catalog Elf</supports>
              </element>
            </elements>
            """);
        await ContentImport.ImportAsync(_root, Database);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        // Simulate retained pre-migration package preferences. The catalog remains complete;
        // restrictions are applied to characters separately, never to PDF identity lookup.
        command.CommandText = "UPDATE content_packages SET is_enabled = 0";
        command.ExecuteNonQuery().Should().BeGreaterThan(0);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public void NameLookupUsesCatalogEvenWhenItsOldPackageSwitchIsOff()
    {
        var result = new CharacterInferenceEngine(Database).Infer(new ParsedCharacterSheet
        {
            Feats = [new ParsedFeature { Name = "Catalog Feat" }]
        });

        result.Elements.Should().ContainSingle(e => e.AuroraId == "ID_PDF_CATALOG_FEAT");
        result.Warnings.Should().NotContain(w => w.Item == "Catalog Feat");
    }

    [Fact]
    public void SubraceLookupIncludesItsParentEvenWhenOldPackageSwitchesAreOff()
    {
        var result = new CharacterInferenceEngine(Database).Infer(new ParsedCharacterSheet
        {
            Species = "Catalog High Elf"
        });

        result.Elements.Select(e => e.AuroraId).Should()
            .BeEquivalentTo(["ID_PDF_CATALOG_HIGH_ELF", "ID_PDF_CATALOG_ELF"]);
        result.Warnings.Should().NotContain(w => w.Category == "Race");
    }
}
