using Aurora.App.Services;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

/// <summary>
/// The catalog always loads in full, so sources a user had switched off in the database become
/// default source restrictions instead. Only a source that came entirely from switched-off
/// packages may be restricted; infrastructure never is.
/// </summary>
public sealed class SourcePreferenceSeedTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "aurora-seed-" + Guid.NewGuid().ToString("N") + ".sqlite");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    /// <param name="elements">(source book, package, package enabled) per element.</param>
    private void WriteDatabase(bool withPreferenceColumn, params (string Book, string Package, bool Enabled)[] elements)
    {
        using var connection = new SqliteConnection($"Data Source={_path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = withPreferenceColumn
            ? """
              CREATE TABLE content_packages(content_package_id INTEGER PRIMARY KEY, package_key TEXT, is_enabled INTEGER);
              CREATE TABLE source_files(source_file_id INTEGER PRIMARY KEY, content_package_id INTEGER);
              CREATE TABLE source_books(source_book_id INTEGER PRIMARY KEY, name TEXT);
              CREATE TABLE elements(element_id INTEGER PRIMARY KEY, source_file_id INTEGER, source_book_id INTEGER);
              """
            : """
              CREATE TABLE content_packages(content_package_id INTEGER PRIMARY KEY, package_key TEXT);
              CREATE TABLE source_files(source_file_id INTEGER PRIMARY KEY, content_package_id INTEGER);
              CREATE TABLE source_books(source_book_id INTEGER PRIMARY KEY, name TEXT);
              CREATE TABLE elements(element_id INTEGER PRIMARY KEY, source_file_id INTEGER, source_book_id INTEGER);
              """;
        command.ExecuteNonQuery();

        var packages = new Dictionary<string, int>(StringComparer.Ordinal);
        var books = new Dictionary<string, int>(StringComparer.Ordinal);
        int elementId = 0;
        foreach (var (book, package, enabled) in elements)
        {
            if (!packages.TryGetValue(package, out int packageId))
            {
                packages[package] = packageId = packages.Count + 1;
                command.CommandText = withPreferenceColumn
                    ? $"INSERT INTO content_packages VALUES({packageId},'{package}',{(enabled ? 1 : 0)});"
                    : $"INSERT INTO content_packages VALUES({packageId},'{package}');";
                command.ExecuteNonQuery();
                command.CommandText = $"INSERT INTO source_files VALUES({packageId},{packageId});";
                command.ExecuteNonQuery();
            }
            if (!books.TryGetValue(book, out int bookId))
            {
                books[book] = bookId = books.Count + 1;
                command.CommandText = $"INSERT INTO source_books VALUES({bookId},'{book.Replace("'", "''")}');";
                command.ExecuteNonQuery();
            }
            command.CommandText = $"INSERT INTO elements VALUES({++elementId},{packageId},{bookId});";
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void SourceSwitchedOffEntirelyBecomesARestriction()
    {
        WriteDatabase(true,
            ("Ryoko's Guide to the Yokai Realms", "ryokos-guide", false),
            ("Ryoko's Guide to the Yokai Realms", "ryokos-guide", false),
            ("Player's Handbook", "phb", true));

        SourcePreferenceSeed.ReadSwitchedOffSourceNames(_path)
            .Should().Equal("Ryoko's Guide to the Yokai Realms");
    }

    [Fact]
    public void SourceThatOnlyPartlyCameFromASwitchedOffPackageKeepsLoading()
    {
        // A switched-off supplement often adds a few elements to a book that stays on.
        WriteDatabase(true,
            ("Player's Handbook", "phb", true),
            ("Player's Handbook", "ryokos-guide", false));

        SourcePreferenceSeed.ReadSwitchedOffSourceNames(_path).Should().BeEmpty();
    }

    [Fact]
    public void BuilderInfrastructureIsNeverRestricted()
    {
        WriteDatabase(true,
            ("Aurora Legacy Essentials", "core-ale-xml", false),
            ("Internal", "core-internal-xml", false),
            ("Core", "core-core", false));

        SourcePreferenceSeed.ReadSwitchedOffSourceNames(_path).Should().BeEmpty();
    }

    [Fact]
    public void DatabaseWithoutPreferenceFlagsHasNothingToMigrate()
    {
        WriteDatabase(false, ("Ryoko's Guide to the Yokai Realms", "ryokos-guide", false));

        SourcePreferenceSeed.ReadSwitchedOffSourceNames(_path).Should().BeEmpty();
    }

    [Fact]
    public void MissingDatabaseHasNothingToMigrate()
    {
        SourcePreferenceSeed.ReadSwitchedOffSourceNames(_path).Should().BeEmpty();
        SourcePreferenceSeed.ReadSwitchedOffSourceNames(null).Should().BeEmpty();
    }
}
