using Aurora.App.Services;
using Aurora.Content;
using Aurora.Content.Preparation;
using Aurora.Tests.Helpers;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseHealthTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
    private readonly string? _previousRoot;
    private string DatabasePath => Path.Combine(_root, ContentDatabaseService.DatabaseFileName);

    public ContentDatabaseHealthTests()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        _previousRoot = DataManager.Current.UserDocumentsCustomElementsDirectory;
        Directory.CreateDirectory(_root);
        SetRoot(_root);
        File.WriteAllText(Path.Combine(_root, "content.xml"),
            "<elements><element id='ID_HEALTH_TEST' type='Feat' name='Health Test' source='Test'/></elements>");
        ContentImport.ImportAsync(_root, DatabasePath).GetAwaiter().GetResult();
    }

    [Fact]
    public void ManualReviewAfterTwentyFourRecoveredGroupsStillAffectsHealth()
    {
        Execute("""
            DROP VIEW v_source_integrity_issues;
            CREATE VIEW v_source_integrity_issues AS
            WITH RECURSIVE rows(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM rows WHERE n < 49)
            SELECT CASE WHEN n < 49 THEN 'grant-target-id-in-name-attribute' ELSE 'unrecognized-rule' END AS issue_kind,
                   'Feat' AS owner_type_name, 'ID_HEALTH_TEST' AS owner_aurora_id, 'Health Test' AS owner_name,
                   'file-' || CAST((n - 1) / 2 AS TEXT) || '.xml' AS relative_path,
                   'ID_HEALTH_TEST' AS issue_key, 'Finding' AS issue_text
            FROM rows;
            """);

        var report = new ContentDatabaseService().GetHealthReport();

        report.Should().NotBeNull();
        report!.SourceIntegrityIssues.Should().Be(49);
        report.Groups.Should().HaveCount(25);
        report.AutoRecoveredIssueCount.Should().Be(48);
        report.ManualReviewIssueCount.Should().Be(1,
            "a display limit must never hide a smaller issue group from the status");
        report.Status.Should().Be(ContentDatabaseHealthStatus.Warning);
        report.Samples.Should().ContainSingle(sample => sample.Impact == ContentDatabaseTrustImpact.ManualReview)
            .Which.Should().Be(new ContentDatabaseHealthIssueSample(ContentDatabaseTrustImpact.ManualReview,
                "source-integrity", "review", "unrecognized-rule", "Feat", "file-24.xml",
                "ID_HEALTH_TEST", "ID_HEALTH_TEST", "Finding"));
        report.Samples.Count(sample => sample.Impact == ContentDatabaseTrustImpact.AutoRecovered).Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public async Task EveryActionableReferenceIncludesItsOwnerFileWithoutInflatingTotals()
    {
        foreach (string file in new[] { "first", "second" })
            File.WriteAllText(Path.Combine(_root, file + ".xml"),
                $"<elements><element id='ID_HEALTH_{file}' name='{file}' type='Feat' source='Test'><rules>" +
                string.Concat(Enumerable.Range(0, 7).Select(index =>
                    $"<grant type='Feat' id='ID_MISSING_{file}_{index}'/>")) + "</rules></element></elements>");
        await ContentImport.ImportAsync(_root, DatabasePath);

        var report = ContentDatabaseHealthReader.Read(DatabasePath)!;

        report.ActionableUnresolvedLinks.Should().Be(14);
        report.BlockingIssueCount.Should().Be(14);
        report.Samples.Should().HaveCount(14, "the former twelve-sample limit must not hide actionable references");
        foreach (string file in new[] { "first", "second" })
        {
            report.Samples.Where(sample => sample.FilePath == file + ".xml").Should().HaveCount(7)
                .And.OnlyContain(sample => sample.Impact == ContentDatabaseTrustImpact.Blocking &&
                    sample.Area == "unresolved-link" && sample.Kind == "grant" && sample.Owner == "ID_HEALTH_" + file &&
                    sample.Key.StartsWith("ID_MISSING_" + file + "_", StringComparison.Ordinal));
            report.Groups.Should().ContainSingle(group => group.FilePath == file + ".xml")
                .Which.Count.Should().Be(7);
        }
    }

    [Fact]
    public async Task MissingTypedRowsIdentifyEachFileAndDoNotDoubleCountBlockingIssues()
    {
        foreach (string kind in new[] { "Spell", "Item", "Companion" })
            File.WriteAllText(Path.Combine(_root, kind + ".xml"),
                $"<elements><element id='ID_HEALTH_{kind}' name='{kind}' type='{kind}' source='Test'/></elements>");
        await ContentImport.ImportAsync(_root, DatabasePath);
        Execute("DELETE FROM spells; DELETE FROM items; DELETE FROM companions;");

        var report = ContentDatabaseHealthReader.Read(DatabasePath)!;

        report.ProjectionIssues.Should().Be(3);
        report.BlockingIssueCount.Should().Be(3);
        report.Groups.Should().NotContain(group => group.Area == "projection",
            "projection counts are already included by the shared report contract");
        foreach (string kind in new[] { "Spell", "Item", "Companion" })
            report.Samples.Should().ContainSingle(sample => sample.Area == "projection" && sample.Kind == kind)
                .Which.Should().Be(new ContentDatabaseHealthIssueSample(ContentDatabaseTrustImpact.Blocking,
                    "projection", "missing-row", "missing-resolved-row", kind, kind + ".xml",
                    "ID_HEALTH_" + kind, "ID_HEALTH_" + kind, $"The effective {kind} has no typed database record."));
    }

    [Fact]
    public void SuccessfulDiagnosticReadOnlyClearsItsOwnFailure()
    {
        Execute("ALTER TABLE database_metadata RENAME COLUMN schema_version TO hidden_schema_version;");
        var service = new ContentDatabaseService();
        service.GetMetadata().Should().BeNull();
        service.LastReadFailure.Should().Contain("read database metadata");

        service.GetHealthReport().Should().NotBeNull();
        service.GetSkippedContent().Should().BeEmpty();
        service.GetLocalCorrections().Should().BeEmpty();
        service.LastReadFailure.Should().Contain("read database metadata",
            "Settings reads several diagnostics in sequence and later successes cannot hide an earlier failure");

        Execute("ALTER TABLE spells RENAME COLUMN element_id TO hidden_element_id;");
        service.GetHealthReport().Should().BeNull();
        service.LastReadFailure.Should().Contain("read database metadata").And.Contain("read database health");

        Execute("ALTER TABLE database_metadata RENAME COLUMN hidden_schema_version TO schema_version;");
        service.GetMetadata().Should().NotBeNull();
        service.LastReadFailure.Should().Contain("read database health").And.NotContain("read database metadata");

        Execute("ALTER TABLE spells RENAME COLUMN hidden_element_id TO element_id;");
        service.GetHealthReport().Should().NotBeNull();
        service.LastReadFailure.Should().BeNull();
    }

    [Fact]
    public void ChangingTheContentDirectoryClearsPreviousDatabaseReadFailures()
    {
        Execute("ALTER TABLE database_metadata RENAME COLUMN schema_version TO hidden_schema_version;");
        var service = new ContentDatabaseService();
        service.GetMetadata();
        service.LastReadFailure.Should().NotBeNull();

        service.NotifyContentDirectoryChanged();

        service.LastReadFailure.Should().BeNull();
    }

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void SetRoot(string? root) => typeof(DataManager)
        .GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!.SetValue(DataManager.Current, root);

    public void Dispose()
    {
        SetRoot(_previousRoot);
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }
}
