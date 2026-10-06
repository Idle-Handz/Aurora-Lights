using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentLibraryUpgradeTests
{
    [Fact]
    public async Task SimulatedData18CatalogRequiresRefreshBeforeTheAppCanLoadIt()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = Path.Combine(root, ContentDatabaseService.DatabaseFileName);
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        try
        {
            File.WriteAllText(Path.Combine(root, "content.xml"), """
                <elements>
                  <element id="ID_TEST_LIBRARY_UPGRADE" name="Preserved content" type="Proficiency" source="Test" />
                </elements>
                """);
            await ContentImport.ImportAsync(root, database);
            primaryProperty.SetValue(manager, root);
            additional.Clear();
            var live = new ElementBaseCollection();
            var initial = await DbElementLoader.TryLoadSnapshotAsync(live);
            initial.Success.Should().BeTrue(initial.FailureReason);
            var previousElements = live.ToArray();
            ContentDatabaseReader.IsStale([root], database).Should().BeFalse();

            // Simulate the prior library's data-18/preparation-2 metadata. The schema and
            // preparation contract are unchanged in 0.11.0; this is not a legacy database fixture.
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                   { DataSource = database, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT contract_version FROM content_preparation_metadata WHERE singleton_id = 1;";
                command.ExecuteScalar().Should().Be(2L);
                command.CommandText = "UPDATE database_metadata SET data_version = 18 WHERE singleton_id = 1;";
                command.ExecuteNonQuery().Should().Be(1);
            }

            ContentDatabaseReader.IsStale([root], database).Should().BeTrue("the data format changed even though the XML did not");
            var rejected = await DbElementLoader.TryLoadSnapshotAsync(live);
            rejected.Success.Should().BeFalse();
            rejected.FailureReason.Should().Contain("data version v18").And.Contain("Refresh the content database");
            live.Should().Equal(previousElements, "an incompatible database must not replace the working catalog");

            await ContentImport.ImportAsync(root, database);

            var metadata = ContentDatabaseReader.ReadMetadata(database);
            metadata.Should().NotBeNull();
            metadata!.DataVersion.Should().Be(ContentDatabaseReader.CurrentDataVersion);
            metadata.SourceFileCount.Should().Be(1);
            ContentDatabaseReader.IsStale([root], database).Should().BeFalse();
            var refreshed = await DbElementLoader.TryLoadSnapshotAsync(live);
            refreshed.Success.Should().BeTrue(refreshed.FailureReason);
            refreshed.DataVersion.Should().Be(ContentDatabaseReader.CurrentDataVersion);
            live.GetElement("ID_TEST_LIBRARY_UPGRADE")!.Name.Should().Be("Preserved content");
        }
        finally
        {
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
