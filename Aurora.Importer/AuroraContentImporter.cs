using Aurora.Content.Preparation;
using Microsoft.Data.Sqlite;

namespace Aurora.Importer;

/// <summary>
/// Public entry point for importing Aurora XML content into the SQLite database.
/// </summary>
public static class AuroraContentImporter
{
    public static string? ResolveSourceFilePath(IEnumerable<string> roots, string relativePath)
        => AuroraXmlCatalogReader.ResolveSourceFilePath(roots, relativePath);

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
            prepared = PreparedCatalogReader.HasPreparationMetadata(connection);
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
