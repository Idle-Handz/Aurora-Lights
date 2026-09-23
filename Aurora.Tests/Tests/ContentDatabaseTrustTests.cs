using Aurora.Content.Preparation;
using Aurora.Content;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseTrustTests
{
    [Fact]
    public void Import_ClassifiesListChoicesAndReportsLegacyNameAttributeGrants()
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

            ContentImport.ImportAsync(tempDirectory, sqlitePath).GetAwaiter().GetResult();

            ContentDatabaseHealthReport health = ContentDatabaseReader.ReadHealth(sqlitePath)
                ?? throw new InvalidOperationException("Expected a content database health report.");

            // A grant that carries its target id in the name attribute is reported, not silently
            // repaired: the link stays unresolved so the content can be fixed.
            health.SourceIntegrityIssues.Should().Be(2);
            health.ActionableUnresolvedLinks.Should().Be(3);
            health.Status.Should().Be(ContentDatabaseHealthStatus.Error);
            health.Groups.Should().Contain(group =>
                group.Area == "source-integrity"
                && group.Impact == ContentDatabaseTrustImpact.AutoRecovered
                && group.Reason == "grant-target-id-in-name-attribute"
                && group.Count == 2);
            health.Groups.Should().Contain(group =>
                group.Area == "unresolved-link"
                && group.Impact == ContentDatabaseTrustImpact.Blocking
                && group.Kind == "grant"
                && group.Count == 2);
            health.Samples.Should().Contain(sample =>
                sample.Area == "source-integrity"
                && sample.Reason == "grant-target-id-in-name-attribute"
                && sample.Owner == "ID_TEST_BACKGROUND");

            using (var connection = ContentDatabase.OpenReadableConnection(sqlitePath))
            {
                QueryScalar(connection, "SELECT option_kind FROM select_items;")
                    .Should().Be("text-choice");
                // The reference is stored exactly as the content wrote it, padding included, so the
                // health report can point at the file instead of a guess about what was meant.
                QueryScalar(connection, "SELECT target_aurora_id FROM grants WHERE grant_type = 'Proficiency';")
                    .Should().Be("  ID_TEST_PROFICIENCY  ");
                QueryScalar(connection, "SELECT COUNT(*) FROM grants WHERE target_element_id IS NOT NULL;")
                    .Should().Be(0L, "a padded reference is not silently matched to an element");
                // The archetype's class is inferred from its supports tag; that inference is a
                // tracked gap in the library, so the link stays unresolved and visible.
                QueryScalar(connection, "SELECT COUNT(*) FROM archetypes WHERE parent_class_element_id IS NOT NULL;")
                    .Should().Be(0L);
                QueryScalar(connection, "SELECT COUNT(*) FROM v_unresolved_loader_link_diagnostics WHERE diagnostic_status = 'actionable';")
                    .Should().Be(3L, "two grants and the archetype parent are reported for review");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Import_RefusesConflictingDuplicateElementIds()
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

            Action import = () => ContentImport.ImportAsync(tempDirectory, sqlitePath).GetAwaiter().GetResult();

            import.Should().Throw<InvalidDataException>()
                .WithMessage("*duplicate-element-id*ID_TEST_DUPLICATE_ITEM*conflicting definitions*",
                    "two different definitions of one id are resolved by review, not by picking one");
            File.Exists(sqlitePath).Should().BeFalse("a refused import leaves the installed database alone");
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
