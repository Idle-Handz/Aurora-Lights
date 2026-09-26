using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentFallbackPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, ContentDatabaseService.DatabaseFileName);

    public ContentFallbackPolicyTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("definition-collision")]
    [InlineData("superseded-definition")]
    [InlineData("unreadable")]
    [InlineData("append")]
    [InlineData("correction")]
    [InlineData("conflict")]
    [InlineData("definition-conflict")]
    [InlineData("unknown-future-exclusion")]
    public void PersistedExclusionsBlockRawFallbackEvenWithoutUnavailableIds(string kind)
    {
        RecordIssue(kind);
        byte[] before = File.ReadAllBytes(Database);
        ContentDatabaseReader.ReadUnavailableIds(Database).Should().BeEmpty();

        Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database, "Failed to parse user/broken.xml");

        fallback.Should().Throw<InvalidDataException>()
            .WithMessage("*raw XML fallback cannot preserve*Settings*affected.xml*user/broken.xml*");
        File.ReadAllBytes(Database).Should().Equal(before);
    }

    [Fact]
    public void ClassificationNoticeAloneDoesNotBlockRawFallback()
    {
        RecordIssue("classification");

        Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database);

        fallback.Should().NotThrow("classification does not exclude any definitions or operations");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeFailureCannotBypassProvisionalOrRetainedDefinitions(bool retainPrevious)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        const string id = "ID_TEST_FALLBACK_POLICY";
        static string Xml(string name) => $"<elements><element id='{id}' name='{name}' type='Proficiency' source='Test' /></elements>";
        string first = Path.Combine(_root, "a.xml");
        string second = Path.Combine(_root, "b.xml");
        try
        {
            primaryProperty.SetValue(manager, _root);
            additional.Clear();
            DbElementLoader.ResetCaches();
            if (retainPrevious)
            {
                File.WriteAllText(first, Xml("Original retained"));
                await ContentImport.ImportAsync(_root, Database, skipUnusableContent: true);
            }
            File.WriteAllText(first, Xml("Changed A"));
            File.WriteAllText(second, Xml("Conflicting B"));
            await ContentImport.ImportAsync(_root, Database, skipUnusableContent: true);
            ContentDatabaseReader.ReadSkippedContent(Database).Should().Contain(issue => issue.Kind == "definition-collision");
            ContentDatabaseReader.ReadUnavailableIds(Database).Should().BeEmpty();

            var live = new ElementBaseCollection();
            var loaded = await DbElementLoader.TryLoadSnapshotAsync(live);
            loaded.Success.Should().BeTrue(loaded.FailureReason);
            live.GetElement(id)!.Name.Should().Be(retainPrevious ? "Original retained" : "Changed A");
            var originalElements = live.ToArray();
            byte[] originalDatabase = File.ReadAllBytes(Database);

            // Unsynced user XML is read during startup and can break an otherwise valid projection.
            string user = Path.Combine(_root, "user");
            Directory.CreateDirectory(user);
            string broken = Path.Combine(user, "broken.xml");
            File.WriteAllText(broken, "<elements><element");
            var reload = await DbElementLoader.TryLoadAsync(live);
            reload.Success.Should().BeFalse();
            live.Should().Equal(originalElements, "a failed reload must preserve the working catalog");

            var coldStart = new ElementBaseCollection();
            var failed = await DbElementLoader.TryLoadAsync(coldStart);
            failed.Success.Should().BeFalse();
            Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(failed.DatabasePath, failed.FailureReason);
            fallback.Should().Throw<InvalidDataException>().WithMessage("*raw XML fallback cannot preserve*Prepared load failed:*");
            coldStart.Should().BeEmpty();
            File.ReadAllBytes(Database).Should().Equal(originalDatabase);

            // Repair releases the old decisions; a stale in-memory flag must not keep blocking recovery.
            File.Delete(broken);
            File.Delete(second);
            await ContentImport.ImportAsync(_root, Database, skipUnusableContent: true);
            Action repairedFallback = () => ContentDatabaseService.ValidateRawXmlFallback(Database);
            repairedFallback.Should().NotThrow();
            var repaired = await DbElementLoader.TryLoadSnapshotAsync(coldStart);
            repaired.Success.Should().BeTrue(repaired.FailureReason);
            coldStart.GetElement(id)!.Name.Should().Be("Changed A");
        }
        finally
        {
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
            DbElementLoader.ResetCaches();
        }
    }

    private void RecordIssue(string kind)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE content_skipped_files (
                skip_ordinal INTEGER PRIMARY KEY, file_path TEXT, relative_path TEXT,
                kind TEXT, detail TEXT, related_path TEXT);
            INSERT INTO content_skipped_files VALUES (0, $path, 'affected.xml', $kind, 'Persisted import decision', NULL);
            """;
        command.Parameters.AddWithValue("$path", Path.Combine(_root, "affected.xml"));
        command.Parameters.AddWithValue("$kind", kind);
        command.ExecuteNonQuery();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
