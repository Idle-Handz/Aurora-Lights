using System.Text.Json;
using System.Xml;
using Builder.Presentation.Utilities;

namespace Aurora.Reflections.Services;

public sealed record RecoveryDraftInfo(Guid Id, string Name, string OriginalPath, DateTimeOffset UpdatedUtc,
    bool IncompleteOperation = false, bool IsCorrupt = false);

/// <summary>Device-local checkpoints, independent of autosave and the original document.
/// Each open tab owns a journal: opening the saved original never deletes an older crash recovery.</summary>
public sealed class CharacterDraftRecovery
{
    private sealed class Journal
    {
        public int Version { get; set; } = 1;
        public RecoveryDraftInfo Info { get; set; } = null!;
        public string CharacterXml { get; set; } = "";
        public SessionState Session { get; set; } = new();
    }

    private sealed class Tracked(CharacterTab tab)
    {
        public CharacterTab Tab { get; } = tab;
        public object Gate { get; } = new();
        public long Revision;
        public bool Retired, Running;
        public string? LastXml;
        public Action Changed = null!;
    }

    private readonly string _directory;
    private readonly bool _enabled;
    private readonly Func<CharacterTab, Task<XmlDocument>> _capture;
    private readonly object _gate = new(), _ioGate = new();
    private readonly Dictionary<Guid, Tracked> _tracked = [];
    private readonly HashSet<Task> _workers = [];
    private readonly object _workersGate = new();
    private readonly HashSet<Guid> _failed = [];
    private readonly object _errorGate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    public string? LastError { get; private set; }
    public event Action? Changed;

    public CharacterDraftRecovery(string directory, bool enabled = true,
        Func<CharacterTab, Task<XmlDocument>>? capture = null)
    {
        _directory = Path.GetFullPath(directory);
        _enabled = enabled;
        _capture = capture ?? BuildService.CaptureCharacterExportAsync;
    }

    public void Track(CharacterTab tab)
    {
        if (!_enabled) return;
        lock (_gate)
        {
            if (_tracked.ContainsKey(tab.Id)) return;
            var tracked = new Tracked(tab);
            tracked.Changed = () => Queue(tracked);
            _tracked.Add(tab.Id, tracked);
            tab.DraftStateChanged += tracked.Changed;
            Queue(tracked);
        }
    }

    public void Forget(CharacterTab tab)
    {
        Tracked? tracked;
        lock (_gate)
        {
            if (!_tracked.Remove(tab.Id, out tracked)) return;
            tab.DraftStateChanged -= tracked.Changed;
        }
        lock (tracked.Gate)
        {
            tracked.Retired = true;
            tracked.Revision++;
            try { Delete(tab.Id); }
            catch (Exception ex) { Report(tab.Id, ex); }
        }
    }

    private void Queue(Tracked tracked)
    {
        lock (tracked.Gate)
        {
            if (tracked.Retired) return;
            tracked.Revision++;
            if (tracked.Running) return;
            tracked.Running = true;
            var worker = CheckpointAsync(tracked);
            lock (_workersGate) _workers.Add(worker);
            _ = worker.ContinueWith(completed =>
            {
                lock (_workersGate) _workers.Remove(completed);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private async Task CheckpointAsync(Tracked tracked)
    {
        // Property setters often run inside a mutation's CharacterContext. Let that operation
        // finish before entering it again, and coalesce edits accepted while capture awaits a save.
        await Task.Yield();
        await Task.Delay(150);
        try
        {
            while (true)
            {
                long revision;
                lock (tracked.Gate)
                {
                    if (tracked.Retired) { tracked.Running = false; return; }
                    revision = tracked.Revision;
                }
                var tab = tracked.Tab;
                XmlDocument? document = null;
                if (CanCapture(tab) && (tab.IsDirty || tab.IsSaving)) document = await _capture(tab);
                lock (tracked.Gate)
                {
                    if (tracked.Retired) { tracked.Running = false; return; }
                    if (revision != tracked.Revision) continue;
                    if (document is not null)
                    {
                        tracked.LastXml = document.OuterXml;
                        Write(tab, document, incomplete: tab.IsSaving);
                    }
                    else if (CanCapture(tab) && !tab.IsDirty && !tab.IsSaving) Delete(tab.Id);
                    tracked.Running = false;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            lock (tracked.Gate) tracked.Running = false;
            Report(tracked.Tab.Id, ex);
        }
    }

    private static bool CanCapture(CharacterTab tab) =>
        !tab.IsLoading && tab.Character is not null && tab.Snapshot is not null && !tab.CloudReloadFailed;

    /// <summary>Completes local disk writes before Android's OnStop returns. Never waits for the
    /// engine/file lock, a cloud upload, or a continuation that needs the stopped UI thread.</summary>
    public void CheckpointOnBackground()
    {
        Tracked[] all;
        lock (_gate) all = _tracked.Values.ToArray();
        foreach (var tracked in all)
        {
            lock (tracked.Gate)
            {
                var tab = tracked.Tab;
                if (tracked.Retired || !CanCapture(tab)) continue;
                tracked.Revision++; // A foreground capture already in flight must not overwrite this checkpoint.
                try
                {
                    if (!tab.IsDirty && !tab.IsSaving) { Delete(tab.Id); continue; }
                    var document = BuildService.TryCaptureRecoveryDocument(tab);
                    bool fallback = document is null;
                    if (document is null)
                    {
                        document = new XmlDocument();
                        if (!ReferenceEquals(CharacterContext.ActiveTab, tab) && tab.StateXml is { Length: > 0 } state)
                        {
                            using var stream = new MemoryStream(state);
                            document.Load(stream);
                        }
                        else if (tracked.LastXml is not null) document.LoadXml(tracked.LastXml);
                        else document = CharacterFileIo.LoadXmlDocument(tab.File.FilePath);
                    }
                    if (!CharacterFileSaveExtensions.ApplyTextEdits(document, tab.Snapshot!))
                        throw new InvalidDataException("The recovery character has no build data.");
                    Write(tab, document, incomplete: fallback || tab.IsSaving);
                    tracked.LastXml = document.OuterXml;
                }
                catch (Exception ex) { Report(tab.Id, ex); }
            }
        }
    }

    public async Task FlushAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_workersGate) pending = _workers.ToArray();
            if (pending.Length == 0) return;
            await Task.WhenAll(pending);
        }
    }

    private void Write(CharacterTab tab, XmlDocument document, bool incomplete)
    {
        var journal = new Journal
        {
            Info = new(tab.Id, tab.Snapshot!.Name, tab.File.FilePath, DateTimeOffset.UtcNow, incomplete),
            CharacterXml = document.OuterXml,
            Session = tab.Session
        };
        lock (_ioGate)
        {
            Directory.CreateDirectory(_directory);
            CharacterFileIo.SaveTextFileAtomic(JournalPath(tab.Id), JsonSerializer.Serialize(journal, Options));
        }
        ClearError(tab.Id);
    }

    public IReadOnlyList<RecoveryDraftInfo> List()
    {
        HashSet<Guid> active;
        lock (_gate) active = _tracked.Keys.ToHashSet();
        lock (_ioGate)
        {
            if (!Directory.Exists(_directory)) return [];
            var drafts = new List<RecoveryDraftInfo>();
            foreach (var file in Directory.EnumerateFiles(_directory, "*.draft.json"))
            {
                if (!Guid.TryParse(Path.GetFileName(file).Replace(".draft.json", ""), out var id)) continue;
                if (active.Contains(id)) continue;
                try { drafts.Add(Read(id).Info); }
                catch (Exception ex)
                {
                    DebugLogService.Instance.LogException(ex, "CharacterDraftRecovery.List");
                    drafts.Add(new(id, "Unreadable recovery draft", "", File.GetLastWriteTimeUtc(file), IsCorrupt: true));
                }
            }
            return drafts.OrderByDescending(draft => draft.UpdatedUtc).ToList();
        }
    }

    /// <summary>Materializes a new local file; never writes to the journal's original path or Drive.</summary>
    public string OpenCopy(Guid id, string charactersDirectory)
    {
        lock (_ioGate)
        {
            var journal = Read(id);
            Directory.CreateDirectory(charactersDirectory);
            var name = string.Concat(journal.Info.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if (string.IsNullOrWhiteSpace(name)) name = "Character";
            name = name[..Math.Min(name.Length, 80)];
            string destination = Path.Combine(charactersDirectory, $"{name} (Recovered {Guid.NewGuid():N}).dnd5e");
            var document = new XmlDocument();
            document.LoadXml(journal.CharacterXml);
            using (File.Open(destination, FileMode.CreateNew)) { }
            try
            {
                CharacterFileIo.SaveXmlDocumentAtomic(destination, document);
                SessionStore.SaveRequired(destination, journal.Session);
                return destination;
            }
            catch
            {
                File.Delete(destination);
                SessionStore.Delete(destination);
                throw;
            }
        }
    }

    public void Delete(Guid id)
    {
        lock (_ioGate)
        {
            try { File.Delete(JournalPath(id)); }
            catch (DirectoryNotFoundException) { } // A fresh installation has no journals yet.
        }
        ClearError(id);
        Changed?.Invoke();
    }

    private Journal Read(Guid id)
    {
        var journal = JsonSerializer.Deserialize<Journal>(CharacterFileIo.LoadTextFile(JournalPath(id)), Options);
        if (journal is null || journal.Version != 1 || journal.Info is null || journal.Info.Id != id
            || string.IsNullOrWhiteSpace(journal.CharacterXml))
            throw new InvalidDataException("The recovery draft is unreadable or from an unsupported version.");
        return journal;
    }

    private string JournalPath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".draft.json");

    private void ClearError(Guid id)
    {
        bool changed;
        lock (_errorGate)
        {
            changed = _failed.Remove(id);
            if (_failed.Count == 0) LastError = null;
        }
        if (changed) Changed?.Invoke();
    }

    private void Report(Guid id, Exception ex)
    {
        DebugLogService.Instance.LogException(ex, "CharacterDraftRecovery");
        lock (_errorGate)
        {
            _failed.Add(id);
            LastError = "Draft recovery could not be updated. Save or export your character before leaving the app.";
        }
        Changed?.Invoke();
    }
}
