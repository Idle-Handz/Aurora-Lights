using Aurora.App.Services;
using Aurora.Content;
using Aurora.Content.Preparation;

namespace Aurora.Tests.Tests;

/// <summary>
/// One file the importer cannot use should not cost the user a refresh. When the app allows
/// skipping, the file is left out, everything else is imported, and what was left out is kept in
/// the database so Settings can still show it after a restart.
/// </summary>
public sealed class ContentSkipReportingTests
{
    private const string Unreadable =
        "<elements xmlns='http://example.com/schema'><element name='Lost' type='Proficiency' source='Test' id='ID_LOST' /></elements>";

    private const string Readable =
        "<elements><element name='Good' type='Proficiency' source='Test' id='ID_SKIPTEST_GOOD' /></elements>";

    private static string NewWorkspace()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public async Task ARefreshReportsTheFilesItLeftOut()
    {
        string workspace = NewWorkspace();
        try
        {
            string database = Path.Combine(workspace, "content.sqlite");
            File.WriteAllText(Path.Combine(workspace, "good.xml"), Readable);
            File.WriteAllText(Path.Combine(workspace, "broken.xml"), Unreadable);

            var result = await ContentImport.ImportAsync(workspace, database, skipUnusableContent: true);

            result.Skipped.Should().ContainSingle().Which.RelativePath
                .Should().Be("broken.xml", "the user needs to know which file to fix");

            // Read back the way Settings does, from the database rather than from the import.
            var reported = ContentDatabaseReader.ReadSkippedContent(database);
            reported.Should().ContainSingle();
            reported[0].Kind.Should().Be("unreadable");
            reported[0].Detail.Should().Contain("elements root");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task WhatCouldBeReadIsStillImported()
    {
        string workspace = NewWorkspace();
        try
        {
            string database = Path.Combine(workspace, "content.sqlite");
            File.WriteAllText(Path.Combine(workspace, "good.xml"), Readable);
            File.WriteAllText(Path.Combine(workspace, "broken.xml"), Unreadable);

            await ContentImport.ImportAsync(workspace, database, skipUnusableContent: true);

            using var connection = ContentDatabase.OpenReadableConnection(database);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM elements WHERE aurora_id IN ('ID_SKIPTEST_GOOD','ID_LOST')";
            Convert.ToInt64(command.ExecuteScalar()).Should().Be(1,
                "the readable file imports and nothing from the skipped one does");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ADatabaseWithNothingSkippedReportsNothing()
    {
        string workspace = NewWorkspace();
        try
        {
            string database = Path.Combine(workspace, "content.sqlite");
            File.WriteAllText(Path.Combine(workspace, "good.xml"), Readable);

            await ContentImport.ImportAsync(workspace, database, skipUnusableContent: true);

            ContentDatabaseReader.ReadSkippedContent(database).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void TheRefreshSummarySaysWhenFilesNeedAttention()
    {
        AuroraImportResult.Succeeded(3, 10, 42, filesSkipped: 2).Summary
            .Should().Contain("2 files were skipped and need attention");
        AuroraImportResult.Succeeded(3, 10, 42, appendOperationsSkipped: 2).Summary
            .Should().Contain("2 append operations were skipped").And.NotContain("files were skipped");
        AuroraImportResult.Succeeded(3, 10, 42, filesSkipped: 1, appendOperationsSkipped: 1).Summary
            .Should().Contain("1 file was skipped and needs attention").And.Contain("1 append operation was skipped");
        AuroraImportResult.Succeeded(3, 10, 42).Summary
            .Should().NotContain("skipped");
    }

    [Fact]
    public void SkippingIsOnByDefaultSoARefreshIsNotBlockedByOneBadFile()
    {
        new Builder.Presentation.AppSettingsStore().SkipUnusableContentOnRefresh.Should().BeTrue();
    }
}
