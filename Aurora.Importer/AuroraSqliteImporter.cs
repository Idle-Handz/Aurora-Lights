using Aurora.Importer.Models;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aurora.Importer;

/// <summary>
/// Incrementally imports an Aurora XML content catalog into the SQLite database.
/// Files whose MD5 hash matches the stored hash are skipped; changed or new files
/// are deleted (cascade) and re-imported; deleted files are removed automatically.
/// Copied and adapted from 5eApiTranslator/AuroraSqlitePocImporter.cs — the
/// original project remains the standalone CLI tool. This version:
///   - Loads the schema SQL from an embedded resource instead of the file system.
///   - Reports progress via IProgress&lt;AuroraImportProgress&gt;.
///   - Omits SRD creature import (not needed for character loading).
///   - Omits expression catalog rebuild (compendium analytics, not needed for loading).
/// </summary>
internal static class AuroraSqliteImporter
{
    private static string LoadSchemaSql()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Aurora.Importer.Resources.aurora-elements-schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static AuroraImportResult Import(
        AuroraImportCatalog catalog,
        string sqlitePath,
        IProgress<AuroraImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath) ?? AppContext.BaseDirectory);

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath }.ToString());
        connection.Open();

        EnsureSchema(connection);
        // When the importer logic changes in a way that affects stored data (e.g. adding a new
        // column to element_supports), bump this constant so existing DBs get a full rebuild.
        const int ExpectedDataVersion = AuroraDatabaseVersions.DataVersion;
        int dbVersion = GetUserVersion(connection);
        if (dbVersion != ExpectedDataVersion)
        {
            // Wipe all source file records — ON DELETE CASCADE clears all element data too.
            ExecuteSql(connection, null, "DELETE FROM source_files;");
            SetUserVersion(connection, ExpectedDataVersion);
        }
        ExecuteSql(connection, null, "PRAGMA foreign_keys = ON;");

        using var transaction = connection.BeginTransaction();

        Dictionary<string, long> elementTypeIds = LoadElementTypeIds(connection, transaction);

        var sourceBookIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceName in catalog.Elements.Select(x => x.source)
            .Concat(catalog.Spells.Select(x => x.source))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            sourceBookIds[sourceName!] = EnsureSourceBook(connection, transaction, sourceName!);
        }

        var primarySourceByFile = BuildPrimarySourceByFile(catalog);

        var existingFiles = LoadExistingSourceFileHashes(connection, transaction);
        var sourceFileIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var changedPaths  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPaths     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileHashesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Build a source-name → kind map from Source element setter flags.
        // These are the authoritative declarations of package kind in the Aurora XML.
        var sourceKindMap = BuildSourceKindMap(catalog);

        // Build content_packages from the top-level directory of each source file path.
        // ON CONFLICT DO NOTHING preserves any user-configured kind/rank from prior runs.
        var contentPackageIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in catalog.Files)
        {
            primarySourceByFile.TryGetValue(file.RelativePath, out string? primarySource);
            string pkgKey = GetPackageKey(file, primarySource, sourceKindMap);
            if (!contentPackageIds.ContainsKey(pkgKey))
                contentPackageIds[pkgKey] = EnsureContentPackage(
                    connection, transaction, pkgKey,
                    GetPackageDisplayName(file, primarySource),
                    DetermineKindForFile(file, primarySource, sourceKindMap));
        }

        // Correct stale/legacy package_kind values.  Two passes: Source-element setters
        // (authoritative) then key-prefix heuristics (fallback for legacy entries).
        CorrectPackageKinds(connection, transaction, sourceKindMap);

        progress?.Report(new AuroraImportProgress(
            AuroraImportPhase.Scanning, 0, catalog.Files.Count, 0, 0, null));

        int scanned = 0;
        foreach (var file in catalog.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            seenPaths.Add(file.RelativePath);
            string hash = ComputeFileHash(file.FullPath);
            fileHashesByPath[file.RelativePath] = hash;
            scanned++;

            primarySourceByFile.TryGetValue(file.RelativePath, out string? primarySource);
            long? pkgId = contentPackageIds.TryGetValue(GetPackageKey(file, primarySource, sourceKindMap), out var pid)
                ? pid : null;

            if (existingFiles.TryGetValue(file.RelativePath, out var existing))
            {
                if (existing.Hash == hash)
                {
                    sourceFileIds[file.RelativePath] = existing.Id;
                    continue;
                }
                DeleteSourceFile(connection, transaction, existing.Id);
            }

            long newId = InsertSourceFile(connection, transaction, file, hash, pkgId);
            sourceFileIds[file.RelativePath] = newId;
            changedPaths.Add(file.RelativePath);

            progress?.Report(new AuroraImportProgress(
                AuroraImportPhase.Scanning, scanned, catalog.Files.Count,
                changedPaths.Count, 0, file.RelativePath));
        }

        foreach (var (path, existing) in existingFiles)
        {
            if (!seenPaths.Contains(path))
                DeleteSourceFile(connection, transaction, existing.Id);
        }

        int addedElements = 0;

        if (changedPaths.Count > 0)
        {
            progress?.Report(new AuroraImportProgress(
                AuroraImportPhase.Importing, catalog.Files.Count, catalog.Files.Count,
                changedPaths.Count, 0, null));
        }

        foreach (var element in catalog.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!changedPaths.Contains(element.source_file_path ?? string.Empty)) continue;
            if (!elementTypeIds.TryGetValue(element.type ?? string.Empty, out long elementTypeId)) continue;

            long elementId = InsertElementBase(
                connection, transaction, elementTypeId,
                sourceBookIds.TryGetValue(element.source ?? string.Empty, out var sbId) ? sbId : (long?)null,
                sourceFileIds.TryGetValue(element.source_file_path ?? string.Empty, out var sfId) ? sfId : (long?)null,
                element.id, element.name, element.index,
                element.compendium.display, DetermineLoaderPriority(element.type));

            InsertElementTexts(connection, transaction, elementId, element);
            InsertElementSupports(connection, transaction, elementId, element.supports);
            InsertElementRequirements(connection, transaction, elementId, element.requirements);
            InsertElementBlocks(connection, transaction, elementId, element.additionalBlocks);
            InsertSetters(connection, transaction, elementId, "element", element.setters);
            InsertExtract(connection, transaction, elementId, element.extract);

            if (element.spellcasting != null)
                InsertSpellcastingProfile(connection, transaction, elementId, element.type, element.spellcasting);

            InsertSubtypeRecord(connection, transaction, elementId, element);
            InsertRules(connection, transaction, elementId, "element", element.rules);

            if (string.Equals(element.type, "class", StringComparison.OrdinalIgnoreCase)
                && element.multiclass != null)
            {
                InsertClassMulticlass(connection, transaction, elementId, element.multiclass);
                InsertSetters(connection, transaction, elementId, "class-multiclass", element.multiclass.setters);
                InsertRules(connection, transaction, elementId, "class-multiclass", element.multiclass.rules);
            }

            addedElements++;
        }

        foreach (var spell in catalog.Spells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!changedPaths.Contains(spell.source_file_path ?? string.Empty)) continue;
            if (!elementTypeIds.TryGetValue("Spell", out long elementTypeId)) continue;

            long elementId = InsertElementBase(
                connection, transaction, elementTypeId,
                sourceBookIds.TryGetValue(spell.source ?? string.Empty, out var sbId) ? sbId : (long?)null,
                sourceFileIds.TryGetValue(spell.source_file_path ?? string.Empty, out var sfId) ? sfId : (long?)null,
                spell.aurora_id, spell.name, spell.index,
                spell.compendium_display, DetermineLoaderPriority("Spell"));

            InsertSpellTexts(connection, transaction, elementId, spell);
            InsertElementSupports(connection, transaction, elementId, spell.supports);
            InsertSetters(connection, transaction, elementId, "element", spell.setters);
            InsertSpellRecord(connection, transaction, elementId, spell);
            addedElements++;
        }

        // Always refresh package kinds — fast, uses authoritative source element flags.
        RefreshPackageKinds(connection, transaction);

        // Rebuild the resolution cache and re-resolve FKs whenever content changed,
        // or on first run after this feature was added (cache is empty but DB has data).
        bool cacheEmpty = IsCacheEmpty(connection);
        if (changedPaths.Count > 0 || cacheEmpty)
        {
            progress?.Report(new AuroraImportProgress(
                AuroraImportPhase.Resolving, catalog.Files.Count, catalog.Files.Count,
                changedPaths.Count, addedElements, null));

            BuildResolutionCache(connection, transaction);
            ResolveDeferredRelationships(connection, transaction);
        }

        UpsertDatabaseMetadata(
            connection,
            transaction,
            sourceFileCount: catalog.Files.Count,
            elementCount: QueryElementCount(connection, transaction),
            contentRootHash: ComputeContentRootHash(fileHashesByPath));

        transaction.Commit();

        progress?.Report(new AuroraImportProgress(
            AuroraImportPhase.Complete, catalog.Files.Count, catalog.Files.Count,
            changedPaths.Count, addedElements, null));

        int unchanged = catalog.Files.Count - changedPaths.Count;
        return AuroraImportResult.Succeeded(changedPaths.Count, unchanged, addedElements);
    }

    // ── Staleness check ──────────────────────────────────────────────────────

    /// <summary>
    /// Fast check: compares file hashes in the DB against actual files on disk.
    /// Returns true if any file is new, changed, or deleted since the last import.
    /// Does not open a write transaction; read-only.
    /// </summary>
    public static bool IsStale(string contentDirectory, string sqlitePath)
    {
        return IsStale(AuroraXmlCatalogReader.BuildCatalog(contentDirectory), sqlitePath);
    }

    public static bool IsStale(AuroraImportCatalog catalog, string sqlitePath)
    {
        if (!File.Exists(sqlitePath)) return true;

        try
        {
            using var conn = AuroraContentImporter.OpenReadableConnection(sqlitePath);

            // Check schema exists (if not, DB is stale/empty).
            using (var chk = conn.CreateCommand())
            {
                chk.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='source_files';";
                if ((long)(chk.ExecuteScalar() ?? 0L) == 0) return true;
            }

            var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT relative_path, file_hash FROM source_files;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    stored[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1);
            }

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in catalog.Files)
            {
                seenPaths.Add(file.RelativePath);
                if (!File.Exists(file.FullPath))
                    return true;

                string hash = ComputeFileHash(file.FullPath);
                if (!stored.TryGetValue(file.RelativePath, out string? storedHash) || storedHash != hash)
                    return true;
            }

            // Any stored file that's gone from disk.
            return stored.Keys.Any(p => !seenPaths.Contains(p));
        }
        catch
        {
            return true;
        }
    }

    public static ContentDatabaseMetadata? GetMetadata(string sqlitePath)
    {
        if (!File.Exists(sqlitePath)) return null;

        using var conn = AuroraContentImporter.OpenReadableConnection(sqlitePath);

        using (var tableCheck = conn.CreateCommand())
        {
            tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='database_metadata';";
            if ((long)(tableCheck.ExecuteScalar() ?? 0L) == 0) return null;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT schema_version,
       data_version,
       importer_version,
       built_utc,
       source_file_count,
       element_count,
       content_root_hash
FROM database_metadata
WHERE singleton_id = 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new ContentDatabaseMetadata(
            SchemaVersion: reader.GetInt32(0),
            DataVersion: reader.GetInt32(1),
            ImporterVersion: reader.GetString(2),
            BuiltUtc: reader.GetString(3),
            SourceFileCount: reader.GetInt32(4),
            ElementCount: reader.GetInt32(5),
            ContentRootHash: reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    public static ContentDatabaseHealthReport? GetHealthReport(string sqlitePath)
    {
        if (!File.Exists(sqlitePath)) return null;

        using var conn = AuroraContentImporter.OpenReadableConnection(sqlitePath);

        int actionableUnresolvedLinks = ExecuteCount(
            conn,
            "SELECT COUNT(*) FROM v_unresolved_loader_link_diagnostics WHERE diagnostic_status = 'actionable';");
        int classifiedUnresolvedLinks = ExecuteCount(
            conn,
            "SELECT COUNT(*) FROM v_unresolved_loader_link_diagnostics WHERE diagnostic_status <> 'actionable';");
        int sourceIntegrityIssues = ExecuteCount(conn, "SELECT COUNT(*) FROM v_source_integrity_issues;");
        int missingResolvedSpellRows = ExecuteCount(conn, @"
SELECT COUNT(*)
FROM v_resolved_elements AS re
JOIN elements AS e
    ON e.element_id = re.winning_element_id
JOIN element_types AS et
    ON et.element_type_id = e.element_type_id
LEFT JOIN spells AS s
    ON s.element_id = e.element_id
WHERE et.type_name = 'Spell'
  AND s.element_id IS NULL;");
        int missingResolvedItemRows = ExecuteCount(conn, @"
SELECT COUNT(*)
FROM v_resolved_elements AS re
JOIN elements AS e
    ON e.element_id = re.winning_element_id
JOIN element_types AS et
    ON et.element_type_id = e.element_type_id
LEFT JOIN items AS i
    ON i.element_id = e.element_id
WHERE et.type_name = 'Item'
  AND i.element_id IS NULL;");
        int missingResolvedCompanionRows = ExecuteCount(conn, @"
SELECT COUNT(*)
FROM v_resolved_elements AS re
JOIN elements AS e
    ON e.element_id = re.winning_element_id
JOIN element_types AS et
    ON et.element_type_id = e.element_type_id
LEFT JOIN companions AS c
    ON c.element_id = e.element_id
WHERE et.type_name = 'Companion'
  AND c.element_id IS NULL;");

        return new ContentDatabaseHealthReport(
            actionableUnresolvedLinks,
            classifiedUnresolvedLinks,
            sourceIntegrityIssues,
            missingResolvedSpellRows,
            missingResolvedItemRows,
            missingResolvedCompanionRows,
            QueryHealthIssueGroups(conn),
            QueryHealthIssueSamples(conn));
    }

    private static IReadOnlyList<ContentDatabaseHealthIssueGroup> QueryHealthIssueGroups(SqliteConnection connection)
    {
        var groups = new List<ContentDatabaseHealthIssueGroup>();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    'unresolved-link' AS area,
    diagnostic_status AS status,
    COALESCE(diagnostic_reason, 'actionable') AS reason,
    link_kind AS kind,
    '' AS file_path,
    COUNT(*) AS issue_count
FROM v_unresolved_loader_link_diagnostics
GROUP BY diagnostic_status, diagnostic_reason, link_kind
ORDER BY
    CASE diagnostic_status
        WHEN 'actionable' THEN 0
        WHEN 'runtime-resource' THEN 1
        WHEN 'option-pool' THEN 2
        ELSE 3
    END,
    issue_count DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                groups.Add(ReadHealthIssueGroup(reader));
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    'source-integrity' AS area,
    'review' AS status,
    issue_kind AS reason,
    COALESCE(owner_type_name, '') AS kind,
    relative_path AS file_path,
    COUNT(*) AS issue_count
FROM v_source_integrity_issues
GROUP BY issue_kind, owner_type_name, relative_path
ORDER BY issue_count DESC, issue_kind, relative_path
LIMIT 24;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                groups.Add(ReadHealthIssueGroup(reader));
        }

        return groups;
    }

    private static IReadOnlyList<ContentDatabaseHealthIssueSample> QueryHealthIssueSamples(SqliteConnection connection)
    {
        var samples = new List<ContentDatabaseHealthIssueSample>();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    'unresolved-link' AS area,
    diagnostic_status AS status,
    COALESCE(diagnostic_reason, 'actionable') AS reason,
    link_kind AS kind,
    '' AS file_path,
    COALESCE(owner_aurora_id, owner_name, '') AS owner,
    COALESCE(unresolved_key, '') AS issue_key,
    COALESCE(unresolved_text, '') AS issue_text
FROM v_unresolved_loader_link_diagnostics
WHERE diagnostic_status = 'actionable'
ORDER BY link_kind, owner_type_name, owner_name
LIMIT 12;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                samples.Add(ReadHealthIssueSample(reader));
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    'source-integrity' AS area,
    'review' AS status,
    issue_kind AS reason,
    COALESCE(owner_type_name, '') AS kind,
    relative_path AS file_path,
    COALESCE(owner_aurora_id, owner_name, '') AS owner,
    COALESCE(issue_key, '') AS issue_key,
    COALESCE(issue_text, '') AS issue_text
FROM v_source_integrity_issues
ORDER BY issue_kind, relative_path, owner_name
LIMIT 12;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                samples.Add(ReadHealthIssueSample(reader));
        }

        return samples;
    }

    // ── Schema / DB setup ────────────────────────────────────────────────────

    private static void EnsureSchema(SqliteConnection connection)
    {
        // Migrations must run before the schema SQL so that indexes on newly-added
        // columns (e.g. ix_source_files_package) succeed on existing databases.
        ApplyMigrations(connection);

        string rawSql = LoadSchemaSql();
        string schemaSql = System.Text.RegularExpressions.Regex.Replace(
            rawSql,
            @"^\s*(PRAGMA\s+\S.*?;|BEGIN\s+TRANSACTION\s*;|COMMIT\s*;|ROLLBACK\s*;)\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        using var schema = connection.CreateCommand();
        schema.CommandText = schemaSql;
        schema.ExecuteNonQuery();
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void ApplyMigrations(SqliteConnection connection)
    {
        // v1→v2
        AddColumnIfMissing(connection, "source_files", "file_hash", "TEXT");

        // v2→v3: full 5eApiTranslator schema (content_packages FK, semantic grant
        //        columns, raw_xml preservation, select_items option_kind)
        AddColumnIfMissing(connection, "source_files",  "content_package_id",   "INTEGER");
        AddColumnIfMissing(connection, "grants",        "target_semantic_key",  "TEXT");
        AddColumnIfMissing(connection, "grants",        "target_semantic_kind", "TEXT");
        AddColumnIfMissing(connection, "grants",        "target_semantic_name", "TEXT");
        AddColumnIfMissing(connection, "grants",        "raw_xml",              "TEXT");
        AddColumnIfMissing(connection, "selects",       "raw_xml",              "TEXT");
        AddColumnIfMissing(connection, "select_items",  "option_kind",
            "TEXT NOT NULL DEFAULT 'name-reference-candidate'");
        AddColumnIfMissing(connection, "stats",         "raw_xml",              "TEXT");
        AddColumnIfMissing(connection, "database_metadata", "content_root_hash", "TEXT");
    }

    private static void AddColumnIfMissing(SqliteConnection connection,
        string table, string column, string columnDef)
    {
        // Skip if the table doesn't exist yet — CREATE TABLE in the schema SQL will
        // define the column correctly on a fresh database.
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t;";
        tableCheck.Parameters.AddWithValue("$t", table);
        if ((long)tableCheck.ExecuteScalar()! == 0) return;

        using var colCheck = connection.CreateCommand();
        colCheck.CommandText =
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name=$col;";
        colCheck.Parameters.AddWithValue("$col", column);
        if ((long)colCheck.ExecuteScalar()! != 0) return;

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN {column} {columnDef};";
        alter.ExecuteNonQuery();
    }

    // ── Source book helpers ──────────────────────────────────────────────────

    private static long EnsureSourceBook(SqliteConnection connection, SqliteTransaction transaction, string sourceName)
    {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT OR IGNORE INTO source_books (name) VALUES ($name);";
        insert.Parameters.AddWithValue("$name", sourceName);
        insert.ExecuteNonQuery();

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT source_book_id FROM source_books WHERE name = $name;";
        select.Parameters.AddWithValue("$name", sourceName);
        return (long)select.ExecuteScalar()!;
    }

    // ── Content package helpers ──────────────────────────────────────────────

    private static long EnsureContentPackage(SqliteConnection connection, SqliteTransaction transaction,
        string packageKey, string? packageName, string packageKind)
    {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
INSERT INTO content_packages (package_key, package_name, package_kind, precedence_rank)
VALUES ($package_key, $package_name, $package_kind, $precedence_rank)
ON CONFLICT(package_key) DO NOTHING;";
        insert.Parameters.AddWithValue("$package_key",      packageKey);
        insert.Parameters.AddWithValue("$package_name",     packageName ?? packageKey);
        insert.Parameters.AddWithValue("$package_kind",     packageKind);
        insert.Parameters.AddWithValue("$precedence_rank",  DefaultPrecedenceRank(packageKind));
        insert.ExecuteNonQuery();

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT content_package_id FROM content_packages WHERE package_key = $package_key;";
        select.Parameters.AddWithValue("$package_key", packageKey);
        return (long)select.ExecuteScalar()!;
    }

    private static string GetPackageKey(AuroraFileInfo file, string? primarySource,
        IReadOnlyDictionary<string, string>? sourceKindMap = null)
    {
        string packageKind = DetermineKindForFile(file, primarySource, sourceKindMap);
        if (!string.IsNullOrWhiteSpace(primarySource))
            return $"{packageKind}:{BuildPackageSlug(primarySource)}";

        if (!string.IsNullOrWhiteSpace(file.Name))
            return $"{packageKind}:{BuildPackageSlug(file.Name!)}";

        int sep = file.RelativePath.IndexOfAny(['/', '\\']);
        string topLevel = sep > 0 ? file.RelativePath[..sep] : file.RelativePath;
        return $"{packageKind}:{BuildPackageSlug(topLevel)}";
    }

    /// <summary>
    /// Determines the content package kind for a file, using the Source element's
    /// explicit setter flags as the authoritative source when the file's primary
    /// source name is known, falling back to directory-path heuristics otherwise.
    /// </summary>
    private static string DetermineKindForFile(AuroraFileInfo file, string? primarySource,
        IReadOnlyDictionary<string, string>? sourceKindMap)
    {
        if (!string.IsNullOrWhiteSpace(primarySource)
            && sourceKindMap?.TryGetValue(primarySource, out string? xmlKind) == true
            && xmlKind != null)
            return xmlKind;
        return DeterminePackageKind(file.RelativePath);
    }

    private static string DeterminePackageKind(string relativePath)
    {
        int sep = relativePath.IndexOfAny(['/', '\\']);
        string topLevel = sep > 0 ? relativePath[..sep] : relativePath;
        return topLevel.ToLowerInvariant() switch
        {
            "core"                 => "core",
            "supplements"
            or "unearthed-arcana" => "official",
            "user"                 => "homebrew",
            _                      => "third-party"
        };
    }

    /// <summary>
    /// Builds a map from Source element name → package kind by reading the boolean
    /// kind-setter flags (<c>core</c>, <c>official</c>, <c>third-party</c>, <c>homebrew</c>)
    /// declared on every element with <c>type="Source"</c> in the catalog.
    /// </summary>
    private static Dictionary<string, string> BuildSourceKindMap(AuroraImportCatalog catalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in catalog.Elements
            .Where(e => string.Equals(e.type, "Source", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(e.name)
                     && e.setters != null))
        {
            string? kind = GetKindFromSourceSetters(el.setters!);
            if (kind != null)
                map[el.name!] = kind;
        }
        return map;
    }

    private static string? GetKindFromSourceSetters(AuroraSetters setters)
    {
        if (setters.GetBoolean("core") == true)         return "core";
        if (setters.GetBoolean("official") == true)     return "official";
        if (setters.GetBoolean("third-party") == true)  return "third-party";
        if (setters.GetBoolean("homebrew") == true)     return "homebrew";
        return null;
    }

    private static string BuildPackageSlug(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        bool previousDash = false;
        foreach (char ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string? GetPackageDisplayName(AuroraFileInfo file, string? primarySource)
    {
        if (!string.IsNullOrWhiteSpace(primarySource))
            return primarySource;

        return file.Name;
    }

    private static Dictionary<string, string?> BuildPrimarySourceByFile(AuroraImportCatalog catalog)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in catalog.Elements
            .Where(x => !string.IsNullOrWhiteSpace(x.source_file_path))
            .GroupBy(x => x.source_file_path!, StringComparer.OrdinalIgnoreCase))
        {
            result[group.Key] = SelectPrimarySource(group.Select(x => x.source));
        }

        foreach (var group in catalog.Spells
            .Where(x => !string.IsNullOrWhiteSpace(x.source_file_path))
            .GroupBy(x => x.source_file_path!, StringComparer.OrdinalIgnoreCase))
        {
            if (result.ContainsKey(group.Key))
                continue;

            result[group.Key] = SelectPrimarySource(group.Select(x => x.source));
        }

        return result;
    }

    private static string? SelectPrimarySource(IEnumerable<string?> sources)
    {
        var ranked = sources
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => SourcePriority(x.Name))
            .ThenByDescending(x => x.Count)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count == 0)
            return null;

        if (ranked.Count == 1 || ranked[0].Count > ranked[1].Count)
            return ranked[0].Name;

        if (ranked[0].Count == ranked[1].Count
            && SourcePriority(ranked[0].Name) > SourcePriority(ranked[1].Name))
            return ranked[0].Name;

        return null;
    }

    private static int SourcePriority(string sourceName) =>
        IsVirtualSource(sourceName) ? 0 : 1;

    private static bool IsVirtualSource(string sourceName) =>
        string.Equals(sourceName?.Trim(), "Internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Corrects <c>package_kind</c> and <c>precedence_rank</c> for stale package rows.
    /// Two passes:
    /// <list type="number">
    ///   <item>Source-element pass: queries <c>setter_entries</c> for every Source element
    ///   already in the DB, derives the authoritative kind from the XML setter flags
    ///   (<c>core</c>, <c>official</c>, <c>third-party</c>, <c>homebrew</c>), and updates any
    ///   package whose key matches the source name — covering both the current colon-format
    ///   and legacy hyphen-format keys.  The catalog map is merged in for Source elements
    ///   not yet written (new/changed files in the current batch).</item>
    ///   <item>Key-prefix pass: fallback for entries with no Source element in the DB.</item>
    /// </list>
    /// Only updates rows where the stored kind is already wrong.
    /// </summary>
    private static void CorrectPackageKinds(SqliteConnection conn, SqliteTransaction txn,
        IReadOnlyDictionary<string, string> catalogSourceKindMap)
    {
        // Pass 1 — Source element setter flags (authoritative).
        // Build the name→kind map by reading setter_entries for every Source-type element
        // in the DB; this covers historical imports, not just the current batch.
        var sourceKindMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var kindQuery = conn.CreateCommand();
        kindQuery.Transaction = txn;
        kindQuery.CommandText = @"
            SELECT e.name,
                MIN(CASE se.setter_name
                    WHEN 'core'        THEN 1
                    WHEN 'official'    THEN 2
                    WHEN 'third-party' THEN 3
                    WHEN 'homebrew'    THEN 4
                    ELSE 999
                END) AS kind_priority
            FROM elements e
            JOIN element_types et ON et.element_type_id = e.element_type_id
                                  AND et.type_name = 'Source'
            JOIN setter_scopes ss ON ss.owner_element_id = e.element_id
                                  AND ss.owner_kind = 'element'
            JOIN setter_entries se ON se.setter_scope_id = ss.setter_scope_id
            WHERE se.setter_name IN ('core', 'official', 'third-party', 'homebrew')
              AND LOWER(TRIM(se.setter_value)) = 'true'
            GROUP BY e.name
            HAVING kind_priority < 999;";

        using (var r = kindQuery.ExecuteReader())
        {
            while (r.Read())
            {
                string name = r.GetString(0);
                string kind = r.GetInt32(1) switch
                {
                    1 => "core",
                    2 => "official",
                    3 => "third-party",
                    _ => "homebrew"
                };
                sourceKindMap[name] = kind;
            }
        }

        // Merge catalog entries so Source elements in the current import batch
        // (whose elements may have been deleted and not yet re-inserted) are also covered.
        foreach (var (name, kind) in catalogSourceKindMap)
            sourceKindMap.TryAdd(name, kind);

        // Apply source-element corrections: try every key format that could represent
        // this source (current colon-format + legacy directory-prefixed hyphen-format).
        using var updateCmd = conn.CreateCommand();
        updateCmd.Transaction = txn;
        updateCmd.CommandText = @"
            UPDATE content_packages
            SET package_kind = $kind, precedence_rank = $rank
            WHERE package_key = $key AND package_kind <> $kind;";
        updateCmd.Parameters.AddWithValue("$key",  "");
        updateCmd.Parameters.AddWithValue("$kind", "");
        updateCmd.Parameters.AddWithValue("$rank", 0);

        foreach (var (sourceName, kind) in sourceKindMap)
        {
            string slug = BuildPackageSlug(sourceName);
            int rank = DefaultPrecedenceRank(kind);
            foreach (string candidateKey in new[]
            {
                $"{kind}:{slug}",
                $"core-{slug}", $"supplements-{slug}", $"unearthed-arcana-{slug}",
                $"third-party-{slug}", $"homebrew-{slug}", $"user-{slug}"
            })
            {
                updateCmd.Parameters["$key"].Value  = candidateKey;
                updateCmd.Parameters["$kind"].Value = kind;
                updateCmd.Parameters["$rank"].Value = rank;
                updateCmd.ExecuteNonQuery();
            }
        }

        // Pass 2 — key-prefix heuristics (fallback for entries with no Source element).
        var rules = new (string kind, int rank, string[] prefixes)[]
        {
            ("core",        100, ["core:", "core-"]),
            ("official",    200, ["supplements:", "supplements-", "unearthed-arcana:", "unearthed-arcana-"]),
            ("third-party", 300, ["third-party:", "third-party-"]),
            ("homebrew",    400, ["homebrew:", "homebrew-", "user:", "user-"]),
        };

        foreach (var (kind, rank, prefixes) in rules)
        {
            string likeClause = string.Join(" OR ", prefixes.Select((_, i) => $"package_key LIKE $p{i}"));
            using var cmd = conn.CreateCommand();
            cmd.Transaction = txn;
            cmd.CommandText = $@"
                UPDATE content_packages
                SET package_kind = $kind, precedence_rank = $rank
                WHERE ({likeClause})
                  AND package_kind <> $kind;";
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$rank", rank);
            for (int i = 0; i < prefixes.Length; i++)
                cmd.Parameters.AddWithValue($"$p{i}", prefixes[i] + "%");
            cmd.ExecuteNonQuery();
        }
    }

    private static int DefaultPrecedenceRank(string packageKind) =>
        packageKind switch
        {
            "core"        => 100,
            "official"    => 200,
            "third-party" => 300,
            "homebrew"    => 400,
            _             => 500
        };

    // ── Source file helpers ──────────────────────────────────────────────────

    private static Dictionary<string, (long Id, string Hash)> LoadExistingSourceFileHashes(
        SqliteConnection connection, SqliteTransaction transaction)
    {
        var map = new Dictionary<string, (long, string)>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT source_file_id, relative_path, file_hash FROM source_files;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            map[reader.GetString(1)] = (reader.GetInt64(0), reader.IsDBNull(2) ? "" : reader.GetString(2));
        return map;
    }

    private static void DeleteSourceFile(SqliteConnection connection, SqliteTransaction transaction, long sourceFileId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM source_files WHERE source_file_id = $id;";
        cmd.Parameters.AddWithValue("$id", sourceFileId);
        cmd.ExecuteNonQuery();
    }

    private static string ComputeFileHash(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
        using var md5    = MD5.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }

    private static Dictionary<string, long> LoadElementTypeIds(SqliteConnection connection, SqliteTransaction transaction)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT element_type_id, type_name FROM element_types;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            map[reader.GetString(1)] = reader.GetInt64(0);
        return map;
    }

    private static long InsertSourceFile(
        SqliteConnection connection, SqliteTransaction transaction,
        AuroraFileInfo file, string? hash = null, long? contentPackageId = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO source_files
(relative_path, content_package_id, package_name, package_description, version_text, update_file_name, update_url, author_name, author_url, file_hash)
VALUES
($relative_path, $content_package_id, $package_name, $package_description, $version_text, $update_file_name, $update_url, $author_name, $author_url, $file_hash);";
        command.Parameters.AddWithValue("$relative_path",       file.RelativePath);
        command.Parameters.AddWithValue("$content_package_id",  contentPackageId.HasValue ? contentPackageId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$package_name",        (object?)file.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$package_description", (object?)file.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$version_text",        (object?)file.FileVersion?.versionString ?? DBNull.Value);
        command.Parameters.AddWithValue("$update_file_name",    (object?)file.FileVersion?.fileName ?? DBNull.Value);
        command.Parameters.AddWithValue("$update_url",          (object?)file.FileVersion?.fileUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$author_name",         (object?)file.Author?.name ?? DBNull.Value);
        command.Parameters.AddWithValue("$author_url",          (object?)file.Author?.url ?? DBNull.Value);
        command.Parameters.AddWithValue("$file_hash",           (object?)hash ?? DBNull.Value);
        command.ExecuteNonQuery();
        return GetLastInsertRowId(connection, transaction);
    }

    private static long InsertElementBase(
        SqliteConnection connection, SqliteTransaction transaction,
        long elementTypeId, long? sourceBookId, long? sourceFileId,
        string? auroraId, string? name, string? slug,
        bool compendiumDisplay, int loaderPriority)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO elements (aurora_id, element_type_id, source_book_id, source_file_id, name, slug, compendium_display, loader_priority)
VALUES ($aurora_id, $element_type_id, $source_book_id, $source_file_id, $name, $slug, $compendium_display, $loader_priority);";
        command.Parameters.AddWithValue("$aurora_id",         auroraId ?? "");
        command.Parameters.AddWithValue("$element_type_id",   elementTypeId);
        command.Parameters.AddWithValue("$source_book_id",    sourceBookId.HasValue ? sourceBookId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$source_file_id",    sourceFileId.HasValue ? sourceFileId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$name",              name ?? "");
        command.Parameters.AddWithValue("$slug",              slug ?? name?.Trim().ToLower().Replace(" ", "-") ?? "");
        command.Parameters.AddWithValue("$compendium_display", compendiumDisplay ? 1 : 0);
        command.Parameters.AddWithValue("$loader_priority",   loaderPriority);
        command.ExecuteNonQuery();
        return GetLastInsertRowId(connection, transaction);
    }

    // ── Text insertion ───────────────────────────────────────────────────────

    private static void InsertElementTexts(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.prerequisite))
            InsertElementText(connection, transaction, elementId, "prerequisite", 1, null, null, null, null, null, element.prerequisite);

        if (element.prerequisites?.Any() == true)
        {
            int ordinal = 1;
            foreach (var p in element.prerequisites)
                InsertElementText(connection, transaction, elementId, "prerequisites", ordinal++, null, null, null, null, null, p);
        }

        if (!string.IsNullOrWhiteSpace(element.description))
            InsertElementText(connection, transaction, elementId, "description", 1, null, null, null, null, null, element.description, element.descriptionRawXml);

        if (element.sheet == null) return;

        if (element.sheet.description?.Any() == true)
        {
            int ordinal = 1;
            foreach (var desc in element.sheet.description)
            {
                InsertElementText(connection, transaction, elementId, "sheet", ordinal++,
                    desc.level, element.sheet.display, element.sheet.alt,
                    element.sheet.action, element.sheet.usage, desc.text, desc.rawXml);
            }
        }
        else
        {
            InsertElementText(connection, transaction, elementId, "sheet", 1,
                null, element.sheet.display, element.sheet.alt,
                element.sheet.action, element.sheet.usage, string.Empty, element.sheet.rawXml);
        }
    }

    private static void InsertSpellTexts(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraSpell spell)
    {
        if (spell.desc?.Any() == true)
            InsertElementText(connection, transaction, elementId, "description", 1, null, null, null, null, null,
                string.Join(Environment.NewLine, spell.desc), spell.descriptionRawXml);

        if (spell.higher_level?.Any() == true)
            InsertElementText(connection, transaction, elementId, "summary", 1, null, null, null, null, null,
                string.Join(Environment.NewLine, spell.higher_level));
    }

    private static void InsertElementText(
        SqliteConnection connection, SqliteTransaction transaction, long elementId,
        string textKind, int ordinal, int? level, bool? display,
        string? altText, string? actionText, string? usageText, string? body, string? rawXml = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO element_texts (element_id, text_kind, ordinal, level, display, alt_text, action_text, usage_text, body)
VALUES ($element_id, $text_kind, $ordinal, $level, $display, $alt_text, $action_text, $usage_text, $body);";
        command.Parameters.AddWithValue("$element_id",   elementId);
        command.Parameters.AddWithValue("$text_kind",    textKind);
        command.Parameters.AddWithValue("$ordinal",      ordinal);
        command.Parameters.AddWithValue("$level",        level.HasValue ? level.Value : DBNull.Value);
        command.Parameters.AddWithValue("$display",      display.HasValue ? (display.Value ? 1 : 0) : DBNull.Value);
        command.Parameters.AddWithValue("$alt_text",     (object?)altText ?? DBNull.Value);
        command.Parameters.AddWithValue("$action_text",  (object?)actionText ?? DBNull.Value);
        command.Parameters.AddWithValue("$usage_text",   (object?)usageText ?? DBNull.Value);
        command.Parameters.AddWithValue("$body",         body ?? string.Empty);
        command.ExecuteNonQuery();

        if (!string.IsNullOrWhiteSpace(rawXml))
        {
            long textId = GetLastInsertRowId(connection, transaction);
            ExecuteInsert(connection, transaction,
                "INSERT INTO element_text_markup (element_text_id, content_format, raw_xml) VALUES ($element_text_id, 'aurora-xml', $raw_xml);",
                ("$element_text_id", textId),
                ("$raw_xml", rawXml));
        }
    }

    // ── Block / support / requirement insertion ──────────────────────────────

    private static void InsertElementBlocks(SqliteConnection connection, SqliteTransaction transaction, long elementId, IEnumerable<AuroraBlockEntry>? blocks)
    {
        if (blocks?.Any() != true) return;
        int ordinal = 1;
        foreach (var block in blocks)
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO element_blocks (element_id, ordinal, block_name, body_text, raw_xml) VALUES ($element_id, $ordinal, $block_name, $body_text, $raw_xml);",
                ("$element_id",  elementId),
                ("$ordinal",     ordinal++),
                ("$block_name",  block.name ?? string.Empty),
                ("$body_text",   (object?)block.value ?? DBNull.Value),
                ("$raw_xml",     block.rawXml ?? string.Empty));

            long blockId = GetLastInsertRowId(connection, transaction);
            int attrOrdinal = 1;
            foreach (var attr in block.attributes)
            {
                ExecuteInsert(connection, transaction,
                    "INSERT INTO element_block_attributes (element_block_id, ordinal, attribute_name, attribute_value) VALUES ($element_block_id, $ordinal, $attribute_name, $attribute_value);",
                    ("$element_block_id", blockId), ("$ordinal", attrOrdinal++),
                    ("$attribute_name", attr.Key), ("$attribute_value", (object?)attr.Value ?? DBNull.Value));
            }
        }
    }

    private static void InsertElementSupports(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraTextCollection? supports)
    {
        if (supports == null || supports.Count == 0) return;
        int ordinal = 1;
        foreach (var s in supports)
            ExecuteInsert(connection, transaction,
                "INSERT INTO element_supports (element_id, ordinal, support_text) VALUES ($element_id, $ordinal, $support_text);",
                ("$element_id", elementId), ("$ordinal", ordinal++), ("$support_text", s));
    }

    private static void InsertElementRequirements(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraTextCollection? requirements)
    {
        if (requirements == null || requirements.Count == 0) return;
        int ordinal = 1;
        foreach (var r in requirements)
            ExecuteInsert(connection, transaction,
                "INSERT INTO element_requirements (element_id, ordinal, requirement_text) VALUES ($element_id, $ordinal, $requirement_text);",
                ("$element_id", elementId), ("$ordinal", ordinal++), ("$requirement_text", r));
    }

    // ── Subtype record insertion ─────────────────────────────────────────────

    private static void InsertSubtypeRecord(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraElement element)
    {
        if (string.Equals(element.type, "Source", StringComparison.OrdinalIgnoreCase))
        {
            var authorSetter = element.setters?.FindEntry("author");
            ExecuteInsert(connection, transaction,
                @"INSERT INTO source_elements (element_id, abbreviation_text, source_url, image_url, errata_url, author_name, author_abbreviation, author_url, is_official, is_core, is_supplement, is_third_party, release_text)
VALUES ($element_id, $abbreviation_text, $source_url, $image_url, $errata_url, $author_name, $author_abbreviation, $author_url, $is_official, $is_core, $is_supplement, $is_third_party, $release_text);",
                ("$element_id",            elementId),
                ("$abbreviation_text",     (object?)element.setters?.GetValue("abbreviation") ?? DBNull.Value),
                ("$source_url",            (object?)element.setters?.GetValue("url") ?? DBNull.Value),
                ("$image_url",             (object?)element.setters?.GetValue("image") ?? DBNull.Value),
                ("$errata_url",            (object?)element.setters?.GetValue("errata") ?? DBNull.Value),
                ("$author_name",           (object?)authorSetter?.value ?? DBNull.Value),
                ("$author_abbreviation",   (object?)authorSetter?.GetAttribute("abbreviation") ?? DBNull.Value),
                ("$author_url",            (object?)authorSetter?.GetAttribute("url") ?? DBNull.Value),
                ("$is_official",           element.setters?.GetBoolean("official").HasValue == true ? (element.setters.GetBoolean("official")!.Value ? 1 : 0) : DBNull.Value),
                ("$is_core",               element.setters?.GetBoolean("core").HasValue == true ? (element.setters.GetBoolean("core")!.Value ? 1 : 0) : DBNull.Value),
                ("$is_supplement",         element.setters?.GetBoolean("supplement").HasValue == true ? (element.setters.GetBoolean("supplement")!.Value ? 1 : 0) : DBNull.Value),
                ("$is_third_party",        element.setters?.GetBoolean("third-party").HasValue == true ? (element.setters.GetBoolean("third-party")!.Value ? 1 : 0) : DBNull.Value),
                ("$release_text",          (object?)element.setters?.GetValue("release") ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Class", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO classes (element_id, hit_die, short_text) VALUES ($element_id, $hit_die, $short_text);",
                ("$element_id", elementId),
                ("$hit_die",    (object?)element.setters?.hd ?? DBNull.Value),
                ("$short_text", (object?)element.setters?.@short ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Archetype", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO archetypes (element_id, parent_support_text) VALUES ($element_id, $parent_support_text);",
                ("$element_id", elementId),
                ("$parent_support_text", (object?)element.supports?.FirstOrDefault() ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Race", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO races (element_id, names_format_text) VALUES ($element_id, $names_format_text);",
                ("$element_id", elementId),
                ("$names_format_text", (object?)element.setters?.GetValue("names-format") ?? DBNull.Value));

            int ordinal = 1;
            foreach (var nameGroup in element.setters?.names ?? Enumerable.Empty<Names>())
            {
                foreach (var nameValue in nameGroup.names ?? Enumerable.Empty<string>())
                {
                    ExecuteInsert(connection, transaction,
                        "INSERT INTO race_name_groups (race_element_id, ordinal, name_group_type, name_value) VALUES ($race_element_id, $ordinal, $name_group_type, $name_value);",
                        ("$race_element_id", elementId), ("$ordinal", ordinal++),
                        ("$name_group_type", (object?)nameGroup.type ?? DBNull.Value), ("$name_value", nameValue));
                }
            }
            return;
        }

        if (string.Equals(element.type, "Sub Race", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO subraces (element_id, parent_support_text) VALUES ($element_id, $parent_support_text);",
                ("$element_id", elementId),
                ("$parent_support_text", (object?)element.supports?.FirstOrDefault() ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Race Variant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.type, "Dragonmark", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO race_variants (element_id, variant_kind, parent_support_text) VALUES ($element_id, $variant_kind, $parent_support_text);",
                ("$element_id", elementId), ("$variant_kind", element.type!),
                ("$parent_support_text", (object?)GetPreferredSupportText(element.supports, "Race Variant") ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Background", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction, "INSERT INTO backgrounds (element_id) VALUES ($element_id);", ("$element_id", elementId));
            return;
        }

        if (string.Equals(element.type, "Background Variant", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO background_variants (element_id, parent_support_text) VALUES ($element_id, $parent_support_text);",
                ("$element_id", elementId),
                ("$parent_support_text", (object?)GetPreferredSupportText(element.supports, "Background Variant") ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Feat", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO feats (element_id, allow_duplicate) VALUES ($element_id, $allow_duplicate);",
                ("$element_id", elementId),
                ("$allow_duplicate", element.setters?.GetBoolean("allow duplicate") == true ? 1 : 0));
            return;
        }

        if (string.Equals(element.type, "Language", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO languages (element_id, script_text, speakers_text, is_standard, is_exotic, is_secret) VALUES ($element_id, $script_text, $speakers_text, $is_standard, $is_exotic, $is_secret);",
                ("$element_id",   elementId),
                ("$script_text",  (object?)element.setters?.script ?? DBNull.Value),
                ("$speakers_text",(object?)element.setters?.speakers ?? DBNull.Value),
                ("$is_standard",  element.setters?.standard == true ? 1 : 0),
                ("$is_exotic",    element.setters?.exotic == true ? 1 : 0),
                ("$is_secret",    element.setters?.secret == true ? 1 : 0));
            return;
        }

        if (string.Equals(element.type, "Proficiency", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO proficiencies (element_id, proficiency_group, proficiency_subgroup) VALUES ($element_id, $proficiency_group, $proficiency_subgroup);",
                ("$element_id",          elementId),
                ("$proficiency_group",   (object?)element.supports?.FirstOrDefault() ?? DBNull.Value),
                ("$proficiency_subgroup", element.supports?.Count > 1 ? element.supports[1] : (object)DBNull.Value));
            return;
        }

        if (IsFeatureType(element.type))
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO features (element_id, feature_kind, parent_support_text, min_level) VALUES ($element_id, $feature_kind, $parent_support_text, $min_level);",
                ("$element_id",          elementId),
                ("$feature_kind",        element.type!),
                ("$parent_support_text", (object?)element.supports?.FirstOrDefault() ?? DBNull.Value),
                ("$min_level",           GetMinimumLevel(element) is int ml ? ml : DBNull.Value));
            return;
        }

        if (IsItemType(element.type))
        {
            ExecuteInsert(connection, transaction,
                @"INSERT INTO items (element_id, item_kind, cost_text, weight_text, damage_dice_text, damage_type_text, armor_class_text, properties_text, speed_text, capacity_text)
VALUES ($element_id, $item_kind, $cost_text, $weight_text, $damage_dice_text, $damage_type_text, $armor_class_text, $properties_text, $speed_text, $capacity_text);",
                ("$element_id",       elementId), ("$item_kind", element.type!),
                ("$cost_text",        (object?)element.setters?.GetValue("cost") ?? DBNull.Value),
                ("$weight_text",      (object?)element.setters?.GetValue("weight") ?? DBNull.Value),
                ("$damage_dice_text", (object?)element.setters?.GetValue("damage") ?? DBNull.Value),
                ("$damage_type_text", (object?)element.setters?.GetValue("damage type") ?? DBNull.Value),
                ("$armor_class_text", (object?)element.setters?.GetValue("armor class") ?? DBNull.Value),
                ("$properties_text",  (object?)element.supports?.raw ?? DBNull.Value),
                ("$speed_text",       (object?)element.setters?.GetValue("speed") ?? DBNull.Value),
                ("$capacity_text",    (object?)element.setters?.GetValue("capacity") ?? DBNull.Value));
            return;
        }

        if (string.Equals(element.type, "Companion", StringComparison.OrdinalIgnoreCase))
        {
            var crText = element.setters?.GetValue("challenge");
            ExecuteInsert(connection, transaction,
                @"INSERT INTO companions
(element_id, size_text, creature_type, alignment, ac_text, hp_text, speed_text,
 str_score, dex_score, con_score, int_score, wis_score, cha_score,
 skills_text, resistances_text, immunities_text, condition_immunities_text,
 senses_text, languages_text, challenge_text, cr_value, proficiency_bonus, actions_text)
VALUES
($element_id, $size_text, $creature_type, $alignment, $ac_text, $hp_text, $speed_text,
 $str_score, $dex_score, $con_score, $int_score, $wis_score, $cha_score,
 $skills_text, $resistances_text, $immunities_text, $condition_immunities_text,
 $senses_text, $languages_text, $challenge_text, $cr_value, $proficiency_bonus, $actions_text);",
                ("$element_id",               elementId),
                ("$size_text",                (object?)element.setters?.GetValue("size")               ?? DBNull.Value),
                ("$creature_type",            (object?)element.setters?.GetValue("type")               ?? DBNull.Value),
                ("$alignment",                (object?)element.setters?.GetValue("alignment")          ?? DBNull.Value),
                ("$ac_text",                  (object?)element.setters?.GetValue("ac")                 ?? DBNull.Value),
                ("$hp_text",                  (object?)element.setters?.GetValue("hp")                 ?? DBNull.Value),
                ("$speed_text",               (object?)element.setters?.GetValue("speed")              ?? DBNull.Value),
                ("$str_score",                ParseIntSetter(element.setters?.GetValue("strength"))),
                ("$dex_score",                ParseIntSetter(element.setters?.GetValue("dexterity"))),
                ("$con_score",                ParseIntSetter(element.setters?.GetValue("constitution"))),
                ("$int_score",                ParseIntSetter(element.setters?.GetValue("intelligence"))),
                ("$wis_score",                ParseIntSetter(element.setters?.GetValue("wisdom"))),
                ("$cha_score",                ParseIntSetter(element.setters?.GetValue("charisma"))),
                ("$skills_text",              (object?)element.setters?.GetValue("skills")             ?? DBNull.Value),
                ("$resistances_text",         (object?)element.setters?.GetValue("resistances")        ?? DBNull.Value),
                ("$immunities_text",          (object?)element.setters?.GetValue("immunities")         ?? DBNull.Value),
                ("$condition_immunities_text",(object?)element.setters?.GetValue("condition-immunities") ?? DBNull.Value),
                ("$senses_text",              (object?)element.setters?.GetValue("senses")             ?? DBNull.Value),
                ("$languages_text",           (object?)element.setters?.GetValue("languages")          ?? DBNull.Value),
                ("$challenge_text",           (object?)crText                                          ?? DBNull.Value),
                ("$cr_value",                 ParseCrValue(crText)),
                ("$proficiency_bonus",        ParseIntSetter(element.setters?.GetValue("proficiency"))),
                ("$actions_text",             (object?)element.setters?.GetValue("actions")            ?? DBNull.Value));
            return;
        }
    }

    // ── Class / spellcasting / setters / rules ───────────────────────────────

    private static void InsertClassMulticlass(SqliteConnection connection, SqliteTransaction transaction, long elementId, Multiclass multiclass)
    {
        ExecuteInsert(connection, transaction,
            "INSERT INTO class_multiclass (class_element_id, multiclass_aurora_id, prerequisite_text, requirements_text, proficiencies_text) VALUES ($class_element_id, $multiclass_aurora_id, $prerequisite_text, $requirements_text, $proficiencies_text);",
            ("$class_element_id",     elementId),
            ("$multiclass_aurora_id", (object?)multiclass.id ?? DBNull.Value),
            ("$prerequisite_text",    (object?)multiclass.prerequisite ?? DBNull.Value),
            ("$requirements_text",    (object?)multiclass.requirements?.raw ?? DBNull.Value),
            ("$proficiencies_text",   (object?)multiclass.setters?.GetValue("multiclass proficiencies") ?? DBNull.Value));
    }

    private static void InsertSpellcastingProfile(SqliteConnection connection, SqliteTransaction transaction, long elementId, string? elementType, Spellcasting spellcasting)
    {
        ExecuteInsert(connection, transaction,
            "INSERT INTO spellcasting_profiles (owner_element_id, owner_kind, profile_name, ability_name, is_extended, prepare_spells, allow_replace, list_text, extend_text) VALUES ($owner_element_id, $owner_kind, $profile_name, $ability_name, $is_extended, $prepare_spells, $allow_replace, $list_text, $extend_text);",
            ("$owner_element_id", elementId),
            ("$owner_kind",       GetSpellcastingOwnerKind(elementType)),
            ("$profile_name",     spellcasting.name ?? "Spellcasting"),
            ("$ability_name",     (object?)spellcasting.ability ?? DBNull.Value),
            ("$is_extended",      spellcasting.extend ? 1 : 0),
            ("$prepare_spells",   spellcasting.prepare.HasValue ? (spellcasting.prepare.Value ? 1 : 0) : DBNull.Value),
            ("$allow_replace",    spellcasting.allowReplace.HasValue ? (spellcasting.allowReplace.Value ? 1 : 0) : DBNull.Value),
            ("$list_text",        (object?)spellcasting.list?.raw ?? DBNull.Value),
            ("$extend_text",      (object?)spellcasting.extendList?.raw ?? DBNull.Value));
    }

    private static void InsertSetters(SqliteConnection connection, SqliteTransaction transaction, long elementId, string ownerKind, AuroraSetters? setters)
    {
        if (setters?.entries?.Any() != true) return;

        long scopeId = InsertSetterScope(connection, transaction, ownerKind, elementId);
        int ordinal = 1;
        foreach (var entry in setters.entries)
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO setter_entries (setter_scope_id, ordinal, setter_name, setter_value) VALUES ($setter_scope_id, $ordinal, $setter_name, $setter_value);",
                ("$setter_scope_id", scopeId), ("$ordinal", ordinal++),
                ("$setter_name",  entry.name ?? string.Empty),
                ("$setter_value", (object?)entry.value ?? DBNull.Value));

            long entryId = GetLastInsertRowId(connection, transaction);
            int attrOrdinal = 1;
            foreach (var attr in entry.attributes)
            {
                ExecuteInsert(connection, transaction,
                    "INSERT INTO setter_entry_attributes (setter_entry_id, ordinal, attribute_name, attribute_value) VALUES ($setter_entry_id, $ordinal, $attribute_name, $attribute_value);",
                    ("$setter_entry_id", entryId), ("$ordinal", attrOrdinal++),
                    ("$attribute_name", attr.Key), ("$attribute_value", (object?)attr.Value ?? DBNull.Value));
            }
        }
    }

    private static void InsertExtract(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraExtract? extract)
    {
        if (extract == null) return;
        if (string.IsNullOrWhiteSpace(extract.description) && !(extract.items?.Any() == true)) return;

        ExecuteInsert(connection, transaction,
            "INSERT INTO element_extracts (element_id, description_text) VALUES ($element_id, $description_text);",
            ("$element_id", elementId), ("$description_text", (object?)extract.description ?? DBNull.Value));

        int ordinal = 1;
        foreach (var item in extract.items ?? Enumerable.Empty<AuroraItemEntry>())
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO element_extract_items (element_id, ordinal, item_text, target_aurora_id, amount_text) VALUES ($element_id, $ordinal, $item_text, $target_aurora_id, $amount_text);",
                ("$element_id",      elementId), ("$ordinal", ordinal++),
                ("$item_text",       (object?)item.value ?? DBNull.Value),
                ("$target_aurora_id",(object?)GetItemTargetAuroraId(item) ?? DBNull.Value),
                ("$amount_text",     (object?)item.GetAttribute("amount") ?? DBNull.Value));

            long extractItemId = GetLastInsertRowId(connection, transaction);
            int attrOrdinal = 1;
            foreach (var attr in item.attributes)
            {
                ExecuteInsert(connection, transaction,
                    "INSERT INTO element_extract_item_attributes (extract_item_id, ordinal, attribute_name, attribute_value) VALUES ($extract_item_id, $ordinal, $attribute_name, $attribute_value);",
                    ("$extract_item_id", extractItemId), ("$ordinal", attrOrdinal++),
                    ("$attribute_name", attr.Key), ("$attribute_value", (object?)attr.Value ?? DBNull.Value));
            }
        }
    }

    private static void InsertRules(SqliteConnection connection, SqliteTransaction transaction, long elementId, string ownerKind, Rules? rules)
    {
        if (rules == null) return;
        if (!(rules.grants?.Any() == true || rules.selects?.Any() == true || rules.stats?.Any() == true)) return;

        long ruleScopeId = InsertRuleScope(connection, transaction, ownerKind, elementId);
        int ordinal = 1;

        foreach (var grant in rules.grants ?? Enumerable.Empty<Grant>())
        {
            // target_aurora_id is set only for direct ID_* references; the raw id
            // attribute is always preserved in target_semantic_key for diagnostics.
            string? targetAuroraId = GetGrantTargetAuroraId(grant);
            ExecuteInsert(connection, transaction,
                "INSERT INTO grants (rule_scope_id, ordinal, grant_type, target_aurora_id, target_semantic_key, target_semantic_kind, target_semantic_name, raw_xml, name_text, grant_level, spellcasting_name, is_prepared, requirements_text) VALUES ($rule_scope_id, $ordinal, $grant_type, $target_aurora_id, $target_semantic_key, $target_semantic_kind, $target_semantic_name, $raw_xml, $name_text, $grant_level, $spellcasting_name, $is_prepared, $requirements_text);",
                ("$rule_scope_id",         ruleScopeId), ("$ordinal", ordinal++),
                ("$grant_type",            grant.type ?? string.Empty),
                ("$target_aurora_id",      (object?)targetAuroraId ?? DBNull.Value),
                ("$target_semantic_key",   (object?)grant.id ?? DBNull.Value),
                ("$target_semantic_kind",  (object?)grant.type ?? DBNull.Value),
                ("$target_semantic_name",  (object?)grant.name ?? DBNull.Value),
                ("$raw_xml",               (object?)grant.rawXml ?? DBNull.Value),
                ("$name_text",             (object?)grant.name ?? DBNull.Value),
                ("$grant_level",           grant.level.HasValue ? grant.level.Value : DBNull.Value),
                ("$spellcasting_name",     (object?)grant.spellcasting ?? DBNull.Value),
                ("$is_prepared",           grant.prepared.HasValue ? (grant.prepared.Value ? 1 : 0) : DBNull.Value),
                ("$requirements_text",     (object?)grant.requirements?.raw ?? DBNull.Value));
        }

        ordinal = 1;
        foreach (var select in rules.selects ?? Enumerable.Empty<Select>())
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO selects (rule_scope_id, ordinal, select_type, name_text, supports_text, select_level, number_to_choose, default_choice_text, is_optional, spellcasting_profile_id, raw_xml, requirements_text) VALUES ($rule_scope_id, $ordinal, $select_type, $name_text, $supports_text, $select_level, $number_to_choose, $default_choice_text, $is_optional, $spellcasting_profile_id, $raw_xml, $requirements_text);",
                ("$rule_scope_id",           ruleScopeId), ("$ordinal", ordinal++),
                ("$select_type",             select.type ?? string.Empty),
                ("$name_text",               select.name ?? string.Empty),
                ("$supports_text",           (object?)select.supports?.raw ?? DBNull.Value),
                ("$select_level",            select.level.HasValue ? select.level.Value : DBNull.Value),
                ("$number_to_choose",        select.number),
                ("$default_choice_text",     (object?)select.defaultChoice ?? DBNull.Value),
                ("$is_optional",             select.optional ? 1 : 0),
                ("$spellcasting_profile_id", ResolveSpellcastingProfileId(connection, transaction, elementId, select.spellcasting)),
                ("$raw_xml",                 (object?)select.rawXml ?? DBNull.Value),
                ("$requirements_text",       (object?)select.requirements?.raw ?? DBNull.Value));

            long selectId = GetLastInsertRowId(connection, transaction);
            int suppOrdinal = 1;
            foreach (var s in select.supports ?? Enumerable.Empty<string>())
                ExecuteInsert(connection, transaction,
                    "INSERT INTO select_supports (select_id, ordinal, support_text) VALUES ($select_id, $ordinal, $support_text);",
                    ("$select_id", selectId), ("$ordinal", suppOrdinal++), ("$support_text", s));

            InsertSelectItems(connection, transaction, selectId, select, select.items);
        }

        ordinal = 1;
        foreach (var stat in rules.stats ?? Enumerable.Empty<Stat>())
        {
            ExecuteInsert(connection, transaction,
                "INSERT INTO stats (rule_scope_id, ordinal, stat_name, value_expression_text, bonus_expression_text, equipped_expression_text, stat_level, inline_display, alt_text, raw_xml, requirements_text) VALUES ($rule_scope_id, $ordinal, $stat_name, $value_expression_text, $bonus_expression_text, $equipped_expression_text, $stat_level, $inline_display, $alt_text, $raw_xml, $requirements_text);",
                ("$rule_scope_id",           ruleScopeId), ("$ordinal", ordinal++),
                ("$stat_name",               stat.name ?? string.Empty),
                ("$value_expression_text",   (object?)stat.value ?? DBNull.Value),
                ("$bonus_expression_text",   (object?)stat.bonus ?? DBNull.Value),
                ("$equipped_expression_text",(object?)stat.equipped?.raw ?? DBNull.Value),
                ("$stat_level",              stat.level.HasValue ? stat.level.Value : DBNull.Value),
                ("$inline_display",          stat.inline ? 1 : 0),
                ("$alt_text",                (object?)stat.alt ?? DBNull.Value),
                ("$raw_xml",                 (object?)stat.rawXml ?? DBNull.Value),
                ("$requirements_text",       (object?)stat.requirements?.raw ?? DBNull.Value));
        }
    }

    private static void InsertSelectItems(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long selectId,
        Select select,
        IEnumerable<AuroraItemEntry>? items)
    {
        if (items?.Any() != true) return;
        int ordinal = 1;
        foreach (var item in items)
        {
            string? targetAuroraId = GetItemTargetAuroraId(item);
            string optionKind = DetermineSelectItemOptionKind(select, item, targetAuroraId);
            ExecuteInsert(connection, transaction,
                "INSERT INTO select_items (select_id, ordinal, item_text, target_aurora_id, option_kind) VALUES ($select_id, $ordinal, $item_text, $target_aurora_id, $option_kind);",
                ("$select_id", selectId), ("$ordinal", ordinal++),
                ("$item_text",       (object?)item.value ?? DBNull.Value),
                ("$target_aurora_id",(object?)targetAuroraId ?? DBNull.Value),
                ("$option_kind",     optionKind));

            long selectItemId = GetLastInsertRowId(connection, transaction);
            int attrOrdinal = 1;
            foreach (var attr in item.attributes)
            {
                ExecuteInsert(connection, transaction,
                    "INSERT INTO select_item_attributes (select_item_id, ordinal, attribute_name, attribute_value) VALUES ($select_item_id, $ordinal, $attribute_name, $attribute_value);",
                    ("$select_item_id", selectItemId), ("$ordinal", attrOrdinal++),
                    ("$attribute_name", attr.Key), ("$attribute_value", (object?)attr.Value ?? DBNull.Value));
            }
        }
    }

    private static void InsertSpellRecord(SqliteConnection connection, SqliteTransaction transaction, long elementId, AuroraSpell spell)
    {
        ExecuteInsert(connection, transaction,
            @"INSERT INTO spells
(element_id, spell_level, school_name, casting_time_text, range_text, duration_text, has_verbal, has_somatic, has_material, material_text, is_concentration, is_ritual, attack_type, damage_type_text, damage_formula_text, dc_ability_name, dc_success_text, source_url)
VALUES
($element_id, $spell_level, $school_name, $casting_time_text, $range_text, $duration_text, $has_verbal, $has_somatic, $has_material, $material_text, $is_concentration, $is_ritual, $attack_type, $damage_type_text, $damage_formula_text, $dc_ability_name, $dc_success_text, $source_url);",
            ("$element_id",          elementId),
            ("$spell_level",         spell.level),
            ("$school_name",         (object?)spell.school?.index ?? DBNull.Value),
            ("$casting_time_text",   (object?)spell.casting_time ?? DBNull.Value),
            ("$range_text",          (object?)spell.range ?? DBNull.Value),
            ("$duration_text",       (object?)spell.duration ?? DBNull.Value),
            ("$has_verbal",          spell.hasVerbal ? 1 : 0),
            ("$has_somatic",         spell.hasSomatic ? 1 : 0),
            ("$has_material",        spell.hasMaterial ? 1 : 0),
            ("$material_text",       (object?)spell.material ?? DBNull.Value),
            ("$is_concentration",    spell.concentration ? 1 : 0),
            ("$is_ritual",           spell.ritual ? 1 : 0),
            ("$attack_type",         (object?)spell.attack_type ?? DBNull.Value),
            ("$damage_type_text",    (object?)spell.damage?.damage_type?.index ?? DBNull.Value),
            ("$damage_formula_text", JsonSerializer.Serialize(spell.damage?.damage_at_slot_level, new JsonSerializerOptions { IncludeFields = true })),
            ("$dc_ability_name",     (object?)spell.dc?.index ?? DBNull.Value),
            ("$dc_success_text",     (object?)spell.dc?.dc_success ?? DBNull.Value),
            ("$source_url",          (object?)spell.url ?? DBNull.Value));

        if (spell.classes?.Any() == true)
        {
            int ordinal = 1;
            foreach (var access in spell.classes)
                ExecuteInsert(connection, transaction,
                    "INSERT INTO spell_access (spell_element_id, ordinal, access_text) VALUES ($spell_element_id, $ordinal, $access_text);",
                    ("$spell_element_id", elementId), ("$ordinal", ordinal++), ("$access_text", access.name ?? ""));
        }
    }

    // ── Public cache-only rebuild ────────────────────────────────────────────

    /// <summary>
    /// Re-runs the resolution cache build and FK resolve without re-importing any files.
    /// Call this after toggling <c>content_packages.is_enabled</c> so that the new
    /// enable/disable state is reflected in the cache and FK columns immediately.
    /// </summary>
    internal static void RebuildCacheOnly(string sqlitePath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        BuildResolutionCache(connection, transaction);
        ResolveDeferredRelationships(connection, transaction);
        transaction.Commit();
    }

    // ── FK resolution ────────────────────────────────────────────────────────

    // ── Resolution cache ─────────────────────────────────────────────────────

    private static bool IsCacheEmpty(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM resolved_elements_cache;";
        return (long)(cmd.ExecuteScalar() ?? 0L) == 0;
    }

    private static void BuildResolutionCache(SqliteConnection connection, SqliteTransaction transaction)
    {
        ExecuteSql(connection, transaction, "DELETE FROM resolved_unique_element_names_cache;");
        ExecuteSql(connection, transaction, "DELETE FROM resolved_elements_cache;");

        ExecuteSql(connection, transaction, @"
INSERT INTO resolved_elements_cache
(
    aurora_id,
    winning_element_id,
    source_file_id,
    content_package_id,
    package_key,
    package_name,
    package_kind,
    precedence_rank,
    duplicate_count,
    resolution_rank
)
WITH ranked AS
(
    SELECT
        e.aurora_id,
        e.element_id AS winning_element_id,
        e.source_file_id,
        sf.content_package_id,
        cp.package_key,
        cp.package_name,
        cp.package_kind,
        cp.precedence_rank,
        COUNT(*) OVER (PARTITION BY e.aurora_id) AS duplicate_count,
        ROW_NUMBER() OVER
        (
            PARTITION BY e.aurora_id
            ORDER BY
                COALESCE(cp.is_enabled, 1) DESC,
                COALESCE(cp.precedence_rank, 500) DESC,
                CASE COALESCE(cp.package_kind, 'local')
                    WHEN 'local'        THEN 5
                    WHEN 'homebrew'     THEN 4
                    WHEN 'third-party'  THEN 3
                    WHEN 'official'     THEN 2
                    WHEN 'core'         THEN 1
                    ELSE 0
                END DESC,
                e.source_file_id ASC,
                e.element_id ASC
        ) AS resolution_rank
    FROM elements AS e
    JOIN source_files AS sf
        ON sf.source_file_id = e.source_file_id
    LEFT JOIN content_packages AS cp
        ON cp.content_package_id = sf.content_package_id
    WHERE e.aurora_id IS NOT NULL
      AND trim(e.aurora_id) <> ''
      AND COALESCE(cp.is_enabled, 1) = 1
)
SELECT
    aurora_id,
    winning_element_id,
    source_file_id,
    content_package_id,
    package_key,
    package_name,
    package_kind,
    precedence_rank,
    duplicate_count,
    resolution_rank
FROM ranked
WHERE resolution_rank = 1;");

        ExecuteSql(connection, transaction, @"
INSERT INTO resolved_unique_element_names_cache
(
    normalized_name,
    winning_element_id,
    name
)
WITH named AS
(
    SELECT
        rec.winning_element_id,
        e.name,
        lower(trim(e.name)) AS normalized_name,
        COUNT(*) OVER (PARTITION BY lower(trim(e.name))) AS match_count
    FROM resolved_elements_cache AS rec
    JOIN elements AS e
        ON e.element_id = rec.winning_element_id
    WHERE e.name IS NOT NULL
      AND trim(e.name) <> ''
)
SELECT
    normalized_name,
    winning_element_id,
    name
FROM named
WHERE match_count = 1;");
    }

    // ── FK resolution ─────────────────────────────────────────────────────────

    private static void ResolveDeferredRelationships(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Reset all deferred FKs for a full, cache-driven rebuild.
        ExecuteSql(connection, transaction, @"
UPDATE grants
SET target_element_id = NULL
WHERE target_aurora_id IS NOT NULL;");

        ExecuteSql(connection, transaction, "UPDATE element_extract_items SET linked_element_id = NULL;");
        ExecuteSql(connection, transaction, "UPDATE select_items SET linked_element_id = NULL;");
        ExecuteSql(connection, transaction, "UPDATE subraces SET race_element_id = NULL;");
        ExecuteSql(connection, transaction, "UPDATE race_variants SET race_element_id = NULL;");
        ExecuteSql(connection, transaction, "UPDATE background_variants SET background_element_id = NULL;");

        ExecuteSql(connection, transaction, @"
UPDATE features
SET parent_element_id = NULL
WHERE parent_support_text IS NOT NULL;");

        ExecuteSql(connection, transaction, "UPDATE archetypes SET parent_class_element_id = NULL;");
        ExecuteSql(connection, transaction, "DELETE FROM select_support_links;");
        ExecuteSql(connection, transaction, "DELETE FROM element_support_links;");

        ExecuteSql(connection, transaction, @"
DELETE FROM support_tags
WHERE support_text <> '[[inline-item]]'
  AND NOT EXISTS (SELECT 1 FROM element_supports WHERE element_supports.support_text = support_tags.support_text)
  AND NOT EXISTS (SELECT 1 FROM select_supports  WHERE select_supports.support_text  = support_tags.support_text);");

        ExecuteSql(connection, transaction, @"
UPDATE support_tags
SET support_kind = 'unclassified'
WHERE support_text <> '[[inline-item]]';");

        // grants — direct aurora_id lookup via cache
        ExecuteSql(connection, transaction, @"
UPDATE grants SET target_element_id =
(
    SELECT rec.winning_element_id
    FROM resolved_elements_cache AS rec
    WHERE rec.aurora_id = grants.target_aurora_id
)
WHERE target_element_id IS NULL AND target_aurora_id IS NOT NULL;");

        // element_extract_items — prefer aurora_id cache, fall back to unique name cache
        ExecuteSql(connection, transaction, @"
UPDATE element_extract_items SET linked_element_id =
(
    SELECT COALESCE(
        (SELECT rec.winning_element_id  FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = element_extract_items.target_aurora_id),
        (SELECT runc.winning_element_id FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(element_extract_items.item_text)))
    )
)
WHERE linked_element_id IS NULL
  AND (target_aurora_id IS NOT NULL OR item_text IS NOT NULL);");

        // select_items — same pattern; skip text-choice options
        ExecuteSql(connection, transaction, @"
UPDATE select_items SET linked_element_id =
(
    SELECT COALESCE(
        (SELECT rec.winning_element_id  FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = select_items.target_aurora_id),
        (SELECT runc.winning_element_id FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(select_items.item_text)))
    )
)
WHERE option_kind <> 'text-choice'
  AND linked_element_id IS NULL
  AND (target_aurora_id IS NOT NULL OR item_text IS NOT NULL);");

        // subraces — restrict parent to cache winners
        ExecuteSql(connection, transaction, @"
UPDATE subraces SET race_element_id =
(
    SELECT MIN(parent.element_id)
    FROM races AS r
    JOIN elements AS parent ON parent.element_id = r.element_id
    JOIN resolved_elements_cache AS rec ON rec.winning_element_id = parent.element_id
    WHERE parent.aurora_id = subraces.parent_support_text OR parent.name = subraces.parent_support_text
       OR subraces.parent_support_text = parent.name || ' Subrace'
       OR subraces.parent_support_text = parent.name || ' Ancestry'
       OR subraces.parent_support_text LIKE '% ' || parent.name
)
WHERE race_element_id IS NULL AND parent_support_text IS NOT NULL;");

        // race_variants — restrict parent to cache winners
        ExecuteSql(connection, transaction, @"
UPDATE race_variants SET race_element_id =
(
    SELECT MIN(parent.element_id)
    FROM races AS r
    JOIN elements AS parent ON parent.element_id = r.element_id
    JOIN resolved_elements_cache AS rec ON rec.winning_element_id = parent.element_id
    WHERE parent.aurora_id = race_variants.parent_support_text OR parent.name = race_variants.parent_support_text
       OR race_variants.parent_support_text = parent.name || ' Variant'
       OR trim(replace(replace(race_variants.parent_support_text, 'Variant ', ''), ' Variant', '')) = parent.name
)
WHERE race_element_id IS NULL AND parent_support_text IS NOT NULL;");

        // background_variants — restrict parent to cache winners
        ExecuteSql(connection, transaction, @"
UPDATE background_variants SET background_element_id =
(
    SELECT MIN(parent.element_id)
    FROM backgrounds AS b
    JOIN elements AS parent ON parent.element_id = b.element_id
    JOIN resolved_elements_cache AS rec ON rec.winning_element_id = parent.element_id
    WHERE parent.aurora_id = background_variants.parent_support_text OR parent.name = background_variants.parent_support_text
       OR background_variants.parent_support_text = 'Variant ' || parent.name
       OR trim(replace(background_variants.parent_support_text, 'Variant ', '')) = parent.name
)
WHERE background_element_id IS NULL AND parent_support_text IS NOT NULL;");

        // features — restrict parent to cache winners
        ExecuteSql(connection, transaction, @"
UPDATE features SET parent_element_id =
(
    SELECT MIN(parent.element_id)
    FROM elements AS parent
    JOIN resolved_elements_cache AS rec ON rec.winning_element_id = parent.element_id
    WHERE parent.aurora_id = features.parent_support_text OR parent.name = features.parent_support_text
)
WHERE parent_element_id IS NULL AND parent_support_text IS NOT NULL;");

        // archetypes — restrict class parent to cache winners, known alias list
        ExecuteSql(connection, transaction, @"
UPDATE archetypes SET parent_class_element_id =
(
    SELECT MIN(class_element.element_id)
    FROM elements AS class_element
    JOIN element_types AS et ON et.element_type_id = class_element.element_type_id
    JOIN resolved_elements_cache AS rec ON rec.winning_element_id = class_element.element_id
    WHERE et.type_name = 'Class' AND
    (
        class_element.name = archetypes.parent_support_text
        OR archetypes.parent_support_text = class_element.name || ' Subclass'
        OR (archetypes.parent_support_text = 'Sacred Oath'         AND class_element.name = 'Paladin')
        OR (archetypes.parent_support_text = 'Divine Domain'       AND class_element.name = 'Cleric')
        OR (archetypes.parent_support_text = 'Bard College'        AND class_element.name = 'Bard')
        OR (archetypes.parent_support_text = 'Druid Circle'        AND class_element.name = 'Druid')
        OR (archetypes.parent_support_text = 'Martial Archetype'   AND class_element.name = 'Fighter')
        OR (archetypes.parent_support_text = 'Monastic Tradition'  AND class_element.name = 'Monk')
        OR (archetypes.parent_support_text = 'Ranger Archetype'    AND class_element.name = 'Ranger')
        OR (archetypes.parent_support_text = 'Ranger Conclave'     AND class_element.name = 'Ranger')
        OR (archetypes.parent_support_text = 'Roguish Archetype'   AND class_element.name = 'Rogue')
        OR (archetypes.parent_support_text = 'Sorcerous Origin'    AND class_element.name = 'Sorcerer')
        OR (archetypes.parent_support_text = 'Arcane Tradition'    AND class_element.name = 'Wizard')
        OR (archetypes.parent_support_text = 'Otherworldly Patron' AND class_element.name = 'Warlock')
        OR (archetypes.parent_support_text = 'Primal Path'         AND class_element.name = 'Barbarian')
        OR (archetypes.parent_support_text = 'Blood Hunter Order'  AND class_element.name = 'Blood Hunter')
        OR (archetypes.parent_support_text = 'Artificer Specialist' AND class_element.name = 'Artificer')
        OR (archetypes.parent_support_text = 'UA Artificer Specialist' AND class_element.name = 'Artificer')
        OR (archetypes.parent_support_text = 'Bender Discipline'   AND class_element.name = 'Bender')
        OR (archetypes.parent_support_text = 'Avenger Archetype'   AND class_element.name = 'Avenger')
        OR (archetypes.parent_support_text = 'Kibbles Psionic Archetype' AND class_element.name = 'Psion')
    )
)
WHERE parent_class_element_id IS NULL AND parent_support_text IS NOT NULL;");

        // archetypes fallback — same-file class
        ExecuteSql(connection, transaction, @"
UPDATE archetypes SET parent_class_element_id =
(
    SELECT MIN(class_element.element_id)
    FROM elements AS archetype_element
    JOIN elements AS class_element ON class_element.source_file_id = archetype_element.source_file_id
    JOIN element_types AS et ON et.element_type_id = class_element.element_type_id
    WHERE archetype_element.element_id = archetypes.element_id AND et.type_name = 'Class'
)
WHERE parent_class_element_id IS NULL;");

        // support tags & links — use cache for element lookups
        ExecuteSql(connection, transaction, @"
INSERT OR IGNORE INTO support_tags (support_text, normalized_text)
SELECT support_text, lower(trim(support_text))
FROM (SELECT support_text FROM element_supports UNION SELECT support_text FROM select_supports);");

        ExecuteSql(connection, transaction, @"
INSERT OR IGNORE INTO support_tags (support_text, normalized_text, support_kind)
VALUES ('[[inline-item]]', '[[inline-item]]', 'bounded-option-set');");

        ExecuteSql(connection, transaction, @"
INSERT OR IGNORE INTO element_support_links
    (element_id, ordinal, support_tag_id, linked_element_id, resolution_kind, is_primary_parent)
SELECT
    es.element_id,
    es.ordinal,
    st.support_tag_id,
    COALESCE(
        (SELECT rec.winning_element_id  FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = es.support_text),
        (SELECT runc.winning_element_id FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(es.support_text)))
    ),
    CASE
        WHEN EXISTS(SELECT 1 FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = es.support_text)               THEN 'aurora-id'
        WHEN EXISTS(SELECT 1 FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(es.support_text))) THEN 'element-name'
        WHEN es.support_text LIKE '$(%' THEN 'dynamic'
        ELSE 'support-category'
    END,
    0
FROM element_supports AS es
JOIN support_tags AS st ON st.support_text = es.support_text;");

        ExecuteSql(connection, transaction, @"
INSERT OR IGNORE INTO select_support_links
    (select_id, ordinal, support_tag_id, linked_element_id, resolution_kind)
SELECT
    ss.select_id,
    ss.ordinal,
    st.support_tag_id,
    COALESCE(
        (SELECT rec.winning_element_id  FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = ss.support_text),
        (SELECT runc.winning_element_id FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(ss.support_text)))
    ),
    CASE
        WHEN EXISTS(SELECT 1 FROM resolved_elements_cache             AS rec  WHERE rec.aurora_id       = ss.support_text)               THEN 'aurora-id'
        WHEN EXISTS(SELECT 1 FROM resolved_unique_element_names_cache AS runc WHERE runc.normalized_name = lower(trim(ss.support_text))) THEN 'element-name'
        WHEN ss.support_text LIKE '$(%' THEN 'dynamic'
        ELSE 'support-category'
    END
FROM select_supports AS ss
JOIN support_tags AS st ON st.support_text = ss.support_text;");
    }

    // ── Package kind refinement ──────────────────────────────────────────────

    private static void RefreshPackageKinds(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Reclassify packages that defaulted to 'third-party' but whose Source
        // element carries no affirmative is_third_party flag as 'homebrew'.
        // Packages with explicit is_third_party=1 (e.g. Ryoko's Guide) are untouched.
        // This runs unconditionally so incremental imports stay accurate too.
        ExecuteSql(connection, transaction, @"
UPDATE content_packages
SET package_kind = 'homebrew'
WHERE package_kind = 'third-party'
  AND NOT EXISTS (
      SELECT 1 FROM source_elements se
      JOIN elements e  ON e.element_id  = se.element_id
      JOIN source_files sf ON sf.source_file_id = e.source_file_id
      WHERE sf.content_package_id = content_packages.content_package_id
        AND se.is_third_party = 1
  )
  AND EXISTS (
      SELECT 1 FROM source_elements se
      JOIN elements e  ON e.element_id  = se.element_id
      JOIN source_files sf ON sf.source_file_id = e.source_file_id
      WHERE sf.content_package_id = content_packages.content_package_id
  );");
    }

    private static void UpsertDatabaseMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int sourceFileCount,
        int elementCount,
        string contentRootHash)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
INSERT INTO database_metadata
(
    singleton_id,
    schema_version,
    data_version,
    importer_version,
    built_utc,
    source_file_count,
    element_count,
    content_root_hash
)
VALUES
(
    1,
    $schema_version,
    $data_version,
    $importer_version,
    CURRENT_TIMESTAMP,
    $source_file_count,
    $element_count,
    $content_root_hash
)
ON CONFLICT(singleton_id) DO UPDATE SET
    schema_version = excluded.schema_version,
    data_version = excluded.data_version,
    importer_version = excluded.importer_version,
    built_utc = excluded.built_utc,
    source_file_count = excluded.source_file_count,
    element_count = excluded.element_count,
    content_root_hash = excluded.content_root_hash;";
        cmd.Parameters.AddWithValue("$schema_version", AuroraDatabaseVersions.SchemaVersion);
        cmd.Parameters.AddWithValue("$data_version", AuroraDatabaseVersions.DataVersion);
        cmd.Parameters.AddWithValue("$importer_version", GetImporterVersion());
        cmd.Parameters.AddWithValue("$source_file_count", sourceFileCount);
        cmd.Parameters.AddWithValue("$element_count", elementCount);
        cmd.Parameters.AddWithValue("$content_root_hash", contentRootHash);
        cmd.ExecuteNonQuery();
    }

    // ── Scope helpers ────────────────────────────────────────────────────────

    private static long InsertRuleScope(SqliteConnection connection, SqliteTransaction transaction, string ownerKind, long ownerElementId)
    {
        ExecuteInsert(connection, transaction,
            "INSERT INTO rule_scopes (owner_kind, owner_element_id, scope_key) VALUES ($owner_kind, $owner_element_id, $scope_key);",
            ("$owner_kind", ownerKind), ("$owner_element_id", ownerElementId),
            ("$scope_key", ownerKind == "class-multiclass" ? "multiclass" : "element"));
        return GetLastInsertRowId(connection, transaction);
    }

    private static long InsertSetterScope(SqliteConnection connection, SqliteTransaction transaction, string ownerKind, long ownerElementId)
    {
        ExecuteInsert(connection, transaction,
            "INSERT INTO setter_scopes (owner_kind, owner_element_id, scope_key) VALUES ($owner_kind, $owner_element_id, $scope_key);",
            ("$owner_kind", ownerKind), ("$owner_element_id", ownerElementId),
            ("$scope_key", ownerKind == "class-multiclass" ? "multiclass" : "element"));
        return GetLastInsertRowId(connection, transaction);
    }

    private static object ResolveSpellcastingProfileId(SqliteConnection connection, SqliteTransaction transaction, long ownerElementId, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return DBNull.Value;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT spellcasting_profile_id FROM spellcasting_profiles WHERE owner_element_id = $owner_element_id AND profile_name = $profile_name LIMIT 1;";
        command.Parameters.AddWithValue("$owner_element_id", ownerElementId);
        command.Parameters.AddWithValue("$profile_name", name);
        return command.ExecuteScalar() ?? DBNull.Value;
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static string? GetItemTargetAuroraId(AuroraItemEntry item)
    {
        string? attributeId = item?.GetAttribute("id");
        if (LooksLikeAuroraId(attributeId))
            return attributeId!.Trim();
        if (LooksLikeAuroraId(item?.value))
            return item!.value!.Trim();
        return null;
    }

    private static string? GetGrantTargetAuroraId(Grant grant)
    {
        if (LooksLikeAuroraId(grant?.id))
            return grant!.id!.Trim();

        if (LooksLikeAuroraId(grant?.name))
            return grant!.name!.Trim();

        return null;
    }

    private static bool LooksLikeAuroraId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.TrimStart().StartsWith("ID_", StringComparison.OrdinalIgnoreCase);

    private static string DetermineSelectItemOptionKind(Select select, AuroraItemEntry item, string? targetAuroraId)
    {
        if (!string.IsNullOrWhiteSpace(targetAuroraId))
            return "aurora-reference";

        if (string.Equals(select?.type, "List", StringComparison.OrdinalIgnoreCase))
            return "text-choice";

        string? itemText = item?.value?.Trim();
        if (IsLikelyTextChoice(select?.name, itemText))
            return "text-choice";

        return "name-reference-candidate";
    }

    private static bool IsLikelyTextChoice(string? selectName, string? itemText)
    {
        if (string.IsNullOrWhiteSpace(itemText))
            return true;

        string normalizedSelectName = (selectName ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedItemText = itemText.Trim();

        string[] textChoiceKeywords =
        [
            "personality",
            "ideal",
            "bond",
            "flaw",
            "specialty",
            "speciality",
            "trait",
            "harrowing event",
            "memento",
            "life event",
            "favorite scheme",
            "guild business",
            "characteristic"
        ];

        if (textChoiceKeywords.Any(keyword => normalizedSelectName.Contains(keyword, StringComparison.Ordinal)))
            return true;

        int wordCount = normalizedItemText
            .Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Length;

        return normalizedItemText.Contains('.')
            || normalizedItemText.Contains(',')
            || normalizedItemText.Contains(';')
            || normalizedItemText.Contains(':')
            || normalizedItemText.Length >= 60
            || wordCount >= 8;
    }

    private static string? GetPreferredSupportText(AuroraTextCollection? supports, params string[] ignoredSupports)
    {
        if (supports == null || supports.Count == 0) return null;
        var ignored = new HashSet<string>(ignoredSupports.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        string? preferred = supports.FirstOrDefault(x => !ignored.Contains(x));
        return string.IsNullOrWhiteSpace(preferred) ? supports.FirstOrDefault() : preferred;
    }

    private static string GetSpellcastingOwnerKind(string? elementType)
    {
        if (string.Equals(elementType, "Class", StringComparison.OrdinalIgnoreCase)) return "class";
        if (string.Equals(elementType, "Archetype", StringComparison.OrdinalIgnoreCase)) return "archetype";
        return "feature";
    }

    private static int DetermineLoaderPriority(string? elementType) =>
        elementType?.ToLowerInvariant() switch
        {
            "source" => 5, "race" => 10, "sub race" => 20, "race variant" => 25, "dragonmark" => 26,
            "class" => 30, "archetype" => 40, "background" => 50, "background variant" => 55,
            "feat" => 60, "language" => 70, "proficiency" => 80, "spell" => 90,
            "class feature" => 100, "archetype feature" => 110, "racial trait" => 120,
            "background feature" => 130, "background sub-feature" => 135, "feat feature" => 140,
            "ability score improvement" => 150, "grants" => 160, "companion" => 200,
            "companion action" => 210, "companion reaction" => 215, "companion trait" => 210,
            "companion feature" => 210, "monster" => 220, "weapon property" => 230,
            "weapon group" => 235, "option" => 300, "support" => 310, "rule" => 320,
            "information" => 330, "deity" => 340, "alignment" => 350, "vision" => 360,
            "condition" => 370, "magic school" => 380, "background characteristics" => 390,
            _ => 500
        };

    private static object ParseIntSetter(string? value) =>
        int.TryParse(value?.Trim(), out var n) ? (object)n : DBNull.Value;

    private static object ParseCrValue(string? crText)
    {
        if (string.IsNullOrWhiteSpace(crText)) return DBNull.Value;
        return crText.Trim() switch
        {
            "0" => (object)0.0, "1/8" => 0.125, "1/4" => 0.25, "1/2" => 0.5,
            _ => double.TryParse(crText.Trim(), out var d) ? (object)d : DBNull.Value
        };
    }

    private static int ExecuteCount(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private static ContentDatabaseHealthIssueGroup ReadHealthIssueGroup(SqliteDataReader reader) =>
        new(
            ClassifyTrustImpact(ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2)),
            ReadString(reader, 0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            Convert.ToInt32(reader.GetValue(5)));

    private static ContentDatabaseHealthIssueSample ReadHealthIssueSample(SqliteDataReader reader) =>
        new(
            ClassifyTrustImpact(ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2)),
            ReadString(reader, 0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));

    private static string ReadString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static ContentDatabaseTrustImpact ClassifyTrustImpact(string area, string status, string reason)
    {
        if (string.Equals(area, "unresolved-link", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(status, "actionable", StringComparison.OrdinalIgnoreCase)
                ? ContentDatabaseTrustImpact.Blocking
                : ContentDatabaseTrustImpact.Expected;
        }

        if (!string.Equals(area, "source-integrity", StringComparison.OrdinalIgnoreCase))
            return ContentDatabaseTrustImpact.ManualReview;

        return reason switch
        {
            "grant-target-id-in-name-attribute" => ContentDatabaseTrustImpact.AutoRecovered,
            "recovered-grant-target-id-in-name-attribute" => ContentDatabaseTrustImpact.AutoRecovered,
            "duplicate-element-id-in-file" => ContentDatabaseTrustImpact.ManualReview,
            "blank-grant-target-id" => ContentDatabaseTrustImpact.ManualReview,
            "blank-select-type" => ContentDatabaseTrustImpact.ManualReview,
            "blank-stat-name" => ContentDatabaseTrustImpact.ManualReview,
            _ => ContentDatabaseTrustImpact.ManualReview
        };
    }

    private static int QueryElementCount(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM elements;";
        return Convert.ToInt32((long)(cmd.ExecuteScalar() ?? 0L));
    }

    private static string ComputeContentRootHash(IReadOnlyDictionary<string, string> fileHashesByPath)
    {
        using var md5 = MD5.Create();
        string payload = string.Join(
            "\n",
            fileHashesByPath
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}|{kvp.Value}"));
        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static string GetImporterVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private static int? GetMinimumLevel(AuroraElement element)
    {
        var levels = new List<int>();
        if (element.sheet?.description?.Any() == true)
            levels.AddRange(element.sheet.description.Where(x => x.level.HasValue).Select(x => x.level!.Value));
        if (element.rules?.grants?.Any() == true)
            levels.AddRange(element.rules.grants.Where(x => x.level.HasValue).Select(x => x.level!.Value));
        if (element.rules?.selects?.Any() == true)
            levels.AddRange(element.rules.selects.Where(x => x.level.HasValue).Select(x => x.level!.Value));
        if (element.rules?.stats?.Any() == true)
            levels.AddRange(element.rules.stats.Where(x => x.level.HasValue).Select(x => x.level!.Value));
        return levels.Count > 0 ? levels.Min() : null;
    }

    private static bool IsFeatureType(string? elementType) =>
        string.Equals(elementType, "Class Feature", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Archetype Feature", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Racial Trait", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Background Feature", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Feat Feature", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Ability Score Improvement", StringComparison.OrdinalIgnoreCase);

    private static bool IsItemType(string? elementType) =>
        string.Equals(elementType, "Item", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Weapon", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Armor", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Ammunition", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Mount", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Vehicle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(elementType, "Magic Item", StringComparison.OrdinalIgnoreCase);

    private static void ExecuteSql(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long GetLastInsertRowId(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    private static void ExecuteInsert(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var p in parameters)
            command.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
