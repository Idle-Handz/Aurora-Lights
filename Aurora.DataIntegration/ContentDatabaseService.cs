using Aurora.Content;
using Aurora.Importer;
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
            ? LocalCorrectionSync.ReadStatuses(path) : [], []);

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

    // ── Package management ───────────────────────────────────────────────────

    /// <summary>
    /// Returns all content packages from the database, ordered by kind then name.
    /// Returns an empty list when the database does not exist yet.
    /// </summary>
    public IReadOnlyList<ContentPackageInfo> GetPackages() =>
        TryRead(
            "read content packages",
            () => DatabasePath is { } p ? AuroraContentImporter.GetPackages(p) : [],
            []);

    public ContentDatabaseMetadata? GetMetadata() =>
        TryRead(
            "read database metadata",
            () => DatabasePath is { } p ? AuroraContentImporter.GetMetadata(p) : null,
            fallback: null);

    public ContentDatabaseHealthReport? GetHealthReport() =>
        TryRead(
            "read database health",
            () => DatabasePath is { } p ? AuroraContentImporter.GetHealthReport(p) : null,
            fallback: null);

    /// <summary>
    /// Toggles an optional package's runtime preference (rebuilding only legacy caches).
    /// Fires <see cref="StateChanged"/> on completion so the UI can refresh.
    /// The caller should prompt for an element reload after calling this.
    /// </summary>
    public async Task<string?> SetPackageEnabledAsync(long packageId, bool enabled)
    {
        await _lock.WaitAsync();
        try
        {
            if (DatabasePath is not { } p)
                return "Content database path could not be determined.";

            await Task.Run(() => AuroraContentImporter.SetPackageEnabled(p, packageId, enabled));
            LastReadFailure = null;
            return null;
        }
        catch (Exception ex)
        {
            RecordReadFailure("update content package", ex);
            return ex.Message;
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

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
        IsStale = AuroraContentImporter.IsStale([ContentDirectory], dbPath);
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
        var imported = await ContentImport.ImportAsync(contentDirectory, dbPath,
            new InlineProgress<ContentImportProgress>(ReportProgress), cancellationToken,
            onDiagnostic: diagnostic => DebugLogService.Instance.Info("Content import: " + diagnostic));
        return AuroraImportResult.Succeeded(imported.FilesChanged, imported.FilesUnchanged, imported.ElementsWritten);
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
        ContentImportPhase.Preparing => Scaled(AuroraImportPhase.Scanning, p, 0, 500),
        ContentImportPhase.Reading => Scaled(AuroraImportPhase.Scanning, p, 500, 500),
        ContentImportPhase.Comparing => Scaled(AuroraImportPhase.Importing, p, 0, 250),
        ContentImportPhase.Writing => Scaled(AuroraImportPhase.Importing, p, 250, 750),
        ContentImportPhase.Resolving or ContentImportPhase.Activating =>
            new AuroraImportProgress(AuroraImportPhase.Resolving, 1000, 1000, p.FilesChanged, p.ElementsWritten, null),
        _ => new AuroraImportProgress(AuroraImportPhase.Complete, 1000, 1000, p.FilesChanged, p.ElementsWritten, null),
    };

    private static AuroraImportProgress Scaled(AuroraImportPhase phase, ContentImportProgress p, int offset, int span)
    {
        int step = p.Total > 0 ? (int)((long)span * p.Completed / p.Total) : span;
        return new AuroraImportProgress(phase, offset + step, 1000, p.FilesChanged, p.ElementsWritten, p.CurrentFile);
    }

    // Reports synchronously on the importing thread; StateChanged consumers already marshal to the UI.
    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
