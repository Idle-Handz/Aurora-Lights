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
    bool   IsEnabled)
{
    public bool IsRequired => Builder.Data.RequiredContentPolicy.IsRequiredPackage(PackageKey, PackageName);
}

/// <summary>
/// Public entry point for importing Aurora XML content into the SQLite database.
/// </summary>
public static class AuroraContentImporter
{
    public static string? ResolveSourceFilePath(IEnumerable<string> roots, string relativePath)
        => AuroraXmlCatalogReader.ResolveSourceFilePath(roots, relativePath);

    /// <summary>
    /// Opens an existing database for queries. Normal reads remain read-only, but a leftover
    /// rollback journal requires a writable connection so SQLite can recover an interrupted
    /// transaction before serving data.
    /// </summary>
    public static SqliteConnection OpenReadableConnection(string sqlitePath)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = sqlitePath,
                Mode = File.Exists(sqlitePath + "-journal")
                    ? SqliteOpenMode.ReadWrite
                    : SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Returns true if the SQLite database does not exist or is out of date
    /// relative to the XML files in <paramref name="contentDirectory"/>.
    /// </summary>
    public static bool IsStale(string contentDirectory, string sqlitePath) =>
        IsStale(new[] { contentDirectory }, sqlitePath);

    public static bool IsStale(
        IReadOnlyList<string> contentDirectories,
        string sqlitePath) =>
        LocalCorrectionSync.IsStale(contentDirectories, sqlitePath) ??
        AuroraSqliteImporter.IsStale(AuroraXmlCatalogReader.BuildFileCatalog(contentDirectories), sqlitePath);

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
        return Import(new[] { contentDirectory }, sqlitePath, progress, cancellationToken);
    }

    public static AuroraImportResult Import(
        IReadOnlyList<string> contentDirectories,
        string sqlitePath,
        IProgress<AuroraImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return LocalCorrectionSync.ImportAsync(contentDirectories, sqlitePath,
            (prepared, candidate, token) => Task.FromResult(AuroraSqliteImporter.Import(
                AuroraXmlCatalogReader.BuildCatalog(prepared), candidate, progress, token)),
            cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Returns all content packages registered in the database.
    /// Returns an empty list if the database does not exist or has no packages yet.
    /// </summary>
    public static IReadOnlyList<ContentPackageInfo> GetPackages(string sqlitePath)
    {
        if (!File.Exists(sqlitePath)) return [];

        var result = new List<ContentPackageInfo>();
        using var connection = OpenReadableConnection(sqlitePath);

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
            var package = new ContentPackageInfo(
                Id:            reader.GetInt64(0),
                PackageKey:    reader.GetString(1),
                PackageName:   reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                PackageKind:   reader.IsDBNull(3) ? "local" : reader.GetString(3),
                PrecedenceRank: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsEnabled:     reader.GetInt64(5) != 0);
            // Old disabled flags must not hide the definitions the builder needs.
            result.Add(package.IsRequired ? package with { IsEnabled = true } : package);
        }
        return result;
    }

    /// <summary>
    /// Sets an optional package's preference. Only legacy catalogs need a cache rebuild;
    /// prepared catalogs apply preferences in the app's next projection reload.
    /// </summary>
    public static void SetPackageEnabled(string sqlitePath, long packageId, bool enabled)
    {
        bool prepared;
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath }.ToString()))
        {
            connection.Open();
            using (var query = connection.CreateCommand())
            {
                query.CommandText = "SELECT package_key, package_name FROM content_packages WHERE content_package_id=$id";
                query.Parameters.AddWithValue("$id", packageId);
                using var reader = query.ExecuteReader();
                if (!reader.Read()) throw new ArgumentException("Content source does not exist.", nameof(packageId));
                if (!enabled && Builder.Data.RequiredContentPolicy.IsRequiredPackage(reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1)))
                    throw new InvalidOperationException("Aurora Essentials and Internal/Core infrastructure must remain enabled.");
            }
            prepared = AuroraTranslator.Content.PreparedCatalogReader.HasPreparationMetadata(connection);
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE content_packages SET is_enabled = $v WHERE content_package_id = $id;";
            update.Parameters.AddWithValue("$v",  enabled ? 1 : 0);
            update.Parameters.AddWithValue("$id", packageId);
            update.ExecuteNonQuery();
        }

        // Legacy catalogs use filtered links; prepared catalogs keep global links intact.
        // Runtime preferences take effect on the next app projection reload.
        if (!prepared) AuroraSqliteImporter.RebuildCacheOnly(sqlitePath);
    }

    /// <summary>Effective preferences, including required infrastructure despite stale disabled flags.</summary>
    public static HashSet<string> ReadEnabledPackageKeys(SqliteConnection connection)
    {
        var enabled = new HashSet<string>(StringComparer.Ordinal);
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT package_key,package_name,COALESCE(is_enabled,1) FROM content_packages";
        using var reader = query.ExecuteReader();
        while (reader.Read())
            if (reader.GetInt64(2) != 0 || Builder.Data.RequiredContentPolicy.IsRequiredPackage(reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1)))
                enabled.Add(reader.GetString(0));
        return enabled;
    }
}
