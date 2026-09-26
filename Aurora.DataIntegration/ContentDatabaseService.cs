using Aurora.Content;
using Aurora.Content.Preparation;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

public enum ContentDatabaseSyncState { Idle, Syncing, Done, Failed }

public sealed class ContentDatabaseService
{
    public const string DatabaseFileName = "aurora-elements.sqlite";

    private readonly SemaphoreSlim _lock = new(1, 1);

    // ── State ────────────────────────────────────────────────────────────────

    public ContentDatabaseSyncState SyncState  { get; private set; } = ContentDatabaseSyncState.Idle;
    public AuroraImportProgress?    Progress   { get; private set; }
    public AuroraImportResult?      LastResult { get; private set; }

    public bool IsStale { get; private set; }
    public string? LastReadFailure { get; private set; }

    public IReadOnlyList<LocalCorrectionStatus> GetLocalCorrections() => TryRead(
        "read local corrections", () => DatabasePath is { } path
            ? ContentDatabaseReader.ReadLocalCorrections(path) : [], []);

    /// <summary>
    /// Content the last refresh could not use and left out. Read from the database, so it survives
    /// a restart and stands until the next refresh reads those files again.
    /// </summary>
    public IReadOnlyList<ContentImportSkip> GetSkippedContent() => TryRead(
        "read skipped content", () => DatabasePath is { } path
            ? ContentDatabaseReader.ReadSkippedContent(path) : [], []);

    /// <summary>Prevents raw XML recovery from undoing persisted import decisions.</summary>
    public static void ValidateRawXmlFallback(string? databasePath, string? loadFailure = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) return;
        var unavailable = ContentDatabaseReader.ReadUnavailableIds(databasePath);
        if (unavailable.Count > 0)
            throw new InvalidDataException("The database contains conflicting element IDs that must remain unavailable. " +
                "Raw XML fallback cannot bypass that decision. Review conflicts in Settings, correct the content files, and refresh the database before retrying. " +
                "Unavailable IDs: " + string.Join(", ", unavailable.OrderBy(id => id, StringComparer.Ordinal)) +
                (string.IsNullOrWhiteSpace(loadFailure) ? "" : ". Prepared load failed: " + loadFailure));

        // Retained/provisional definitions and successor choices remain usable, so they are not
        // in ReadUnavailableIds. The library persists their rejected declarations here alongside
        // skipped files and append operations. Raw XML loading cannot preserve those decisions.
        // Classification notices alone do not exclude or replace any content.
        var exclusions = ContentDatabaseReader.ReadSkippedContent(databasePath)
            .Where(issue => issue.Kind != "classification")
            .ToArray();
        if (exclusions.Length > 0)
        {
            var affectedFiles = exclusions.Select(issue => issue.RelativePath)
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            throw new InvalidDataException(
                "The database contains retained definitions or skipped content that raw XML fallback cannot preserve. " +
                "Loading was stopped to avoid replacing working definitions or restoring rejected content. " +
                "Review content issues in Settings, correct the files, and refresh the database before retrying. " +
                "Affected files: " + string.Join(", ", affectedFiles.Take(5)) +
                (affectedFiles.Length > 5 ? $" (and {affectedFiles.Length - 5} more)" : "") +
                (string.IsNullOrWhiteSpace(loadFailure) ? "" : ". Prepared load failed: " + loadFailure));
        }
    }

    /// <summary>Fires on the calling (background) thread whenever state changes.</summary>
    public event Action? StateChanged;

    // ── Path helpers ─────────────────────────────────────────────────────────

    public string? DatabasePath => GetDatabasePath();

    public string ContentDirectory => GetContentDirectory();

    public IReadOnlyList<string> ContentDirectories => ContentDirectoryResolver.GetContentDirectories();

    public static string GetContentDirectory() =>
        ContentDirectoryResolver.GetPrimaryContentDirectory();

    public static string? GetDatabasePath()
    {
        string contentDirectory = GetContentDirectory();
        return string.IsNullOrWhiteSpace(contentDirectory)
            ? null
            : Path.Combine(contentDirectory, DatabaseFileName);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public ContentDatabaseMetadata? GetMetadata() =>
        TryRead(
            "read database metadata",
            () => DatabasePath is { } p ? ContentDatabaseReader.ReadMetadata(p) : null,
            fallback: null);

    public ContentDatabaseHealthReport? GetHealthReport() =>
        TryRead(
            "read database health",
            () => DatabasePath is { } p ? ContentDatabaseReader.ReadHealth(p) : null,
            fallback: null);

    public void NotifyContentDirectoryChanged()
    {
        DbElementLoader.ResetCaches();
        SyncState  = ContentDatabaseSyncState.Idle;
        Progress   = null;
        LastResult = null;
        IsStale    = false;
        LastReadFailure = null;
        StateChanged?.Invoke();
    }

    private T TryRead<T>(string operation, Func<T> action, T fallback)
    {
        try
        {
            T result = action();
            LastReadFailure = null;
            return result;
        }
        catch (Exception ex)
        {
            RecordReadFailure(operation, ex);
            return fallback;
        }
    }

    private void RecordReadFailure(string operation, Exception ex)
    {
        string message = $"{operation}: {ex.Message}";
        if (!string.Equals(LastReadFailure, message, StringComparison.Ordinal))
            DebugLogService.Instance.Warn($"Content database could not {operation}.", ex.ToString());
        LastReadFailure = message;
    }

    // ── Staleness check ──────────────────────────────────────────────────────

    /// <summary>
    /// Fast staleness check — compares MD5 hashes in the DB against disk.
    /// Safe to call on any thread; reads the DB read-only.
    /// </summary>
    public bool CheckIsStale()
    {
        var contentDirectories = ContentDirectories;
        if (DatabasePath is not { } dbPath || contentDirectories.Count == 0)
        {
            IsStale = false;
            return false;
        }
        IsStale = ContentDatabaseReader.IsStale([ContentDirectory], dbPath);
        StateChanged?.Invoke();
        return IsStale;
    }

    /// <summary>
    /// Runs a full incremental sync. Only one sync can run at a time; concurrent callers
    /// wait for the running sync and then return its result.
    /// </summary>
    public async Task<AuroraImportResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (DatabasePath is not { } dbPath)
            {
                var fail = AuroraImportResult.Failed("Content database path could not be determined.");
                LastResult = fail;
                IsStale = false;
                StateChanged?.Invoke();
                return fail;
            }

            var contentDirectories = ContentDirectories;
            if (contentDirectories.Count == 0)
            {
                var fail = AuroraImportResult.Failed($"Content directory not found: {ContentDirectory}");
                LastResult = fail;
                IsStale = false;
                StateChanged?.Invoke();
                return fail;
            }

            SyncState = ContentDatabaseSyncState.Syncing;
            Progress  = null;
            StateChanged?.Invoke();

            // The shared library owns correction evaluation, candidate validation,
            // activation and retirement. Give it the real primary root; secondary
            // roots are composed from XML by the runtime reader.
            string contentDirectory = ContentDirectory;
            var result = await Task.Run(() => ImportAsync(contentDirectory, dbPath, cancellationToken), cancellationToken);

            LastResult = result;
            IsStale    = !result.Success;
            SyncState  = result.Success
                ? ContentDatabaseSyncState.Done
                : ContentDatabaseSyncState.Failed;
            StateChanged?.Invoke();
            return result;
        }
        catch (OperationCanceledException)
        {
            SyncState = ContentDatabaseSyncState.Idle;
            StateChanged?.Invoke();
            throw;
        }
        catch (Exception ex)
        {
            var fail = AuroraImportResult.Failed(ex.Message);
            LastResult = fail;
            SyncState  = ContentDatabaseSyncState.Failed;
            StateChanged?.Invoke();
            return fail;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AuroraImportResult> ImportAsync(string contentDirectory, string dbPath, CancellationToken cancellationToken)
    {
        // A file the library cannot use is left out and listed in Settings rather than costing the
        // user the whole refresh; the setting turns that off for anyone who wants it to stop instead.
        bool skipUnusable = ApplicationContext.Current.Settings.SkipUnusableContentOnRefresh;
        var imported = await ContentImport.ImportAsync(contentDirectory, dbPath,
            new InlineProgress<ContentImportProgress>(ReportProgress), cancellationToken,
            onDiagnostic: diagnostic => DebugLogService.Instance.Info("Content import: " + diagnostic),
            skipUnusableContent: skipUnusable);
        foreach (var skip in imported.Skipped)
            if (skip.Kind == "superseded-definition")
                DebugLogService.Instance.Info($"Archived definition superseded: {skip.Path}", skip.Detail);
            else
                DebugLogService.Instance.Warn($"Content import notice ({skip.Kind}): {skip.Path}", skip.Detail);
        return AuroraImportResult.Succeeded(imported.FilesChanged, imported.FilesUnchanged, imported.ElementsWritten,
            filesSkipped: imported.Skipped.Count(skip => skip.Kind is not ("append" or "definition-conflict" or "definition-collision" or "superseded-definition" or "classification")),
            appendOperationsSkipped: imported.Skipped.Count(skip => skip.Kind == "append"),
            unavailableDefinitions: imported.Skipped.Count(skip => skip.Kind == "definition-conflict"),
            definitionCollisions: imported.Skipped.Count(skip => skip.Kind == "definition-collision"),
            supersededDefinitions: imported.Skipped.Count(skip => skip.Kind == "superseded-definition"),
            classificationIssues: imported.Skipped.Count(skip => skip.Kind == "classification"));
    }

    private void ReportProgress(ContentImportProgress p)
    {
        Progress = MapProgress(p);
        StateChanged?.Invoke();
    }

    // The Settings bar shows Scanning as 0-50% and Importing as 50-90% from FilesScanned/FilesTotal,
    // so each library phase is scaled into a fixed share of a 1000-step range.
    internal static AuroraImportProgress MapProgress(ContentImportProgress p) => p.Phase switch
    {
        ContentImportPhase.Preparing => Scaled(AuroraImportPhase.Scanning, p, 0, 500, "Scanning content files", "files"),
        ContentImportPhase.Reading => Scaled(AuroraImportPhase.Scanning, p, 500, 500, "Reading content files", "files"),
        ContentImportPhase.Comparing => Scaled(AuroraImportPhase.Importing, p, 0, 250, "Comparing content files", "files"),
        ContentImportPhase.Writing => Scaled(AuroraImportPhase.Importing, p, 250, 750, "Writing content to database", "elements"),
        ContentImportPhase.Resolving or ContentImportPhase.Activating =>
            new AuroraImportProgress(AuroraImportPhase.Resolving, 1000, 1000, p.FilesChanged, p.ElementsWritten, null)
            {
                IsIndeterminate = true,
                StatusText = p.Phase == ContentImportPhase.Activating
                    ? "Validating and activating database…" : "Resolving relationships…"
            },
        _ => new AuroraImportProgress(AuroraImportPhase.Complete, 1000, 1000, p.FilesChanged, p.ElementsWritten, null),
    };

    private static AuroraImportProgress Scaled(AuroraImportPhase phase, ContentImportProgress p, int offset, int span,
        string activity, string units)
    {
        int step = p.Total > 0 ? (int)((long)span * p.Completed / p.Total) : span;
        return new AuroraImportProgress(phase, offset + step, 1000, p.FilesChanged, p.ElementsWritten, p.CurrentFile)
        {
            IsIndeterminate = p.Total <= 0,
            StatusText = p.Total > 0 ? $"{activity} ({p.Completed:N0} / {p.Total:N0} {units})…" : $"{activity}…"
        };
    }

    // Reports synchronously on the importing thread; StateChanged consumers already marshal to the UI.
    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
