using Builder.Data.Files;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Aurora.Importer;

public sealed record LocalCorrectionStatus(string FilePath, string SourcePath, string Status, string ReviewDetails);
public sealed record LocalCorrectionRuntimeContent(string EffectiveXml, IReadOnlyList<string> SuppressedIds, string SourcePath);

/// <summary>Stages effective content without rewriting authoritative or local XML.</summary>
public static class LocalCorrectionSync
{
    private sealed record FileState(string Root, string Relative, string Path, string Hash);
    private sealed record ManagedFile(FileState File, LocalCorrectionEvaluation Evaluation);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static async Task<AuroraImportResult> ImportAsync(IReadOnlyList<string> roots, string database,
        Func<IReadOnlyList<string>, string, CancellationToken, Task<AuroraImportResult>> import,
        CancellationToken cancellationToken = default)
    {
        var files = roots.SelectMany(root => Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
            .Select(path => new FileState(root, Path.GetRelativePath(root, path), path, Hash(path)))).ToList();
        var managed = new List<ManagedFile>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Only local files are eligible. Unknown metadata versions are rejected by FromFile.
            if (!file.Relative.Replace('\\', '/').StartsWith("user/local/", StringComparison.OrdinalIgnoreCase)) continue;
            var evaluation = LocalCorrectionDocument.FromFile(file.Path, file.Root);
            if (evaluation != null) managed.Add(new(file, evaluation));
        }
        if (managed.GroupBy(m => LocalCorrectionDocument.ResolveSourcePath(m.File.Root, m.Evaluation.SourcePath), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidDataException("More than one managed local file targets an authoritative file; consolidate or review them first.");
        if (managed.SelectMany(m => m.Evaluation.Corrections).Where(c => c.Group != null).GroupBy(c => c.Group)
            .Any(g => g.Select(c => c.State).Distinct().Count() > 1))
            throw new InvalidDataException("Related corrections across files must be reviewed together.");

        // Even an empty managed set must remove obsolete mirror rows/effective content
        // when the user removes their last correction file.
        bool hasMirror = ReadStatuses(database).Count > 0;
        if (managed.Count == 0 && !hasMirror)
            return await import(roots, database, cancellationToken);

        string work = Path.Combine(Path.GetTempPath(), "aurora-correction-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string candidate = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!, ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            var stagedRoots = new List<string>();
            // Keep root basenames stable: multi-root catalog prefixes depend on them.
            foreach (var root in roots)
            {
                string stage = Path.Combine(work, stagedRoots.Count.ToString(), new DirectoryInfo(root).Name);
                stagedRoots.Add(stage);
                Directory.CreateDirectory(stage);
                foreach (var file in files.Where(f => f.Root == root))
                {
                    string destination = Path.Combine(stage, file.Relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file.Path, destination);
                }
                foreach (var entry in managed.Where(m => m.File.Root == root))
                {
                    string origin = LocalCorrectionDocument.ResolveSourcePath(stage, entry.Evaluation.SourcePath);
                    File.WriteAllText(origin, entry.Evaluation.EffectiveXml);
                    // Preserve file bookkeeping, but import no second set of local definitions.
                    var local = LocalCorrectionDocument.Parse(entry.Evaluation.LocalXml);
                    local.Root!.Elements().Where(e => e.Name != "info").Remove();
                    File.WriteAllText(Path.Combine(stage, entry.File.Relative), local.ToString(SaveOptions.DisableFormatting));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
            if (File.Exists(database))
            {
                using var source = AuroraContentImporter.OpenReadableConnection(database);
                using var destination = Open(candidate);
                source.BackupDatabase(destination);
            }
            var result = await import(stagedRoots, candidate, cancellationToken);
            if (!result.Success) return result;
            cancellationToken.ThrowIfCancellationRequested();
            using (var connection = Open(candidate))
            {
                Execute(connection, "PRAGMA foreign_keys=ON;");
                using var check = connection.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                if ((string?)check.ExecuteScalar() != "ok") throw new InvalidDataException("Candidate database failed integrity validation.");
                check.CommandText = "PRAGMA foreign_key_check;";
                using (var reader = check.ExecuteReader())
                    if (reader.Read()) throw new InvalidDataException("Candidate database contains broken foreign keys.");
                var sourceFiles = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                check.CommandText = "SELECT source_file_id,relative_path FROM source_files";
                using (var reader = check.ExecuteReader())
                    while (reader.Read())
                        if (AuroraContentImporter.ResolveSourceFilePath(roots, reader.GetString(1)) is string path)
                            sourceFiles.Add(path, reader.GetInt64(0));
                foreach (var entry in managed)
                {
                    string origin = LocalCorrectionDocument.ResolveSourcePath(entry.File.Root, entry.Evaluation.SourcePath);
                    if (!sourceFiles.TryGetValue(origin, out long sourceFileId))
                        throw new InvalidDataException($"Corrected source file {entry.Evaluation.SourcePath} is missing from the candidate database.");
                    foreach (string id in LocalCorrectionDocument.Parse(entry.Evaluation.EffectiveXml)
                        .Root!.Elements("element").Select(e => (string?)e.Attribute("id")).OfType<string>().Distinct())
                    {
                        // Disabled packages retain imported definitions but intentionally have
                        // no resolved cache entries. Check storage and provenance independently.
                        check.CommandText = """
                            SELECT COUNT(*), COALESCE(MAX(cp.is_enabled),1)
                            FROM elements e JOIN source_files sf ON sf.source_file_id=e.source_file_id
                            LEFT JOIN content_packages cp ON cp.content_package_id=sf.content_package_id
                            WHERE e.aurora_id=$id AND e.source_file_id=$source;
                            """;
                        check.Parameters.Clear();
                        check.Parameters.AddWithValue("$id", id);
                        check.Parameters.AddWithValue("$source", sourceFileId);
                        bool enabled;
                        using (var reader = check.ExecuteReader())
                        {
                            reader.Read();
                            if (reader.GetInt64(0) == 0)
                                throw new InvalidDataException($"Corrected element {id} from {entry.Evaluation.SourcePath} is missing from the candidate database.");
                            enabled = reader.GetInt64(1) != 0;
                        }
                        if (!enabled) continue;
                        check.CommandText = "SELECT COUNT(*) FROM resolved_elements_cache WHERE aurora_id=$id";
                        if (Convert.ToInt64(check.ExecuteScalar()) != 1)
                            throw new InvalidDataException($"Enabled corrected element {id} is missing from the candidate resolution cache.");
                    }
                }
                Mirror(connection, managed);
                // Track the real inputs separately. Source-file hashes continue to describe
                // staged effective content, so the importer correctly detects the next change.
                Execute(connection, "CREATE TABLE IF NOT EXISTS local_correction_inputs (path TEXT PRIMARY KEY, sha256 TEXT NOT NULL); DELETE FROM local_correction_inputs;");
                foreach (var file in files)
                    Execute(connection, "INSERT INTO local_correction_inputs VALUES ($path,$hash);", ("$path", Path.GetFullPath(file.Path)), ("$hash", file.Hash));
                using var remaining = connection.CreateCommand();
                remaining.CommandText = "SELECT COUNT(*) FROM local_override_files";
                if (Convert.ToInt64(remaining.ExecuteScalar()) == 0)
                    Execute(connection, "DROP TABLE local_correction_inputs;");
            }
            // Refuse to activate a candidate built from files edited during the import.
            var currentPaths = roots.SelectMany(root => Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!currentPaths.SetEquals(files.Select(f => Path.GetFullPath(f.Path))) || files.Any(f => !File.Exists(f.Path) || Hash(f.Path) != f.Hash))
                throw new IOException("Content changed during sync; the working database was preserved. Retry sync.");
            cancellationToken.ThrowIfCancellationRequested();
            // All readers in this module use nonpooled connections; release writer handles too.
            SqliteConnection.ClearAllPools();
            File.Move(candidate, database, overwrite: true);
            foreach (var entry in managed.Where(m => m.Evaluation.CanRetire))
            {
                // Retain the complete XML as a recoverable, non-scanned artifact. If an
                // editor holds the file open, retry retirement on the next successful sync.
                try
                {
                    if (File.Exists(entry.File.Path) && Hash(entry.File.Path) == entry.File.Hash)
                    {
                        File.Move(entry.File.Path, entry.File.Path + ".retired-" + Guid.NewGuid().ToString("N"));
                        using var connection = Open(database);
                        Execute(connection, "UPDATE local_override_files SET status='retired' WHERE file_path=$path; DELETE FROM local_correction_inputs WHERE path=$path;", ("$path", Path.GetFullPath(entry.File.Path)));
                    }
                }
                catch (IOException) { /* Safe to leave a redundant file active until next sync. */ }
                catch (UnauthorizedAccessException) { }
                catch (SqliteException) { /* Retirement is recoverable; the database is already active. */ }
            }
            return result;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(candidate)) File.Delete(candidate);
            Directory.Delete(work, recursive: true);
        }
    }

    public static bool? IsStale(IReadOnlyList<string> roots, string database)
    {
        if (!File.Exists(database)) return null;
        using var connection = AuroraContentImporter.OpenReadableConnection(database);
        if (!HasTable(connection, "local_correction_inputs")) return null;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path,sha256 FROM local_correction_inputs";
        var recorded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = command.ExecuteReader())
            while (reader.Read()) recorded.Add(reader.GetString(0), reader.GetString(1));
        var paths = roots.SelectMany(r => Directory.EnumerateFiles(r, "*.xml", SearchOption.AllDirectories)).Select(Path.GetFullPath).ToList();
        return paths.Count != recorded.Count || paths.Any(p => !recorded.TryGetValue(p, out string? hash) || hash != Hash(p));
    }

    public static IReadOnlyList<LocalCorrectionStatus> ReadStatuses(string database)
    {
        if (!File.Exists(database)) return [];
        using var connection = AuroraContentImporter.OpenReadableConnection(database);
        if (!HasTable(connection, "local_override_files")) return [];
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT file_path,source_path,status,review_details FROM local_override_files ORDER BY file_path";
        var result = new List<LocalCorrectionStatus>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            string.Join("; ", JsonSerializer.Deserialize<string[]>(reader.GetString(3)) ?? [])));
        return result;
    }

    public static LocalCorrectionRuntimeContent? ReadRuntimeContent(string filePath, string database)
    {
        if (!File.Exists(database)) return null;
        string? root = LocalCorrectionDocument.FindContentRoot(filePath);
        if (root == null) return null;
        using var connection = AuroraContentImporter.OpenReadableConnection(database);
        if (!HasTable(connection, "local_override_files")) return null;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_path,effective_xml,suppressed_ids FROM local_override_files WHERE file_path=$path AND status <> 'retired'";
        command.Parameters.AddWithValue("$path", Path.GetFullPath(filePath));
        string source, xml, suppressed;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            source = LocalCorrectionDocument.ResolveSourcePath(root, reader.GetString(0));
            xml = reader.GetString(1);
            suppressed = reader.GetString(2);
        }
        foreach (string path in new[] { Path.GetFullPath(filePath), source })
        {
            command.CommandText = "SELECT sha256 FROM local_correction_inputs WHERE path=$path";
            command.Parameters["$path"].Value = path;
            if (!File.Exists(path) || (string?)command.ExecuteScalar() != Hash(path)) return null;
        }
        return new(xml, JsonSerializer.Deserialize<string[]>(suppressed) ?? [], source);
    }

    private static void Mirror(SqliteConnection connection, List<ManagedFile> managed)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS local_override_files (
              file_path TEXT PRIMARY KEY, source_path TEXT NOT NULL, local_xml TEXT NOT NULL,
              baseline_xml TEXT NOT NULL, upstream_xml TEXT NOT NULL, effective_xml TEXT NOT NULL,
              status TEXT NOT NULL, review_details TEXT NOT NULL, suppressed_ids TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS local_corrections (
              file_path TEXT NOT NULL REFERENCES local_override_files(file_path) ON DELETE CASCADE,
              correction_key TEXT NOT NULL, operation TEXT NOT NULL, target_id TEXT NOT NULL,
              replacement_id TEXT, original_fingerprint TEXT, state TEXT NOT NULL,
              review_group TEXT, reason TEXT, PRIMARY KEY(file_path,correction_key));
            DELETE FROM local_corrections WHERE file_path IN (SELECT file_path FROM local_override_files WHERE status <> 'retired');
            DELETE FROM local_override_files WHERE status <> 'retired';
            """, transaction: transaction);
        foreach (var entry in managed)
        {
            var e = entry.Evaluation;
            string path = Path.GetFullPath(entry.File.Path);
            Execute(connection, "INSERT OR REPLACE INTO local_override_files VALUES ($path,$source,$local,$baseline,$upstream,$effective,$status,$reviews,$suppressed)", transaction,
                ("$path", path), ("$source", e.SourcePath), ("$local", e.LocalXml), ("$baseline", e.BaselineXml),
                ("$upstream", e.UpstreamXml), ("$effective", e.EffectiveXml),
                ("$status", e.CanRetire ? "ready-to-retire" : "review-required"), ("$reviews", JsonSerializer.Serialize(e.ReviewReasons)),
                ("$suppressed", JsonSerializer.Serialize(e.SuppressedIds)));
            foreach (var c in e.Corrections)
                Execute(connection, "INSERT INTO local_corrections VALUES ($path,$key,$operation,$target,$replacement,$fingerprint,$state,$group,$reason)", transaction,
                    ("$path", path), ("$key", c.Key), ("$operation", c.Operation), ("$target", c.TargetId),
                    ("$replacement", c.ReplacementId), ("$fingerprint", c.OriginalFingerprint), ("$state", c.State), ("$group", c.Group), ("$reason", c.Reason));
        }
        transaction.Commit();
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
    private static bool HasTable(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }
    private static void Execute(SqliteConnection c, string sql, params (string, object?)[] values) => Execute(c, sql, null, values);
    private static void Execute(SqliteConnection c, string sql, SqliteTransaction? transaction, params (string, object?)[] values)
    {
        using var command = c.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
