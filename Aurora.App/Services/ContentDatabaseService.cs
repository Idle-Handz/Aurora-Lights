using System.Diagnostics;
using System.Text.RegularExpressions;
using Aurora.Importer;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

public enum ContentDatabaseSyncState { Idle, Syncing, Done, Failed }

public sealed class ContentDatabaseService
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    // ── State ────────────────────────────────────────────────────────────────

    public ContentDatabaseSyncState SyncState  { get; private set; } = ContentDatabaseSyncState.Idle;
    public AuroraImportProgress?    Progress   { get; private set; }
    public AuroraImportResult?      LastResult { get; private set; }

    public bool IsStale { get; private set; }

    /// <summary>Fires on the calling (background) thread whenever state changes.</summary>
    public event Action? StateChanged;

    // ── Path helpers ─────────────────────────────────────────────────────────

    public string? DatabasePath =>
        DataManager.Current.LocalAppDataRootDirectory is { Length: > 0 } root
            ? Path.Combine(root, "aurora-elements.sqlite")
            : null;

    public string ContentDirectory =>
        DataManager.Current.UserDocumentsCustomElementsDirectory ?? string.Empty;

    /// <summary>
    /// Path to the bundled AuroraTranslator snapshot published via tools/publish-translator.ps1.
    /// Null (on non-Windows) or a path that may not exist yet on first run.
    /// </summary>
    private static string? BundledTranslatorPath =>
#if WINDOWS
        Path.Combine(AppContext.BaseDirectory, "BundledTools", "AuroraTranslator", "AuroraTranslator.exe");
#else
        null;
#endif

    // ── Public API ───────────────────────────────────────────────────────────

    // ── Package management ───────────────────────────────────────────────────

    /// <summary>
    /// Returns all content packages from the database, ordered by kind then name.
    /// Returns an empty list when the database does not exist yet.
    /// </summary>
    public IReadOnlyList<ContentPackageInfo> GetPackages() =>
        DatabasePath is { } p ? AuroraContentImporter.GetPackages(p) : [];

    public ContentDatabaseMetadata? GetMetadata() =>
        DatabasePath is { } p ? AuroraContentImporter.GetMetadata(p) : null;

    public ContentDatabaseHealthReport? GetHealthReport() =>
        DatabasePath is { } p ? AuroraContentImporter.GetHealthReport(p) : null;

    /// <summary>
    /// Toggles a package's enabled state and rebuilds the resolution cache.
    /// Fires <see cref="StateChanged"/> on completion so the UI can refresh.
    /// The caller should prompt for an element reload after calling this.
    /// </summary>
    public Task SetPackageEnabledAsync(long packageId, bool enabled) => Task.Run(() =>
    {
        if (DatabasePath is not { } p) return;
        AuroraContentImporter.SetPackageEnabled(p, packageId, enabled);
        StateChanged?.Invoke();
    });

    // ── Staleness check ──────────────────────────────────────────────────────

    /// <summary>
    /// Fast staleness check — compares MD5 hashes in the DB against disk.
    /// Safe to call on any thread; reads the DB read-only.
    /// </summary>
    public bool CheckIsStale()
    {
        if (DatabasePath is not { } dbPath || string.IsNullOrWhiteSpace(ContentDirectory))
        {
            IsStale = false;
            return false;
        }
        IsStale = AuroraContentImporter.IsStale(ContentDirectory, dbPath);
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

            if (string.IsNullOrWhiteSpace(ContentDirectory) || !Directory.Exists(ContentDirectory))
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

            AuroraImportResult result;

            if (BundledTranslatorPath is { } exePath && File.Exists(exePath))
            {
                result = await SyncWithBundledTranslatorAsync(exePath, ContentDirectory, dbPath, cancellationToken);
            }
            else
            {
                var reportProgress = new Progress<AuroraImportProgress>(p =>
                {
                    Progress = p;
                    StateChanged?.Invoke();
                });
                result = await Task.Run(
                    () => AuroraContentImporter.Import(ContentDirectory, dbPath, reportProgress, cancellationToken),
                    cancellationToken);
            }

            LastResult = result;
            IsStale    = false;
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

    private async Task<AuroraImportResult> SyncWithBundledTranslatorAsync(
        string exePath, string contentDirectory, string dbPath, CancellationToken cancellationToken)
    {
        Progress = new AuroraImportProgress(AuroraImportPhase.Scanning, 0, 0, 0, 0, null);
        StateChanged?.Invoke();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName               = exePath,
            Arguments              = $"sqlite-import \"{contentDirectory}\" \"{dbPath}\"",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };
        process.Start();

        using var reg = cancellationToken.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            string msg = stderr.Length > 0 ? stderr.Trim() : $"AuroraTranslator exited with code {process.ExitCode}";
            return AuroraImportResult.Failed(msg);
        }

        // Parse "Aurora import: N elements processed (M files changed, K unchanged)."
        var match = Regex.Match(stdout,
            @"Aurora import: (\d+) elements processed \((\d+) files changed, (\d+) unchanged\)");
        if (match.Success)
        {
            int elements  = int.Parse(match.Groups[1].Value);
            int changed   = int.Parse(match.Groups[2].Value);
            int unchanged = int.Parse(match.Groups[3].Value);
            return AuroraImportResult.Succeeded(changed, unchanged, elements);
        }

        return AuroraImportResult.Succeeded(0, 0, 0);
    }
}
