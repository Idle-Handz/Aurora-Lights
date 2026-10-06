using Builder.Core.Events;
using Builder.Presentation;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using Builder.Presentation.Views.Sliders;
using Microsoft.Maui.Storage;
using System.Collections.Concurrent;

#if MACCATALYST
using CommunityToolkit.Maui.Storage;
#endif

namespace Aurora.App.Services;

public sealed record NewCharacterInfo(string Name, string PlayerName, string Group = "");

/// <summary>
/// Wraps DataManager to provide character file listing and full character loading.
/// InitializeDirectories() is called eagerly; InitializeElementDataAsync() is called
/// lazily on first character load (it can take a few seconds).
/// Subscribes to the EventAggregator to surface loading progress for the UI.
/// </summary>
public sealed class CharacterService :
    ISubscriber<CharacterLoadingSliderProgressEvent>,
    ISubscriber<CharacterLoadingSliderStatusUpdateEvent>
{
    private bool _directoriesInitialized;
    private bool _elementsInitialized;
    private string? _loadedContentDirectory;
    private readonly SemaphoreSlim _elementLock  = new(1, 1);

    // ── Character list cache ────────────────────────────────────────────────
    private List<CharacterFile>? _fileListCache;
    private Dictionary<string, CharacterFileDiskStamp> _fileListStampCache =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _portraitCache =
        new(StringComparer.Ordinal);
    // Serialized access to CharacterManager.Current is owned by CharacterContext; callers that
    // perform full-file loads acquire it via CharacterContext.EnterForLoadAsync().
    private volatile bool _isCharacterLoading;

    public Character? CurrentCharacter { get; private set; }
    public CharacterFile? CurrentCharacterFile { get; private set; }
    public string ElementLoadSource { get; private set; } = "Not loaded";
    public string ElementLoadSummary { get; private set; } = "Elements have not been initialized yet.";
    public string? ElementLoadDatabasePath { get; private set; }
    public int? ElementLoadSchemaVersion { get; private set; }
    public int? ElementLoadDataVersion { get; private set; }
    public string? ElementLoadImporterVersion { get; private set; }
    public string? ElementLoadBuiltUtc { get; private set; }
    public int? ElementLoadSourceFileCount { get; private set; }
    public string? ElementLoadContentRootHash { get; private set; }
    public string? ElementLoadFailureReason { get; private set; }
    public int ElementLoadSkippedElements { get; private set; }

    // ── Loading progress ────────────────────────────────────────────────────
    public int    LoadingPercent { get; private set; }
    public string LoadingStatus  { get; private set; } = "";

    /// <summary>Fires on the background thread whenever progress or status changes.</summary>
    public event Action? LoadingProgressChanged;

    public CharacterService()
    {
        ApplicationContext.Current.EventAggregator.Subscribe(this);
    }

    void ISubscriber<CharacterLoadingSliderProgressEvent>.OnHandleEvent(CharacterLoadingSliderProgressEvent e)
    {
        LoadingPercent = e.ProgressPercentage;
        LoadingProgressChanged?.Invoke();
    }

    void ISubscriber<CharacterLoadingSliderStatusUpdateEvent>.OnHandleEvent(CharacterLoadingSliderStatusUpdateEvent e)
    {
        LoadingStatus = e.StatusMessage ?? "";
        LoadingProgressChanged?.Invoke();
    }
    // ────────────────────────────────────────────────────────────────────────

    public void EnsureDirectoriesInitialized()
    {
        if (_directoriesInitialized) return;
        DataManager.Current.InitializeDirectories();
        _directoriesInitialized = true;
        SweepStaleTempFiles();
    }

    // Keep original name so callers (LoadCharacterFiles) still compile.
    public void EnsureInitialized() => EnsureDirectoriesInitialized();

    /// <summary>
    /// Deletes leftover atomic-save temp files ("&lt;name&gt;.dnd5e.&lt;guid&gt;.tmp") that a
    /// previous session may have orphaned if it was killed mid-write. Best-effort; never throws.
    /// Sweeps only the top level of the characters directory, where character files live.
    /// </summary>
    private static void SweepStaleTempFiles()
    {
        try
        {
            string dir = DataManager.Current.UserDocumentsRootDirectory;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;

            foreach (string tmp in Directory.EnumerateFiles(dir, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                if (!Path.GetFileName(tmp).Contains(".dnd5e.", StringComparison.OrdinalIgnoreCase))
                    continue;
                try { File.Delete(tmp); }
                catch { /* locked or already gone — skip */ }
            }
        }
        catch
        {
            // Never let cleanup crash startup.
        }
    }

    private string? _initDiagnostic;

    private async Task EnsureElementsLoadedAsync()
    {
        if (_elementsInitialized) return;
        using var preloadTrace = ContentLoadTrace.Begin("content.preload");
        using (ContentLoadTrace.Begin("content.wait-load-lock"))
            await _elementLock.WaitAsync();
        try
        {
            if (_elementsInitialized) return;
            EnsureDirectoriesInitialized();
            DataManager.Current.InitializeFileLogger();

            DbLoadResult dbResult = await DbElementLoader.TryLoadAsync(DataManager.Current.ElementsCollection);
            if (!dbResult.Success)
            {
                if (DataManager.Current.ElementsCollection.Count > 0)
                {
                    // A failed refresh can retain a working catalog only for its own folder. Never
                    // make the old folder's catalog reusable after a switch: _loadedContentDirectory
                    // is set only on success, so after a switch it still names the folder we left.
                    // With no record at all there is nothing to contradict, and refusing a catalog
                    // that is already loaded would leave the app unable to start rather than safer.
                    _elementsInitialized = _loadedContentDirectory is null || string.Equals(_loadedContentDirectory,
                        DataManager.Current.UserDocumentsCustomElementsDirectory, StringComparison.OrdinalIgnoreCase);
                    throw new InvalidDataException($"Content reload failed; the previous working elements were preserved. {dbResult.FailureReason}");
                }
                ContentDatabaseService.ValidateRawXmlFallback(dbResult.DatabasePath, dbResult.FailureReason);
                using (ContentLoadTrace.Begin("content.xml-fallback"))
                    await DataManager.Current.InitializeElementDataAsync();
                ElementLoadSource = "XML fallback";
                ElementLoadSummary = $"Loaded baseline content from XML. SQLite reason: {dbResult.FailureReason ?? "unknown"}";
            }
            else
            {
                ElementLoadSource = dbResult.SourceLabel;
                ElementLoadSummary = dbResult.Summary;
            }

            ElementLoadDatabasePath = dbResult.DatabasePath;
            ElementLoadSchemaVersion = dbResult.SchemaVersion;
            ElementLoadDataVersion = dbResult.DataVersion;
            ElementLoadImporterVersion = dbResult.ImporterVersion;
            ElementLoadBuiltUtc = dbResult.BuiltUtc;
            ElementLoadSourceFileCount = dbResult.SourceFileCount;
            ElementLoadContentRootHash = dbResult.ContentRootHash;
            ElementLoadFailureReason = dbResult.FailureReason;
            ElementLoadSkippedElements = dbResult.SkippedElementCount;

            InventoryItemFactory.InvalidateSearchIndex();
            // Sources are loaded now, so switched-off packages can become default restrictions.
            using (ContentLoadTrace.Begin("content.seed-source-preferences"))
                SourcePreferenceSeed.SeedDefaultRestrictions(ElementLoadDatabasePath);
            RefreshEngineSourceList();
            _loadedContentDirectory = DataManager.Current.UserDocumentsCustomElementsDirectory;
            _elementsInitialized = true;
            _ = WarmEquipmentSearchIndexAsync();

            string schemaDiagnostic = ElementLoadSchemaVersion.HasValue
                ? $"Schema version: {ElementLoadSchemaVersion.Value}"
                : "Schema version: unknown";
            _initDiagnostic = $"{ElementLoadSummary}\n{schemaDiagnostic}";
            LoadingProgressChanged?.Invoke();
        }
        finally
        {
            _elementLock.Release();
        }
    }

    /// <summary>
    /// The engine lists sources from a snapshot of the catalog taken when CharacterManager.Current
    /// is first touched, which can happen before this load finishes and always happens before a
    /// refresh replaces the catalog. Rebuild it here so the Manage tab offers the sources that were
    /// just loaded instead of whatever the catalog held at that moment - an empty list, if nothing
    /// had loaded yet.
    /// </summary>
    private static void RefreshEngineSourceList()
    {
        using var sourceTrace = ContentLoadTrace.Begin("content.rebuild-sources");
        try
        {
            CharacterManager.Current.SourcesManager.Refresh();
        }
        catch (Exception ex)
        {
            // Content is loaded either way; only the restriction list would be out of date.
            DebugLogService.Instance.LogException(ex, "CharacterService.RefreshEngineSourceList");
        }
    }

    private static async Task WarmEquipmentSearchIndexAsync()
    {
        try
        {
            await InventoryItemFactory.PrecomputeSearchIndexAsync();
        }
        catch (Exception ex)
        {
            // A picker query can retry the lazy build. Content loading itself should still
            // succeed if optional search precomputation encounters malformed custom data.
            DebugLogService.Instance.LogException(
                ex,
                "CharacterService.PrecomputeEquipmentSearchIndex");
        }
    }

    /// <summary>
    /// Starts loading element data eagerly (e.g. on app launch) so it is ready before
    /// the user selects a character. Safe to call multiple times; the SemaphoreSlim
    /// ensures only one load runs and subsequent callers return immediately.
    /// </summary>
    public Task PreloadAsync() => EnsureElementsLoadedAsync();

    /// <summary>
    /// Forces element data to be reloaded from disk on the next call to
    /// <see cref="PreloadAsync"/> or any character load. Call this after
    /// adding or updating custom content directories / index files.
    /// Callers must close all character tabs before invoking this.
    /// </summary>
    public async Task ReloadElementsAsync()
    {
        using var reloadTrace = ContentLoadTrace.Begin("content.reload-elements");
        // Catalog objects are also held by the loaded character graph. Serialize
        // refresh with character loads/edits and invalidate both preload and tab
        // context state before replacing them, even if the reload subsequently fails.
        // Otherwise reopening the same file skips Load() and mixes old/new grants.
        using var scope = await CharacterContext.EnterForLoadAsync();
        CurrentCharacter = null;
        CurrentCharacterFile = null;
        await _elementLock.WaitAsync();
        try
        {
            _elementsInitialized = false;
            _initDiagnostic = null;
            ElementLoadSource = "Not loaded";
            ElementLoadSummary = "Elements have not been initialized yet.";
            ElementLoadDatabasePath = null;
            InventoryItemFactory.InvalidateSearchIndex();
            StartingEquipmentDataLoader.Invalidate();
            ElementLoadSchemaVersion = null;
            ElementLoadDataVersion = null;
            ElementLoadImporterVersion = null;
            ElementLoadBuiltUtc = null;
            ElementLoadSourceFileCount = null;
            ElementLoadContentRootHash = null;
            ElementLoadFailureReason = null;
            ElementLoadSkippedElements = 0;
        }
        finally
        {
            _elementLock.Release();
        }
        await PreloadAsync();
    }

    public int ElementCount => _elementsInitialized ? DataManager.Current.ElementsCollection.Count : -1;

    public string CustomElementsDirectory => DataManager.Current.UserDocumentsCustomElementsDirectory ?? "(not initialized)";

    /// <summary>
    /// Returns a cached file list when the underlying .dnd5e directory has not changed.
    /// A cheap path/length/timestamp scan catches edits made by Aurora Legacy or a file manager.
    /// </summary>
    public bool TryGetCachedFiles(out IReadOnlyList<CharacterFile> files)
    {
        files = [];
        if (_fileListCache is null)
            return false;

        if (!IsFileListCacheCurrent())
        {
            InvalidateFileListCache();
            return false;
        }

        files = _fileListCache.ToList();
        return true;
    }

    /// <summary>Updates the in-memory file list cache after any mutation.</summary>
    public void UpdateFileListCache(IEnumerable<CharacterFile> files)
    {
        _fileListCache = files.ToList();
        _fileListStampCache = CaptureCharacterFileStamps();
        var activePaths = _fileListCache
            .Select(file => file.FilePath)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string cachedPath in _portraitCache.Keys)
        {
            if (!activePaths.Contains(cachedPath))
                _portraitCache.TryRemove(cachedPath, out _);
        }
    }

    /// <summary>Drops the file list cache so the next visit re-scans from disk.</summary>
    public void InvalidateFileListCache()
    {
        _fileListCache = null;
        _fileListStampCache.Clear();
        _portraitCache.Clear();
    }

    /// <summary>Stores a portrait data URL keyed by file path so portraits survive navigation.</summary>
    public void CachePortrait(string filePath, string dataUrl) =>
        _portraitCache[filePath] = dataUrl;

    /// <summary>Returns a cached portrait data URL, or null if not yet loaded.</summary>
    public string? GetCachedPortrait(string filePath) =>
        _portraitCache.TryGetValue(filePath, out var url) ? url : null;

    public IReadOnlyList<CharacterFile> LoadCharacterFiles()
    {
        EnsureInitialized();
        return DataManager.Current.LoadCharacterFiles()
            .OrderBy(x => !x.IsFavorite)
            .ThenBy(x => x.DisplayName)
            .ToList();
    }

    private bool IsFileListCacheCurrent()
    {
        Dictionary<string, CharacterFileDiskStamp> currentStamps = CaptureCharacterFileStamps();
        if (currentStamps.Count != _fileListStampCache.Count)
            return false;

        foreach (var (path, stamp) in currentStamps)
        {
            if (!_fileListStampCache.TryGetValue(path, out CharacterFileDiskStamp cachedStamp)
                || cachedStamp != stamp)
                return false;
        }

        return true;
    }

    private Dictionary<string, CharacterFileDiskStamp> CaptureCharacterFileStamps()
    {
        EnsureInitialized();
        var result = new Dictionary<string, CharacterFileDiskStamp>(StringComparer.Ordinal);
        string dir = DataManager.Current.UserDocumentsRootDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return result;

        foreach (string path in Directory.EnumerateFiles(dir, "*.dnd5e", SearchOption.TopDirectoryOnly))
        {
            CharacterFileDiskStamp? stamp = CharacterFileDiskStamp.Capture(path);
            if (stamp.HasValue)
                result[path] = stamp.Value;
        }

        return result;
    }

    /// <summary>True while a character is being loaded.</summary>
    public bool IsCharacterLoading => _isCharacterLoading;

    /// <summary>
    /// Returns true if <paramref name="file"/> is already loaded into
    /// <see cref="CurrentCharacter"/> and no other load has since overwritten it.
    /// </summary>
    public bool IsPreloaded(CharacterFile file) =>
        CharacterFileIdentity.RefersToSameFile(CurrentCharacterFile, file) && CurrentCharacter != null;

    /// <summary>
    /// Starts loading <paramref name="file"/> on a background thread without blocking
    /// the caller. Safe to call speculatively — errors are swallowed. The caller can
    /// check <see cref="IsPreloaded"/> later to see whether it finished.
    /// </summary>
    public void BeginPreload(CharacterFile file) => _ = BeginPreloadAsync(file);

    private async Task BeginPreloadAsync(CharacterFile file)
    {
        try
        {
            await LoadCharacterAsync(file);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "CharacterService.BeginPreloadAsync");
        }
    }

    public async Task<(bool Success, string Message)> LoadCharacterAsync(CharacterFile file)
    {
        // Acquire the CharacterContext lock for the whole load — this captures the active tab's
        // state, drops the active-tab reference, and serializes against any mutation sites that
        // might otherwise call EnterAsync mid-load.
        using var scope = await CharacterContext.EnterForLoadAsync();
        _isCharacterLoading = true;
        // A failed load must not inherit the previous character. OnCharacterSelected treats a
        // non-null CurrentCharacter as a usable partial load, so stale state here could attach one
        // character to another file tab and make a later save destructive.
        CurrentCharacter = null;
        CurrentCharacterFile = null;
        try
        {
            await EnsureElementsLoadedAsync();
            LoadingPercent = 0;
            LoadingStatus  = "";
            try
            {
                // Warm up the CharacterManager singleton on the current (non-thread-pool) thread.
                // Its static initializer accesses ApplicationContext.Current, which is not safe to
                // run on a Task.Run thread while element-loading background state is still active.
                _ = CharacterManager.Current;

                // Hold the context lock through both hydration and final calculations. Returning
                // to the UI between them froze the loading indicator on large characters.
                var (result, character) = await Task.Run(async () =>
                {
                    CharacterLoadCompatibilityService.PrepareForCharacterLoad();
                    var loaded = await file.Load();
                    var current = CharacterManager.Current?.Character;
                    if (current is not null)
                    {
                        // The engine restores item flags; rebuild the equipped slot references
                        // before evaluating armor-dependent statistics.
                        CharacterLoadCompatibilityService.RestoreEquippedSlots(current);
                        BuildService.ReapplyCustomFeatures(file);
                        BuildService.NormalizeSelectionState();
                        CharacterManager.Current!.ReprocessCharacter();
                    }
                    return (loaded, current);
                });
                if (character != null)
                {
                    CurrentCharacter     = character;
                    CurrentCharacterFile = file;

                    // Remember this as the MRU character so the next app launch can preload it.
                    if (!string.IsNullOrEmpty(file.FilePath))
                        Preferences.Default.Set("app.mru_character", file.FilePath);
                }
                if (!result.Success)
                {
                    // The detail and the load diagnostics belong in the log, where they can be read
                    // and copied; the toast only has to say what was lost and where to look.
                    DebugLogService.Instance.Warn($"Partial character load: {file.FileName}",
                        $"{result.Message}\n\nElements loaded: {ElementCount}\n{_initDiagnostic}\nCustom dir: {CustomElementsDirectory}");
                }
                return (result.Success, result.Success ? string.Empty : PartialLoadReport.Describe(result));
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "CharacterService.LoadCharacterAsync");
                return (false, BuildErrorMessage(ex));
            }
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "CharacterService.LoadCharacterAsync");
            return (false, BuildErrorMessage(ex));
        }
        finally
        {
            _isCharacterLoading = false;
        }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        // Walk the InnerException chain to surface the root cause (e.g. inside a TypeInitializationException).
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{ex.GetType().Name}: {ex.Message}");
        var inner = ex.InnerException;
        int depth = 0;
        while (inner != null && depth < 5)
        {
            sb.AppendLine($"  ↳ {inner.GetType().Name}: {inner.Message}");
            inner = inner.InnerException;
            depth++;
        }
        sb.AppendLine();
        sb.AppendLine("(See Console page for full stack trace)");
        sb.AppendLine();
        sb.Append(ex.StackTrace);
        return sb.ToString();
    }

    public string CharactersDirectory => DataManager.Current.UserDocumentsRootDirectory;

    /// <summary>
    /// Deletes a character file from disk. The caller is responsible for closing any open tab
    /// and releasing <see cref="CharacterContext"/> before calling this.
    /// </summary>
    public void DeleteCharacterFile(CharacterFile file)
    {
        try
        {
            if (File.Exists(file.FilePath))
                File.Delete(file.FilePath);

            SessionStore.Delete(file.FilePath);

            // Purge stale file-location registry entries (mirrors ShellWindowViewModel.DeleteCharacter).
            DataManager.Current.RemoveNonExistingCharacterFileLocations();

            // Clear preload state so IsPreloaded doesn't return a false positive if a new
            // character is later created at the same path.
            if (CharacterFileIdentity.RefersToSameFile(CurrentCharacterFile, file))
            {
                CurrentCharacter     = null;
                CurrentCharacterFile = null;
            }
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "CharacterService.DeleteCharacterFile");
            throw;
        }
    }

    /// <summary>
    /// Creates a new Level-1 character, saves it to disk, and returns the CharacterFile.
    /// Applies the DefaultHpMethod preference — registering the average HP option element
    /// if Average is selected. Callers should immediately open a tab and navigate to /build.
    /// </summary>
    public async Task<(CharacterFile? File, string? Error)> CreateNewCharacterAsync(
        string name, string playerName, HpMethod hpMethod,
        bool feats = true, bool multiclassing = true,
        bool customOrigin = true, bool customLanguage = true, bool customProficiency = true)
    {
        // Capture the active tab's state and hold the context lock while we stomp
        // the singleton with New() + Save.
        using var scope = await CharacterContext.EnterForLoadAsync();
        await EnsureElementsLoadedAsync();
        try
        {
            var character = await CharacterManager.Current.New(initializeFirstLevel: true);
            character.Name       = string.IsNullOrWhiteSpace(name) ? "New Character" : name.Trim();
            character.PlayerName = playerName.Trim();

            // Apply HP method preference
            if (hpMethod == HpMethod.Average)
            {
                var optionId = Builder.Data.Strings.InternalOptions.AllowAverageHitPoints;
                var element  = DataManager.Current.ElementsCollection.FirstOrDefault(e => e.Id == optionId);
                if (element != null)
                    CharacterManager.Current.RegisterElement(element);
            }

            // New() auto-registers options whose default="true"; unregister any the user has turned off.
            ApplyOption(Builder.Data.Strings.InternalOptions.AllowFeats,         feats);
            ApplyOption(Builder.Data.Strings.InternalOptions.AllowMulticlassing, multiclassing);
            ApplyOption("ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI",         customOrigin);
            ApplyOption("ID_WOTC_TCOE_OPTION_CUSTOMIZED_LANGUAGE",    customLanguage);
            ApplyOption("ID_WOTC_TCOE_OPTION_CUSTOMIZED_PROFICIENCY", customProficiency);

            string safeName = string.Concat(character.Name
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string path = DataManager.Current.GetCombinedCharacterFilePath(safeName);
            // Reserve the name before the atomic writer replaces it. Checking File.Exists and
            // falling back to a timestamp can overwrite a previous character from the same second.
            using (var reservation = CreateAvailableCharacterFile(path))
                path = reservation.Name;
            var file = new CharacterFile(path);
            file.RefreshKnownDiskStamp();
            try
            {
                file.Save(character);
            }
            catch
            {
                // Preserve anything another writer put at this path after the reservation.
                if (!file.HasExternalFileChanges() && new FileInfo(path).Length == 0)
                    DeleteFailedCharacterFile(path);
                throw;
            }

            CurrentCharacter     = character;
            CurrentCharacterFile = file;
            return (file, null);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "CharacterService.CreateNewCharacterAsync");
            return (null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a platform file picker so the user can select a .dnd5e character file from
    /// anywhere on the device, then copies it into the Aurora characters directory.
    /// Returns the resulting CharacterFile on success, (null, null) if the user cancelled,
    /// or (null, errorMessage) on failure.
    /// </summary>
    public async Task<(CharacterFile? File, string? Error)> ImportCharacterFromFileAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Select an Aurora character file (.dnd5e)",
            FileTypes   = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI,        new[] { ".dnd5e" } },
                { DevicePlatform.Android,      new[] { "*/*"    } }, // no registered MIME type
                { DevicePlatform.MacCatalyst,  new[] { "dnd5e" } },
                { DevicePlatform.macOS,        new[] { "dnd5e" } },
            }),
        };

        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(options);
        }
        catch (Exception ex)
        {
            return (null, $"File picker error: {ex.Message}");
        }

        if (picked == null) return (null, null); // cancelled

        if (!picked.FileName.EndsWith(".dnd5e", StringComparison.OrdinalIgnoreCase))
            return (null, $"'{picked.FileName}' is not a .dnd5e character file.");

        EnsureDirectoriesInitialized();

        string? createdPath = null;
        try
        {
            string destDir  = DataManager.Current.UserDocumentsRootDirectory;
            string destPath = Path.Combine(destDir, Path.GetFileName(picked.FileName));

            // OpenReadAsync works with both plain file paths and Android content:// URIs.
            using var src  = await picked.OpenReadAsync();
            using var dest = CreateAvailableCharacterFile(destPath);
            createdPath = dest.Name;
            await src.CopyToAsync(dest);

            return (new CharacterFile(createdPath), null);
        }
        catch (Exception ex)
        {
            if (createdPath != null)
                DeleteFailedCharacterFile(createdPath);
            DebugLogService.Instance.LogException(ex, "CharacterService.ImportCharacterFromFileAsync");
            return (null, $"Failed to import file: {ex.Message}");
        }
    }

    private static FileStream CreateAvailableCharacterFile(string preferredPath)
    {
        string directory = Path.GetDirectoryName(preferredPath)!;
        string stem = Path.GetFileNameWithoutExtension(preferredPath);
        string extension = Path.GetExtension(preferredPath);
        for (int number = 1; ; number++)
        {
            string candidate = number == 1
                ? preferredPath
                : Path.Combine(directory, $"{stem}_{number}{extension}");
            try
            {
                // CreateNew is the collision check and reservation together: concurrent imports
                // cannot truncate a name another import has already claimed.
                return new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Try the next suffix only for an occupied path; surface other I/O failures.
            }
        }
    }

    private static void DeleteFailedCharacterFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "CharacterService.DeleteFailedCharacterFile");
        }
    }

    /// <summary>
    /// Returns the app-specific external storage path on Android
    /// (/storage/emulated/0/Android/data/{package}/files), which is accessible from
    /// any file manager without root. Returns null on other platforms.
    /// </summary>
    public static string? GetAndroidExternalStoragePath()
    {
#if ANDROID
        string? externalBase = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
        return externalBase == null ? null : Path.Combine(externalBase, "5e Character Builder");
#else
        return null;
#endif
    }

    /// <summary>
    /// Opens a native folder-picker dialog and returns the chosen path, or null if
    /// the user cancelled.
    /// Windows: WinRT <c>FolderPicker</c> (native dialog).
    /// Mac Catalyst: <c>UIDocumentPickerViewController</c> via MAUI (maps to NSOpenPanel).
    /// Android: returns null — folder selection is surfaced via the "Use External Storage"
    /// shortcut instead, because SAF content URIs are not filesystem paths.
    /// </summary>
    public static async Task<string?> PickCharactersDirectoryAsync()
    {
#if WINDOWS
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        var window = Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
#elif MACCATALYST
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        return result.IsSuccessful ? result.Folder.Path : null;
#else
        return await Task.FromResult<string?>(null);
#endif
    }

    /// <summary>
    /// Applies a new character storage directory. Pass null or empty to reset to the
    /// default (Documents/5e Character Builder). Creates the directory if it does not
    /// exist. Returns an error string on failure, or null on success.
    /// Callers must close all character tabs before calling this.
    /// </summary>
    public string? ApplyCustomCharactersDirectory(string? path)
    {
        path = string.IsNullOrWhiteSpace(path)
            ? null
            : Path.TrimEndingDirectorySeparator(path);

        if (path != null && !Directory.Exists(path))
        {
            try { Directory.CreateDirectory(path); }
            catch (Exception ex) { return $"Could not create directory: {ex.Message}"; }
        }

        ApplicationContext.Current.Settings.DocumentsRootDirectory = path ?? "";
        ApplicationContext.Current.Settings.Save();
        DataManager.Current.InitializeDirectories();
        InvalidateFileListCache();
        // The selected folder also owns the content catalog. A character load while
        // Settings is refreshing it must not reuse the previous folder's preload.
        _elementsInitialized = false;
        _initDiagnostic = null;
        CurrentCharacter = null;
        CurrentCharacterFile = null;
        return null;
    }

    private static void ApplyOption(string optionId, bool enabled)
    {
        var cm = CharacterManager.Current;
        bool has = cm.ContainsOption(optionId);
        if (enabled && !has)
        {
            var el = DataManager.Current.ElementsCollection.FirstOrDefault(e => e.Id == optionId);
            if (el != null) cm.RegisterElement(el);
        }
        else if (!enabled && has)
        {
            var el = cm.GetElements().FirstOrDefault(e => e.Id == optionId);
            if (el != null) cm.UnregisterElement(el);
        }
    }

}
