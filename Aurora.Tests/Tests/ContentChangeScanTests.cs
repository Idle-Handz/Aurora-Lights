using Aurora.App.Services;
using Aurora.Content;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

/// <summary>
/// Deciding whether the content database is out of date compares the files it was built from
/// against what is on disk now. It must notice an edit that keeps a file's size and timestamp,
/// must not need to parse the XML to answer, and must notice files appearing or disappearing.
/// </summary>
public sealed class ContentChangeScanTests : IDisposable
{
    private readonly string work = Path.Combine(Path.GetTempPath(), "aurora-change-scan-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(work, "content");
    private string Database => Path.Combine(work, "content.sqlite");
    private string FilePath => Path.Combine(Root, "core", "features.xml");
    private const string Xml = "<elements><element id='ID_TEST' name='Old' type='Item' source='Test'/></elements>";

    public ContentChangeScanTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, Xml);
        Import();
    }

    private void Import() => ContentImport.ImportAsync(Root, Database).GetAwaiter().GetResult();

    private bool IsStale() => ContentDatabaseReader.IsStale([Root], Database);

    [Fact]
    public void AnUnchangedContentFolderIsNotStale() => IsStale().Should().BeFalse();

    [Fact]
    public void ChangedMalformedXmlIsReportedWithoutParsingTheCatalog()
    {
        File.WriteAllText(FilePath, "<elements><element");

        IsStale().Should().BeTrue("the check compares file contents and never parses them");
    }

    [Fact]
    public void AnEditKeepingTheSameSizeAndTimestampIsStillDetected()
    {
        var timestamp = File.GetLastWriteTimeUtc(FilePath);
        File.WriteAllText(FilePath, Xml.Replace("Old", "New"));
        File.SetLastWriteTimeUtc(FilePath, timestamp);

        IsStale().Should().BeTrue();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("rename")]
    public void FilesAppearingOrDisappearingAreDetected(string change)
    {
        IsStale().Should().BeFalse();

        if (change == "add") File.WriteAllText(Path.Combine(Root, "core", "new.xml"), Xml);
        else if (change == "delete") File.Delete(FilePath);
        else File.Move(FilePath, Path.Combine(Root, "core", "renamed.xml"));

        IsStale().Should().BeTrue();
    }

    [Fact]
    public void ADatabaseThisLibraryDidNotBuildNeedsRebuilding()
    {
        // An older database is not upgraded in place; the app rebuilds it.
        File.Delete(Database);
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE source_files(relative_path TEXT PRIMARY KEY, file_hash TEXT);";
            command.ExecuteNonQuery();
        }

        IsStale().Should().BeTrue();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(work, recursive: true);
    }
}
