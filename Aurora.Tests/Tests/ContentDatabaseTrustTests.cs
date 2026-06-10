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
                  <element name="Duplicate Marker" type="Grants" source="Internal" id="ID_TEST_DUPLICATE_MARKER" />
                  <element name="Duplicate Marker" type="Grants" source="Internal" id="ID_TEST_DUPLICATE_MARKER" />
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
            health.SourceIntegrityIssues.Should().Be(3);
            health.BlockingIssueCount.Should().Be(0);
            health.ManualReviewIssueCount.Should().Be(0);
            health.AutoRecoveredIssueCount.Should().Be(3);
            health.ExpectedIssueCount.Should().Be(1);
            health.Status.Should().Be(ContentDatabaseHealthStatus.Healthy);
            health.Groups.Should().Contain(group =>
                group.Area == "unresolved-link"
                && group.Impact == ContentDatabaseTrustImpact.Expected
                && group.Status == "runtime-resource"
                && group.Reason == "embedded-resource-overlay"
                && group.Count == 1);
            health.Groups.Should().Contain(group =>
                group.Area == "source-integrity"
                && group.Impact == ContentDatabaseTrustImpact.AutoRecovered
                && group.Reason == "grant-target-id-in-name-attribute"
                && group.Count == 2);
            health.Groups.Should().Contain(group =>
                group.Area == "source-integrity"
                && group.Impact == ContentDatabaseTrustImpact.AutoRecovered
                && group.Reason == "duplicate-element-signature-in-file"
                && group.Count == 1);
            health.Samples.Should().Contain(sample =>
                sample.Area == "source-integrity"
                && sample.Reason == "grant-target-id-in-name-attribute"
                && sample.Owner == "ID_TEST_BACKGROUND");

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

    [Fact]
    public void Import_KeepsConflictingDuplicateElementIdsAsManualReview()
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
                  <element name="First Item" type="Item" source="Test" id="ID_TEST_DUPLICATE_ITEM" />
                  <element name="Second Item" type="Item" source="Test" id="ID_TEST_DUPLICATE_ITEM" />
                </elements>
                """);

            AuroraContentImporter.Import(tempDirectory, sqlitePath);

            ContentDatabaseHealthReport health = AuroraContentImporter.GetHealthReport(sqlitePath)
                ?? throw new InvalidOperationException("Expected a content database health report.");

            health.ManualReviewIssueCount.Should().Be(1);
            health.AutoRecoveredIssueCount.Should().Be(0);
            health.Status.Should().Be(ContentDatabaseHealthStatus.Warning);
            health.Groups.Should().Contain(group =>
                group.Area == "source-integrity"
                && group.Impact == ContentDatabaseTrustImpact.ManualReview
                && group.Reason == "duplicate-element-id-in-file"
                && group.Count == 1);
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
