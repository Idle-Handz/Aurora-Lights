using Builder.Data.Files;
using Builder.Data.Files.Updater;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

/// <summary>Result of a content-update check, so callers can distinguish a failure from "nothing to do".</summary>
public enum ContentUpdateOutcome { Updated, UpToDate, Failed }

/// <summary>
/// Manages custom homebrew content paths and .index file operations.
/// Settings are persisted to AppSettingsStore (shared with DataManager).
/// Element data must be reloaded for changes to take effect.
/// </summary>
public sealed class ContentService
{
    private readonly CharacterService _characters;
    private readonly CharacterTabService _tabs;
    private readonly ContentDatabaseService _contentDb;
    private readonly SemaphoreSlim _startupRefreshLock = new(1, 1);
    private bool _startupRefreshAttempted;

    public ContentService(CharacterService characters, CharacterTabService tabs, ContentDatabaseService contentDb)
    {
        _characters = characters;
        _tabs = tabs;
        _contentDb = contentDb;
    }

    // ── Additional custom directories ─────────────────────────────────────────

    /// <summary>
    /// Extra directories scanned for custom XML content in addition to the
    /// built-in <see cref="BuiltInCustomDirectory"/>.
    /// </summary>
    public IReadOnlyList<string> AdditionalDirectories =>
        ApplicationContext.Current.Settings.AdditionalCustomDirectories;

    /// <summary>
    /// The built-in custom elements directory: …/Documents/5e Character Builder/custom
    /// </summary>
    public string BuiltInCustomDirectory =>
        GetBuiltInCustomDirectory();

    public bool ContentReloadPending { get; private set; }

    /// <summary>
    /// Adds a directory to the additional custom content paths and persists the change.
    /// No-op if the path is already in the list or is the built-in custom directory.
    /// </summary>
    public void AddDirectory(string path)
    {
        path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var list = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        if (list.Any(d => d.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
        if (path.Equals(BuiltInCustomDirectory, StringComparison.OrdinalIgnoreCase)) return;
        list.Add(path);
        ApplicationContext.Current.Settings.Save();
        Changed?.Invoke();
    }

    /// <summary>Removes a directory from the additional custom content paths.</summary>
    public void RemoveDirectory(string path)
    {
        var list = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        int removed = list.RemoveAll(d => d.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            ApplicationContext.Current.Settings.Save();
            Changed?.Invoke();
        }
    }

    // ── .index file management ────────────────────────────────────────────────

    /// <summary>
    /// File names (without directory) of .index files installed in the built-in
    /// custom directory. Each represents a tracked content repository.
    /// </summary>
    public IReadOnlyList<string> InstalledIndexNames
    {
        get
        {
            string dir = GetBuiltInCustomDirectory();
            if (!Directory.Exists(dir)) return [];
            return Directory.GetFiles(dir, "*.index")
                            .Select(Path.GetFileName)
                            .Where(n => n != null)
                            .ToList()!;
        }
    }

    /// <summary>
    /// Downloads an .index file from <paramref name="url"/> and saves it to the
    /// built-in custom directory. Does not download content files — call
    /// <see cref="CheckForUpdatesAsync"/> afterwards.
    /// </summary>
    public async Task<(bool Success, string Message)> FetchIndexAsync(string url)
    {
        try
        {
            url = url.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url[7..];
            else if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (!url.EndsWith(".index", StringComparison.OrdinalIgnoreCase))
                return (false, "URL must point to a .index file (e.g. https://…/core.index).");

            var indexFile = await IndexFile.FromUrl(url);
            if (indexFile is null)
                return (false, "Failed to download index file — server returned no content.");

            string savePath = Path.Combine(GetBuiltInCustomDirectory(), indexFile.Info.UpdateFilename);
            indexFile.SaveContent(new FileInfo(savePath));

            Changed?.Invoke();
            return (true, $"'{indexFile.Info.UpdateFilename}' installed. Run Check for Updates to download content.");
        }
        catch (Exception ex)
        {
            DebugLogService.Catch(ex, "ContentService.FetchIndexAsync");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Runs <see cref="IndicesUpdateService"/> against all installed .index files,
    /// downloading or refreshing the referenced XML content files.
    /// </summary>
    public async Task<(ContentUpdateOutcome Outcome, string Message)> CheckForUpdatesAsync()
    {
        try
        {
            // Patch any http:// URLs inside installed index files before the DLL reads them.
            // Android blocks cleartext HTTP; index files saved by the WPF app or authored
            // with http:// URLs would otherwise fail silently on Android 9+.
            UpgradeIndexFileProtocols(GetBuiltInCustomDirectory());

            var version = typeof(ContentService).Assembly.GetName().Version ?? new Version(1, 0, 0);
            var svc = new IndicesUpdateService(version);
            bool updated = await svc.UpdateIndexFiles(GetBuiltInCustomDirectory());
            Changed?.Invoke();
            return updated
                ? (ContentUpdateOutcome.Updated,  "Content files updated. Reload content to apply changes.")
                : (ContentUpdateOutcome.UpToDate, "All content is up to date.");
        }
        catch (Exception ex)
        {
            DebugLogService.Catch(ex, "ContentService.CheckForUpdatesAsync");
            return (ContentUpdateOutcome.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Deletes an installed .index file by its filename (basename only, no directory).
    /// Returns null on success or an error message on failure.
    /// </summary>
    public string? RemoveIndex(string filename)
    {
        try
        {
            string dir = GetBuiltInCustomDirectory();
            string path = Path.Combine(dir, filename);
            if (File.Exists(path))
            {
                File.Delete(path);
                Changed?.Invoke();
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ── Reload ────────────────────────────────────────────────────────────────

    /// <summary>True when at least one character tab is open.</summary>
    public bool HasOpenTabs => _tabs.Tabs.Count > 0;

    /// <summary>
    /// Closes all open character tabs and reloads the element collection from disk.
    /// Must be called on the UI thread (or via InvokeAsync) so TabsChanged fires correctly.
    /// Returns null on success or an error message on failure.
    ///
    /// As of this build the call also runs an incremental DB sync first when the SQLite cache
    /// is out of date. The engine reads from that DB, so a download → reload chain that skipped
    /// the sync would silently still see the old content. Doing it here means every "Reload"
    /// surface (Settings, the snackbar action raised by <see cref="ContentDownloaded"/>) ends up
    /// with the engine seeing the latest disk state, no matter where it was triggered.
    /// </summary>
    public async Task<string?> ReloadContentAsync()
    {
        try
        {
            // Cheap MD5-based staleness check; only pay for the full incremental import if it
            // actually changed. No-op if no content directory is configured yet.
            if (_contentDb.CheckIsStale())
                await _contentDb.SyncAsync().ConfigureAwait(false);

            _tabs.CloseAllTabs();
            await _characters.ReloadElementsAsync();
            ClearContentReloadPending();
            return null;
        }
        catch (Exception ex)
        {
            return DebugLogService.Catch(ex, "ContentService.ReloadContentAsync");
        }
    }

    // ── Startup auto-refresh ──────────────────────────────────────────────────

    /// <summary>
    /// Raised after a successful startup content download when at least one tracked index
    /// produced new files. Carries a short human-readable message suitable for a snackbar.
    /// Subscribed by <c>MainLayout</c> so the user is offered a one-click Reload.
    /// </summary>
    public event Action<string>? ContentDownloaded;

    /// <summary>
    /// Fire-and-forget startup refresh: pulls every installed index, downloading any files
    /// the server now reports as newer. Same path as the user-visible "Check for Updates"
    /// button, but driven by the startup preference. Best-effort — failures are swallowed
    /// into the debug log so they never bubble out into the launch sequence.
    ///
    /// On a successful update the <see cref="ContentDownloaded"/> event fires; the UI layer
    /// decides how to surface it (today: a snackbar with a Reload action when no dirty tab
    /// would be lost, otherwise a passive "open Settings" prompt).
    /// </summary>
    public async Task RunStartupContentRefreshAsync()
    {
        await _startupRefreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_startupRefreshAttempted)
                return;

            _startupRefreshAttempted = true;
            var (outcome, _) = await CheckForUpdatesAsync().ConfigureAwait(false);
            if (outcome == ContentUpdateOutcome.Updated)
            {
                ContentReloadPending = true;
                Changed?.Invoke();
                ContentDownloaded?.Invoke("Content updates downloaded. Reload to apply.");
            }
        }
        catch (Exception ex)
        {
            // CheckForUpdatesAsync already catches its own exceptions and returns Failed,
            // so this is belt-and-braces against anything escaping the event handlers.
            DebugLogService.Catch(ex, "ContentService.RunStartupContentRefreshAsync");
        }
        finally
        {
            _startupRefreshLock.Release();
        }
    }

    public void ClearContentReloadPending()
    {
        if (!ContentReloadPending)
            return;

        ContentReloadPending = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Rewrites http:// to https:// in all url= attributes of every .index file under
    /// <paramref name="directory"/>. Best-effort; individual failures are silently skipped.
    /// </summary>
    private static void UpgradeIndexFileProtocols(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory, "*.index", SearchOption.AllDirectories))
        {
            try
            {
                string text = File.ReadAllText(path);
                if (!text.Contains("http://", StringComparison.Ordinal)) continue;
                File.WriteAllText(path, text.Replace("http://", "https://", StringComparison.Ordinal));
            }
            catch { }
        }
    }

    /// <summary>Fires when the directory list or installed index files change.</summary>
    public event Action? Changed;

    private string GetBuiltInCustomDirectory()
    {
        _characters.EnsureDirectoriesInitialized();
        string dir = DataManager.Current.UserDocumentsCustomElementsDirectory ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }
}
