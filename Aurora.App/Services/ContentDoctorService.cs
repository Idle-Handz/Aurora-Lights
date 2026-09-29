using Aurora.Content.Contracts;
using Microsoft.Data.Sqlite;

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

    public IReadOnlyList<OverrideFileModel> LoadOverrideFiles()
    {
        var files = new List<OverrideFileModel>();
        foreach (string path in ReadKnownOverridePaths())
        {
            OverrideFileModel model = Evaluate(path);
            files.Add(model);
        }

        return files
            .OrderByDescending(file => file.IncorporatedCount)
            .ThenBy(file => file.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Clears one correction by recording that upstream now carries it. The file is rewritten, so
    /// the content database is a refresh behind until the next import.
    /// </summary>
    public void AcceptCorrection(OverrideFileModel file, OverrideCorrectionModel correction) =>
        LocalCorrectionDocument.AcceptUpstream(
            file.FilePath, file.LocalHash, file.UpstreamHash, [correction.Key]);

    private IReadOnlyList<string> ReadKnownOverridePaths()
    {
        if (_contentDb.DatabasePath is not { } dbPath || !File.Exists(dbPath))
            return [];

        var paths = new List<string>();
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT file_path FROM local_override_files ORDER BY file_path";
            using var reader = command.ExecuteReader();
            while (reader.Read()) paths.Add(reader.GetString(0));
        }
        catch (SqliteException)
        {
            // A database built before override tracking, or none at all: nothing to show.
        }

        return paths;
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
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
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
