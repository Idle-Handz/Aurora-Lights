using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentUnavailableReportingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "content.sqlite");

    public ContentUnavailableReportingTests() => Directory.CreateDirectory(_root);

    private void CreateDatabase(bool withUnavailableId)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = withUnavailableId
            ? "CREATE TABLE content_unavailable_elements(aurora_id TEXT PRIMARY KEY,detail TEXT NOT NULL); INSERT INTO content_unavailable_elements VALUES ('ID_CONFLICT','Two different definitions');"
            : "CREATE TABLE previous_format(marker INTEGER);";
        command.ExecuteNonQuery();
    }

    [Fact]
    public void UnavailableIdsPreventRawFallbackFromUndoingTheImportDecision()
    {
        CreateDatabase(withUnavailableId: true);
        byte[] before = File.ReadAllBytes(Database);

        Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database, "Runtime XML failed to parse");

        fallback.Should().Throw<InvalidDataException>()
            .WithMessage("*ID_CONFLICT*Runtime XML failed to parse*");
        ContentDatabaseReader.ReadUnavailableIds(Database).Should().ContainSingle().Which.Should().Be("ID_CONFLICT");
        File.ReadAllBytes(Database).Should().Equal(before);
    }

    [Fact]
    public void OlderDatabaseWithoutUnavailableDefinitionsDoesNotBlockRawFallback()
    {
        CreateDatabase(withUnavailableId: false);

        Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database);

        fallback.Should().NotThrow();
        ContentDatabaseReader.ReadUnavailableIds(Database).Should().BeEmpty();
    }

    [Fact]
    public void MissingDatabaseDoesNotBlockRawFallback()
    {
        Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database);

        fallback.Should().NotThrow();
        ContentDatabaseReader.ReadUnavailableIds(Database).Should().BeEmpty();
    }

    [Fact]
    public void SummarySeparatesUnavailableDefinitionsFromSkippedFilesAndAppends()
    {
        string summary = AuroraImportResult.Succeeded(3, 0, 7, unavailableDefinitions: 2).Summary;
        summary.Should().Contain("2 conflicting element IDs are unavailable")
            .And.Contain("no definition was selected")
            .And.Contain("unaffected content was imported")
            .And.NotContain("files were skipped");

        AuroraImportResult.Succeeded(3, 0, 7, filesSkipped: 1, appendOperationsSkipped: 1, unavailableDefinitions: 1)
            .Summary.Should().Contain("1 file was skipped")
            .And.Contain("1 append operation was skipped")
            .And.Contain("1 conflicting element ID is unavailable");
    }

    [Fact]
    public async Task FullDatabaseLoadDoesNotRestoreUnavailableBuiltinsUserXmlOrGeneratedElements()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        string? previousPrimary = manager.UserDocumentsCustomElementsDirectory;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        var extraDirectories = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousExtra = extraDirectories.ToArray();
        string primary = Path.Combine(_root, "custom");
        string user = Path.Combine(primary, "user");
        Directory.CreateDirectory(user);
        string database = Path.Combine(primary, ContentDatabaseService.DatabaseFileName);
        File.WriteAllText(Path.Combine(primary, "first.xml"), """
            <elements>
              <element id="ID_SIZE_MEDIUM" type="Size" name="Conflicting Medium A" source="Test" />
              <element id="ID_TEST_UNAVAILABLE_USER" type="Proficiency" name="Conflicting User A" source="Test" />
              <element id="ID_TEST_AVAILABLE_AFTER_CONFLICT" type="Proficiency" name="Available" source="Test" />
              <element id="ID_TEST_PROFICIENCY_REGENERATED" type="Proficiency" name="Generates internal proxy" source="Test" />
              <element id="ID__INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_REGENERATED" type="Item" name="Conflicting Generated A" source="Test" />
            </elements>
            """);
        File.WriteAllText(Path.Combine(user, "second.xml"), """
            <elements>
              <element id="ID_SIZE_MEDIUM" type="Size" name="Conflicting Medium B" source="Test" />
              <element id="ID_TEST_UNAVAILABLE_USER" type="Proficiency" name="Conflicting User B" source="Test" />
              <element id="ID_TEST_AVAILABLE_USER" type="Proficiency" name="Available User" source="Test" />
              <element id="ID__INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_REGENERATED" type="Item" name="Conflicting Generated B" source="Test" />
            </elements>
            """);
        await ContentImport.ImportAsync(primary, database);
        try
        {
            primaryProperty.SetValue(manager, primary);
            extraDirectories.Clear();
            DbElementLoader.ResetCaches();
            var elements = new ElementBaseCollection();

            var loaded = await DbElementLoader.TryLoadAsync(elements);

            loaded.Success.Should().BeTrue(loaded.FailureReason);
            loaded.DataVersion.Should().Be(ContentDatabaseReader.CurrentDataVersion);
            elements.Select(element => element.Id).Should()
                .Contain(["ID_TEST_AVAILABLE_AFTER_CONFLICT", "ID_TEST_AVAILABLE_USER", "ID_TEST_PROFICIENCY_REGENERATED"])
                .And.NotContain(["ID_SIZE_MEDIUM", "ID_TEST_UNAVAILABLE_USER", "ID__INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_REGENERATED"]);
            new InternalElementsGenerator().GenerateInternalProficiency(elements).Select(element => element.Id)
                .Should().Contain("ID__INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_REGENERATED",
                    "the retained proficiency normally synthesizes this proxy, so post-processing needs the unavailable-ID mask");
            ContentDatabaseReader.ReadUnavailableIds(database).Should()
                .BeEquivalentTo(["ID_SIZE_MEDIUM", "ID_TEST_UNAVAILABLE_USER", "ID__INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_REGENERATED"]);
        }
        finally
        {
            primaryProperty.SetValue(manager, previousPrimary);
            extraDirectories.Clear();
            foreach (string directory in previousExtra) extraDirectories.Add(directory);
            DbElementLoader.ResetCaches();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
