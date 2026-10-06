using Aurora.Components.Models;
using Aurora.Content;
using Aurora.Content.Contracts;
using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.App.Services;

/// <summary>
/// One correction inside an override file, and whether it is still doing work.
///
/// "Pinned" means upstream still does not carry the corrected content, so the correction is the
/// only thing supplying it. "Incorporated" means upstream has since adopted it and the correction
/// is now redundant - accepting it is what lets the override file eventually retire itself.
/// </summary>
public sealed record OverrideCorrectionModel(
    string Key,
    string Operation,
    string TargetId,
    string? ReplacementId,
    string State,
    bool Incorporated,
    string? Reason)
{
    public bool Accepted => string.Equals(State, "accepted-upstream", StringComparison.Ordinal);

    public string Summary => Accepted ? "Accepted upstream"
        : Incorporated ? "Upstream has adopted this"
        : "Still needed";
}

/// <summary>
/// An override file: a local copy of an upstream content file carrying marked corrections. The
/// hashes are the state the reader is looking at, and accepting a correction passes them back so
/// the write is refused if either file moved underneath them.
/// </summary>
public sealed record OverrideFileModel(
    string FilePath,
    string DisplayName,
    string SourcePath,
    bool CanRetire,
    string LocalHash,
    string UpstreamHash,
    IReadOnlyList<OverrideCorrectionModel> Corrections,
    string? Problem)
{
    public int IncorporatedCount => Corrections.Count(c => c.Incorporated && !c.Accepted);

    public string Status => Problem is not null ? "Cannot be read"
        : CanRetire ? "Ready to retire"
        : IncorporatedCount > 0 ? $"{IncorporatedCount} ready to clear"
        : "In use";
}

/// <summary>One file's claim on an element id, and whether the catalog resolved the id to it.</summary>
public sealed record ConflictDeclarationModel(string PackageName, string RelativePath, bool IsWinner);

/// <summary>
/// An element id more than one file declares. The importer records its selected declaration and
/// preserves alternatives so the reader can inspect load-order and correction decisions.
/// </summary>
public sealed record ContentConflictModel(
    string AuroraId,
    string Name,
    string TypeName,
    IReadOnlyList<ConflictDeclarationModel> Declarations)
{
    public ConflictDeclarationModel? Winner => Declarations.FirstOrDefault(d => d.IsWinner);

    public IReadOnlyList<ConflictDeclarationModel> SetAside =>
        Declarations.Where(d => !d.IsWinner).ToList();
}

/// <summary>
/// A value in a content file that is probably a mistake, and the fix to try. <see cref="Suggested"/> is
/// set when the value is one edit from a known one (a rarity of "Vert Rare" is almost surely "Very
/// Rare"); it is null for a value that is merely not recognised, which is listed but not guessed at.
/// </summary>
public sealed record SuggestedCorrectionModel(
    string Field,
    string AuroraId,
    string Name,
    string Source,
    string RelativePath,
    string Written,
    string? Suggested)
{
    public bool HasSuggestion => Suggested is not null;
}

public sealed record ContentDoctorSnapshot(
    IReadOnlyList<OverrideFileModel> Files,
    IReadOnlyList<ContentConflictModel> Conflicts,
    IReadOnlyList<ContentImportSkip> Skipped,
    string? Problem,
    IReadOnlyList<SuggestedCorrectionModel>? Suggestions = null);

/// <summary>
/// Reads the override files the content importer knows about and re-evaluates each one against
/// what is on disk now.
///
/// The database records an evaluation from the last import, which goes stale the moment a
/// correction is accepted. Evaluating from disk instead means the page keeps telling the truth
/// without waiting for a content refresh; the database is used only to learn which files are
/// override files in the first place.
/// </summary>
public sealed class ContentDoctorService
{
    private readonly ContentDatabaseService _contentDb;

    public ContentDoctorService(ContentDatabaseService contentDb) => _contentDb = contentDb;

    public ContentDoctorSnapshot Load(CancellationToken cancellationToken = default) =>
        ReadSnapshot(_contentDb.DatabasePath, cancellationToken);

    internal static ContentDoctorSnapshot ReadSnapshot(string? dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return new([], [], [], "No content database is available. Refresh content in Settings before reviewing import diagnostics.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenReadOnly(dbPath);
            // Keep all imported evidence on the same database snapshot during a concurrent refresh.
            using var transaction = connection.BeginTransaction(deferred: true);
            IReadOnlyList<string> paths = ReadKnownOverridePaths(connection, transaction, cancellationToken);
            IReadOnlyList<ContentConflictModel> conflicts;
            using (ContentLoadTrace.Begin("doctor.conflicts-worker"))
                conflicts = ReadConflicts(connection, cancellationToken, transaction);
            IReadOnlyList<ContentImportSkip> skipped;
            using (ContentLoadTrace.Begin("doctor.skipped-worker"))
                skipped = ReadSkippedContent(connection, transaction, cancellationToken);
            IReadOnlyList<SuggestedCorrectionModel> suggestions;
            using (ContentLoadTrace.Begin("doctor.suggestions-worker"))
                suggestions = ReadSuggestions(connection, transaction, cancellationToken);
            transaction.Commit();

            var files = new List<OverrideFileModel>();
            using (ContentLoadTrace.Begin("doctor.overrides-worker"))
            {
                foreach (string path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(Evaluate(path));
                }
            }

            return new(files.OrderByDescending(file => file.IncorporatedCount)
                .ThenBy(file => file.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), conflicts, skipped, null, suggestions);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new([], [], [], $"Import diagnostics could not be read. Refresh content in Settings and re-check. {ex.Message}");
        }
    }

    /// <summary>
    /// Clears one correction by recording that upstream now carries it. The file is rewritten, so
    /// the content database is a refresh behind until the next import.
    /// </summary>
    public void AcceptCorrection(OverrideFileModel file, OverrideCorrectionModel correction) =>
        LocalCorrectionDocument.AcceptUpstream(
            file.FilePath, file.LocalHash, file.UpstreamHash, [correction.Key]);

    /// <summary>
    /// The content folder: the one the content database sits in. Paths the database records for a content
    /// file are relative to it, and the override files the importer reads live under its user/local.
    /// </summary>
    public string? ContentRoot => _contentDb.DatabasePath is { } path ? Path.GetDirectoryName(path) : null;

    /// <summary>
    /// Prepares an override file for repairs that all belong to one content file, without writing
    /// anything, so the reader can see and confirm what would be written. See <see cref="OverrideAuthoring"/>.
    /// </summary>
    public PlannedOverride PlanOverride(IReadOnlyList<SuggestedCorrectionModel> repairs) =>
        OverrideAuthoring.Plan(
            ContentRoot ?? throw new OverrideAuthoringException("No content folder is available."),
            repairs,
            DateOnly.FromDateTime(DateTime.Now));

    /// <summary>
    /// Writes a prepared override. The content database is a refresh behind until the next import, which
    /// is what makes the importer read the new file.
    /// </summary>
    public string WriteOverride(PlannedOverride plan) => OverrideAuthoring.Write(plan);

    /// <summary>Internal so a test can point it at a database without resolving the app's own.</summary>
    internal static IReadOnlyList<ContentConflictModel> ReadConflicts(string dbPath)
    {
        using var connection = OpenReadOnly(dbPath);
        return ReadConflicts(connection, CancellationToken.None);
    }

    private static SqliteConnection OpenReadOnly(string dbPath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static IReadOnlyList<ContentConflictModel> ReadConflicts(SqliteConnection connection, CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        var byId = new Dictionary<string, List<ConflictDeclarationModel>>(StringComparer.Ordinal);
        var names = new Dictionary<string, (string Name, string Type)>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT aurora_id, name, type_name, COALESCE(package_name, ''), relative_path, is_winner
            FROM v_duplicate_aurora_ids
            ORDER BY aurora_id, is_winner DESC, relative_path
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string id = reader.GetString(0);
            if (!byId.TryGetValue(id, out var declarations))
                byId[id] = declarations = [];
            declarations.Add(new(reader.GetString(3), reader.GetString(4), reader.GetInt64(5) != 0));
            names.TryAdd(id, (reader.GetString(1), reader.GetString(2)));
        }

        return byId
            .Select(pair => new ContentConflictModel(
                pair.Key, names[pair.Key].Name, names[pair.Key].Type, pair.Value))
            .OrderBy(conflict => conflict.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(conflict => conflict.AuroraId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Internal so a test can point it at a database without resolving the app's own.</summary>
    internal static IReadOnlyList<SuggestedCorrectionModel> ReadSuggestions(string dbPath)
    {
        using var connection = OpenReadOnly(dbPath);
        return ReadSuggestions(connection, null, CancellationToken.None);
    }

    /// <summary>
    /// Rarities the content wrote that mean nothing: a typo, or a value nothing recognises. Those one
    /// edit from a real rarity come with the spelling to use; the rest are listed unguessed. A real
    /// rarity, a "varies" value, an infusion, or the content saying "Unknown" is accepted as it is.
    /// Only the declarations in use are read, since a set-aside copy changes nothing for anyone. A
    /// database without the setter tables yields nothing: these are extras on top of the diagnostics,
    /// not evidence that the content is clean.
    /// </summary>
    private static IReadOnlyList<SuggestedCorrectionModel> ReadSuggestions(SqliteConnection connection,
        SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        var found = new List<SuggestedCorrectionModel>();
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT e.aurora_id, e.name, COALESCE(sb.name, ''), COALESCE(sf.relative_path, ''), se.setter_value
                FROM resolved_elements_cache AS rec
                JOIN elements AS e ON e.element_id = rec.winning_element_id
                JOIN setter_scopes AS ss ON ss.owner_element_id = e.element_id AND ss.owner_kind = 'element'
                JOIN setter_entries AS se ON se.setter_scope_id = ss.setter_scope_id
                LEFT JOIN source_books AS sb ON sb.source_book_id = e.source_book_id
                LEFT JOIN source_files AS sf ON sf.source_file_id = e.source_file_id
                WHERE LOWER(se.setter_name) = 'rarity'
                  AND TRIM(COALESCE(se.setter_value, '')) <> ''
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string written = reader.GetString(4).Trim();
                if (RarityRepair.IsAccepted(written))
                    continue;

                found.Add(new("rarity", reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), written, RarityRepair.Suggest(written)));
            }
        }
        catch (SqliteException)
        {
            return [];
        }

        return found
            .OrderByDescending(item => item.HasSuggestion)
            .ThenBy(item => item.Written, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.AuroraId, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ReadKnownOverridePaths(SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Retired rows preserve history after the importer renames the XML to a backup.
        // Only active overrides still require a file at their original path.
        command.CommandText = "SELECT file_path FROM local_override_files WHERE status <> 'retired' ORDER BY file_path";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            paths.Add(reader.GetString(0));
        }

        return paths;
    }

    private static IReadOnlyList<ContentImportSkip> ReadSkippedContent(SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT file_path, relative_path, kind, detail, related_path FROM content_skipped_files ORDER BY skip_ordinal";
        using var reader = command.ExecuteReader();
        var skipped = new List<ContentImportSkip>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            skipped.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return skipped;
    }

    /// <summary>Evaluates one override file against what is on disk. Internal so a test can
    /// drive it without standing up a content database first.</summary>
    internal static OverrideFileModel Evaluate(string path)
    {
        string name = Path.GetFileName(path);
        if (!File.Exists(path))
            return new(path, name, string.Empty, false, string.Empty, string.Empty, [],
                "The file is no longer on disk. It may already have been retired.");

        try
        {
            string? root = LocalCorrectionDocument.FindContentRoot(path);
            if (root is null)
                return new(path, name, string.Empty, false, string.Empty, string.Empty, [],
                    "Not inside a content root, so its upstream file cannot be located.");

            LocalCorrectionEvaluation? evaluation = LocalCorrectionDocument.FromFile(path, root);
            if (evaluation is null)
                return new(path, name, string.Empty, false, string.Empty, string.Empty, [],
                    "No correction metadata; nothing here treats it as an override file.");

            string source = LocalCorrectionDocument.ResolveSourcePath(root, evaluation.SourcePath);
            var corrections = evaluation.Corrections
                .Select(correction => new OverrideCorrectionModel(
                    correction.Key, correction.Operation, correction.TargetId, correction.ReplacementId,
                    correction.State, IsIncorporated(evaluation, correction.Key), correction.Reason))
                .ToList();

            return new(path, Relative(root, path), Relative(root, source), evaluation.CanRetire,
                LocalCorrectionDocument.FileFingerprint(path),
                File.Exists(source) ? LocalCorrectionDocument.FileFingerprint(source) : string.Empty,
                corrections,
                File.Exists(source) ? null : "The upstream file it corrects is not installed.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or XmlException)
        {
            return new(path, name, string.Empty, false, string.Empty, string.Empty, [], ex.Message);
        }
    }

    /// <summary>
    /// The evaluator reports one reason per unaccepted correction, prefixed with its key. A reason
    /// saying the fix was incorporated is the signal that upstream no longer needs the correction.
    /// </summary>
    private static bool IsIncorporated(LocalCorrectionEvaluation evaluation, string key) =>
        evaluation.ReviewReasons.Any(reason =>
            reason.StartsWith(key + ":", StringComparison.Ordinal) &&
            reason.Contains("incorporated", StringComparison.OrdinalIgnoreCase));

    private static string Relative(string root, string path)
    {
        try { return Path.GetRelativePath(root, path).Replace('\\', '/'); }
        catch (ArgumentException) { return Path.GetFileName(path); }
    }
}
