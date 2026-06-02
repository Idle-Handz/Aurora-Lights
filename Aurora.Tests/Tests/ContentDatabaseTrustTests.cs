using Aurora.Importer;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseTrustTests
{
    [Fact]
    public void Import_ClassifiesListChoicesAndRecoversLegacyNameAttributeGrants()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "Aurora.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string sqlitePath = Path.Combine(tempDirectory, "content.sqlite");
            File.WriteAllText(
                Path.Combine(tempDirectory, "content.xml"),
                """
                <elements>
                  <element name="Test Proficiency" type="Proficiency" source="Test" id="ID_TEST_PROFICIENCY" />
                  <element name="Artificer" type="Class" source="Test" id="ID_TEST_CLASS_ARTIFICER" />
                  <element name="Test Specialist" type="Archetype" source="Test" id="ID_TEST_ARCHETYPE_SPECIALIST">
                    <supports>Artificer Specialist</supports>
                  </element>
                  <element name="Test Background" type="Background" source="Test" id="ID_TEST_BACKGROUND">
                    <rules>
                      <grant type="Proficiency" name="  ID_TEST_PROFICIENCY  " />
                      <grant type="Size" name="  ID_SIZE_MEDIUM  " />
                      <select type="List" name="Personality Trait">
                        <item id="1">I always test the suspicious lever before opening the door.</item>
                      </select>
                    </rules>
                  </element>
                </elements>
                """);

            AuroraContentImporter.Import(tempDirectory, sqlitePath);

            ContentDatabaseHealthReport health = AuroraContentImporter.GetHealthReport(sqlitePath)
                ?? throw new InvalidOperationException("Expected a content database health report.");

            health.ActionableUnresolvedLinks.Should().Be(0);
            health.ClassifiedUnresolvedLinks.Should().Be(1);
            health.SourceIntegrityIssues.Should().Be(2);
            health.Status.Should().Be(ContentDatabaseHealthStatus.Warning);

            using (var connection = AuroraContentImporter.OpenReadableConnection(sqlitePath))
            {
                QueryScalar(connection, "SELECT option_kind FROM select_items;")
                    .Should().Be("text-choice");
                QueryScalar(connection, "SELECT target_aurora_id FROM grants WHERE grant_type = 'Proficiency';")
                    .Should().Be("ID_TEST_PROFICIENCY");
                QueryScalar(connection, "SELECT COUNT(*) FROM grants WHERE target_element_id IS NOT NULL;")
                    .Should().Be(1L);
                QueryScalar(connection, "SELECT COUNT(*) FROM archetypes WHERE parent_class_element_id IS NOT NULL;")
                    .Should().Be(1L);
                QueryScalar(connection, "SELECT diagnostic_reason FROM v_unresolved_loader_link_diagnostics;")
                    .Should().Be("embedded-resource-overlay");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static object? QueryScalar(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
