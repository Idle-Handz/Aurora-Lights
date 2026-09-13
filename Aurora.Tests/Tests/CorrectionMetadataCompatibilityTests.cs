using System.Text.Json;
using System.Xml;
using Aurora.Importer;
using Builder.Data;
using Builder.Data.Files;
using Builder.Presentation.Utilities;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

// Characterization tests for a proposed extension, not a correction implementation.
public sealed class CorrectionMetadataCompatibilityTests : IDisposable
{
    private const string MetadataNamespace = "urn:aurora-lights:corrections:1";
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "aurora-correction-tests-" + Guid.NewGuid().ToString("N"));

    public CorrectionMetadataCompatibilityTests() => Directory.CreateDirectory(temporary);

    [Theory]
    [InlineData("devout.xml", 3)]
    [InlineData("tatsumi.xml", 3)]
    [InlineData("musketball.xml", 1)]
    [InlineData("staff.xml", 1)]
    public void RootMetadata_PreservesFileInformationAndTypedRuntimeDefinitions(string fixture, int count)
    {
        string original = ReadFixture(fixture);
        string annotated = Annotate(original);
        var baseline = LoadFile(original);
        var candidate = LoadFile(annotated);
        candidate.ElementNodes.Should().HaveCount(count);
        candidate.ExtendNodes.Should().HaveCount(baseline.ExtendNodes.Count);
        candidate.Info.Should().BeEquivalentTo(baseline.Info);

        var parsers = ElementParserFactory.GetParsers().ToList();
        for (int i = 0; i < count; i++)
        {
            XmlNode expectedNode = baseline.ElementNodes[i];
            XmlNode actualNode = candidate.ElementNodes[i];
            actualNode.OuterXml.Should().Be(expectedNode.OuterXml);
            string type = expectedNode.Attributes!["type"]!.Value;
            var parser = parsers.FirstOrDefault(p => p.ParserType == type) ?? new ElementParser();
            ElementBase expected = parser.ParseElement(expectedNode);
            ElementBase actual = parser.ParseElement(actualNode);
            actual.GetType().Should().Be(expected.GetType());
            actual.Should().BeEquivalentTo(expected, options => options
                .RespectingRuntimeTypes()
                .Excluding(element => element.ElementNode));
        }
    }

    [Theory]
    [InlineData("devout.xml")]
    [InlineData("tatsumi.xml")]
    [InlineData("musketball.xml")]
    [InlineData("staff.xml")]
    public void ImportAndMetadataOnlyRefresh_PreserveAllNonvolatileDatabaseRows(string fixture)
    {
        string content = Path.Combine(temporary, "content");
        Directory.CreateDirectory(content);
        string file = Path.Combine(content, fixture);
        string database = Path.Combine(temporary, "content.sqlite");
        string original = ReadFixture(fixture);
        File.WriteAllText(file, original);
        Import(content, database);
        var baseline = Snapshot(database);

        File.WriteAllText(file, Annotate(original));
        Import(content, database);
        Snapshot(database).Should().BeEquivalentTo(baseline);

        File.WriteAllText(file, Annotate(original).Replace("review-pending", "reviewed"));
        Import(content, database);
        Snapshot(database).Should().BeEquivalentTo(baseline);

        File.WriteAllText(file, original);
        Import(content, database);
        Snapshot(database).Should().BeEquivalentTo(baseline);
    }

    [Fact]
    public void Metadata_SurvivesRepairAndFileSave_WithoutInjectingElementsOrAppends()
    {
        string original = ReadFixture("devout.xml");
        var document = new XmlDocument();
        document.LoadXml(Annotate(original));
        var manager = new XmlNamespaceManager(document.NameTable);
        manager.AddNamespace("al", MetadataNamespace);
        string before = document.SelectSingleNode("/elements/al:corrections", manager)!.OuterXml;
        AuroraXmlCompatibilityRepair.RepairDocument(document);
        document.SelectSingleNode("/elements/al:corrections", manager)!.OuterXml.Should().Be(before);

        var file = new FileInfo(Path.Combine(temporary, "saved.xml"));
        new ElementsFile(document.OuterXml).SaveContent(file);
        var reloaded = ElementsFile.FromFile(file);
        reloaded.ElementNodes.Should().HaveCount(3);
        reloaded.ExtendNodes.Should().BeEmpty();
        var roundTrip = new XmlDocument();
        roundTrip.Load(file.FullName);
        roundTrip.SelectSingleNode("/elements/al:corrections", manager)!.OuterXml.Should().Be(before);
    }

    [Fact]
    public void MetadataOnlyInfoBlock_IsNotACompatibleExtensionLocation()
    {
        Action load = () => LoadFile("<elements><info><corrections /></info></elements>");
        load.Should().Throw<NullReferenceException>().WithMessage("missing update node in info block");
    }

    [Fact]
    public void DefaultNamespaceOnContentRoot_PreventsImporterDiscovery()
    {
        string content = Path.Combine(temporary, "content");
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, "staff.xml"), ReadFixture("staff.xml")
            .Replace("<elements>", $"<elements xmlns=\"{MetadataNamespace}\">"));
        string database = Path.Combine(temporary, "content.sqlite");
        Import(content, database);
        using var connection = AuroraContentImporter.OpenReadableConnection(database);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM elements";
        Convert.ToInt64(command.ExecuteScalar()).Should().Be(0,
            "this negative control documents why the content root must stay unnamespaced");
    }

    [Fact]
    public void RawBaselineSubtree_IsMutatedByCompatibilityRepair()
    {
        var document = new XmlDocument();
        document.LoadXml($"<elements><al:corrections xmlns:al='{MetadataNamespace}'><al:baseline><grant spellcastin='Wizard'/></al:baseline></al:corrections></elements>");
        AuroraXmlCompatibilityRepair.RepairDocument(document);
        document.SelectSingleNode("//grant")!.Attributes!["spellcastin"].Should().BeNull();
        document.SelectSingleNode("//grant")!.Attributes!["spellcasting"]!.Value.Should().Be("Wizard");
    }

    private static string ReadFixture(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "CorrectionMetadata", name));

    private static ElementsFile LoadFile(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        AuroraXmlCompatibilityRepair.RepairDocument(document);
        var file = new ElementsFile(document.OuterXml);
        file.Load(document.OuterXml);
        return file;
    }

    private static string Annotate(string xml) => xml.Replace("</elements>", $$"""
        <al:corrections xmlns:al="{{MetadataNamespace}}" version="1">
          <al:correction key="compatibility-probe" operation="replace" state="review-pending">
            <al:reason>Compatibility fixture; not an active correction.</al:reason>
            <al:baseline encoding="escaped-xml">&lt;grant spellcastin="Wizard" /&gt;</al:baseline>
            <al:baseline-probe>
              <element id="ID_METADATA_MUST_NOT_IMPORT" name="Probe" source="Probe" type="Item" />
              <append id="ID_METADATA_MUST_NOT_APPEND"><supports>Probe</supports></append>
            </al:baseline-probe>
          </al:correction>
        </al:corrections>
        </elements>
        """);

    private static void Import(string content, string database)
    {
        var result = AuroraContentImporter.Import(content, database);
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.FilesProcessed.Should().Be(1);
    }

    private static Dictionary<string, string[]> Snapshot(string path)
    {
        using var connection = AuroraContentImporter.OpenReadableConnection(path);
        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT IN ('database_metadata', 'import_state') ORDER BY name";
        var tables = new List<string>();
        using (var reader = tableCommand.ExecuteReader())
            while (reader.Read()) tables.Add(reader.GetString(0));
        var result = new Dictionary<string, string[]>();
        foreach (string table in tables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\"";
            using var reader = command.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read())
            {
                var row = new SortedDictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string name = reader.GetName(i);
                    if (name is "file_hash" or "created_utc") continue;
                    row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(JsonSerializer.Serialize(row));
            }
            result[table] = rows.OrderBy(row => row, StringComparer.Ordinal).ToArray();
        }
        return result;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(temporary, recursive: true);
    }
}
