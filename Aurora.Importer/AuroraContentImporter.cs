using Microsoft.Data.Sqlite;

namespace Aurora.Importer;

/// <summary>
/// Describes one content package row from <c>content_packages</c>.
/// </summary>
public sealed record ContentPackageInfo(
    long   Id,
    string PackageKey,
    string PackageName,
    string PackageKind,
    int    PrecedenceRank,
    bool   IsEnabled);

/// <summary>
/// Public entry point for importing Aurora XML content into the SQLite database.
/// </summary>
public static class AuroraContentImporter
{
    /// <summary>
    /// Returns true if the SQLite database does not exist or is out of date
    /// relative to the XML files in <paramref name="contentDirectory"/>.
    /// </summary>
    public static bool IsStale(string contentDirectory, string sqlitePath) =>
        AuroraSqliteImporter.IsStale(contentDirectory, sqlitePath);

    public static ContentDatabaseMetadata? GetMetadata(string sqlitePath) =>
        AuroraSqliteImporter.GetMetadata(sqlitePath);

    public static ContentDatabaseHealthReport? GetHealthReport(string sqlitePath) =>
        AuroraSqliteImporter.GetHealthReport(sqlitePath);

    /// <summary>
    /// Scans <paramref name="contentDirectory"/> for Aurora XML files, then
    /// incrementally updates the SQLite database at <paramref name="sqlitePath"/>.
    /// Only files whose MD5 hash has changed since the last import are re-imported.
    /// </summary>
    public static AuroraImportResult Import(
        string contentDirectory,
        string sqlitePath,
        IProgress<AuroraImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var catalog = AuroraXmlCatalogReader.BuildCatalog(contentDirectory);
        return AuroraSqliteImporter.Import(catalog, sqlitePath, progress, cancellationToken);
    }

    /// <summary>
    /// Returns all content packages registered in the database.
    /// Returns an empty list if the database does not exist or has no packages yet.
    /// </summary>
    public static IReadOnlyList<ContentPackageInfo> GetPackages(string sqlitePath)
    {
        if (!File.Exists(sqlitePath)) return [];

        var result = new List<ContentPackageInfo>();
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT content_package_id, package_key, package_name, package_kind, precedence_rank,
       COALESCE(is_enabled, 1)
FROM content_packages
ORDER BY
    CASE package_kind
        WHEN 'core'        THEN 0
        WHEN 'official'    THEN 1
        WHEN 'third-party' THEN 2
        WHEN 'homebrew'    THEN 3
        ELSE 4
    END,
    package_name COLLATE NOCASE;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ContentPackageInfo(
                Id:            reader.GetInt64(0),
                PackageKey:    reader.GetString(1),
                PackageName:   reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                PackageKind:   reader.IsDBNull(3) ? "local" : reader.GetString(3),
                PrecedenceRank: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsEnabled:     reader.GetInt64(5) != 0));
        }
        return result;
    }

    /// <summary>
    /// Sets <c>is_enabled</c> for a package and rebuilds the resolution cache so the
    /// change takes effect immediately in subsequent DB reads. The caller must reload
    /// element data for the change to be visible in the running app.
    /// </summary>
    public static void SetPackageEnabled(string sqlitePath, long packageId, bool enabled)
    {
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath }.ToString()))
        {
            connection.Open();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE content_packages SET is_enabled = $v WHERE content_package_id = $id;";
            update.Parameters.AddWithValue("$v",  enabled ? 1 : 0);
            update.Parameters.AddWithValue("$id", packageId);
            update.ExecuteNonQuery();
        }

        // Rebuild the resolution cache immediately so FK columns reflect the new state.
        // Connection above is closed before this opens a second write connection.
        AuroraSqliteImporter.RebuildCacheOnly(sqlitePath);
    }
}
