using Aurora.Importer;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace Aurora.Tests.Tests;

public sealed class ContentChangeScanTests : IDisposable
{
    private readonly string work = Path.Combine(Path.GetTempPath(), "aurora-change-scan-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(work, "content");
    private string Extra => Path.Combine(work, "extra-content");
    private string Database => Path.Combine(work, "content.sqlite");
    private string FilePath => Path.Combine(Root, "features.xml");
    private const string Xml = "<elements><element id='ID_TEST' name='Old' type='Item' source='Test'/></elements>";

    public ContentChangeScanTests()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Extra);
        File.WriteAllText(FilePath, Xml);
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE source_files(relative_path TEXT PRIMARY KEY, file_hash TEXT);";
        command.ExecuteNonQuery();
        Record("features.xml", FilePath);
    }

    [Fact]
    public void ChangedMalformedXml_IsReportedWithoutParsingTheCatalog()
    {
        File.WriteAllText(FilePath, "<elements><element");
        AuroraContentImporter.IsStale(Root, Database).Should().BeTrue();
    }

    [Fact]
    public void SameSizeAndTimestampEdit_IsStillDetectedByContentHash()
    {
        AuroraContentImporter.IsStale(Root, Database).Should().BeFalse();
        var timestamp = File.GetLastWriteTimeUtc(FilePath);
        File.WriteAllText(FilePath, Xml.Replace("Old", "New"));
        File.SetLastWriteTimeUtc(FilePath, timestamp);
        AuroraContentImporter.IsStale(Root, Database).Should().BeTrue();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("rename")]
    public void MultipleRootsAndWindowsCatalogPaths_DetectFileSetChanges(string change)
    {
        string extraFile = Path.Combine(Extra, "features.xml");
        File.WriteAllText(extraFile, Xml);
        Record("additional-1-extra-content\\features.xml", extraFile);
        string[] roots = [Root, Root, Extra];
        AuroraContentImporter.IsStale(roots, Database).Should().BeFalse();
        if (change == "add") File.WriteAllText(Path.Combine(Extra, "new.xml"), Xml);
        else if (change == "delete") File.Delete(extraFile);
        else File.Move(extraFile, Path.Combine(Extra, "renamed.xml"));
        AuroraContentImporter.IsStale(roots, Database).Should().BeTrue();
    }

    private void Record(string relative, string path)
    {
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO source_files VALUES ($path,$hash)";
        command.Parameters.AddWithValue("$path", relative);
        command.Parameters.AddWithValue("$hash", Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))));
        command.ExecuteNonQuery();
    }

    public void Dispose() => Directory.Delete(work, recursive: true);
}
