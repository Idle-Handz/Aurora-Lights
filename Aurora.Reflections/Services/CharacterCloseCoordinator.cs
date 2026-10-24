namespace Aurora.Reflections.Services;

/// <summary>Shared Save/Discard/Cancel workflow for a character tab and Windows app exit.</summary>
public sealed class CharacterCloseCoordinator(CharacterTabService tabs)
{
    private readonly object _gate = new();
    private readonly Dictionary<CharacterTab, Task<bool>> _pending = new();
    public bool HasPendingClosures { get { lock (_gate) return _pending.Count > 0; } }
    internal Task<bool>[] GetPendingClosures() { lock (_gate) return _pending.Values.ToArray(); }

    public Task<bool> TryCloseAsync(
        CharacterTab tab,
        Func<CharacterTab, Task<bool?>> confirmAsync,
        Func<CharacterTab, Task<string?>> saveAsync,
        Action<string> reportError,
        Func<CharacterTab, Task> releaseContextAsync)
    {
        lock (_gate)
        {
            // A native close joins an already-open tab-close dialog instead of opening a
            // second prompt whose Save/Discard continuation could race the first one.
            if (_pending.TryGetValue(tab, out var pending)) return pending;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[tab] = completion.Task;
            _ = CompleteAsync();
            return completion.Task;

            async Task CompleteAsync()
            {
                bool closed = false;
                Exception? failure = null;
                try { closed = await CloseCoreAsync(tab, confirmAsync, saveAsync, reportError, releaseContextAsync); }
                catch (Exception ex) { failure = ex; }
                finally { lock (_gate) _pending.Remove(tab); }
                if (failure is not null) completion.TrySetException(failure);
                else completion.TrySetResult(closed);
            }
        }
    }

    private async Task<bool> CloseCoreAsync(
        CharacterTab tab,
        Func<CharacterTab, Task<bool?>> confirmAsync,
        Func<CharacterTab, Task<string?>> saveAsync,
        Action<string> reportError,
        Func<CharacterTab, Task> releaseContextAsync)
    {
        try
        {
            while (tabs.Tabs.Contains(tab))
            {
                Task<string?>? pendingSave;
                long pendingEditVersion;
                lock (tab.SaveRequestGate)
                {
                    pendingSave = tab.InFlightSave;
                    pendingEditVersion = tab.InFlightSaveEditVersion;
                }
                if (pendingSave is not null)
                {
                    string? error = await pendingSave;
                    if (error is not null)
                    {
                        reportError("Save failed: " + error);
                        return false;
                    }
                    if (tab.EditVersion == pendingEditVersion) tabs.MarkClean(tab);
                    continue;
                }
                if (tab.IsSaving)
                {
                    // A cloud reload owns the save flag but has no full-save task to join.
                    reportError("Wait for the character's current operation to finish before closing.");
                    return false;
                }

                // Session patch saves use the file gate and independent pending versions.
                await tab.FileSaveSemaphore.WaitAsync();
                tab.FileSaveSemaphore.Release();
                if (tab.IsSaving) continue;

                long editVersion = tab.EditVersion;
                long sessionVersion = tab.SessionWrites.Version;
                bool? choice = tab.IsDirty ? await confirmAsync(tab) : false;
                if (choice is null) return false;
                if (choice == true)
                {
                    long saveVersion = tab.EditVersion;
                    string? error = await saveAsync(tab);
                    if (error is not null)
                    {
                        reportError("Save failed: " + error);
                        return false;
                    }
                    if (tab.EditVersion == saveVersion) tabs.MarkClean(tab);
                    // A newer draft or failed session patch must get its own close decision.
                    continue;
                }

                // A dialog/save may have yielded to later typing or a queued write. Never apply
                // an earlier discard decision to those edits, or close while a file write runs.
                if (tab.EditVersion != editVersion || tab.SessionWrites.Version != sessionVersion || tab.IsSaving)
                    continue;
                if (!tab.FileSaveSemaphore.Wait(0)) continue;
                try
                {
                    if (tab.IsSaving) continue;
                    tabs.CloseTab(tab);
                }
                finally { tab.FileSaveSemaphore.Release(); }

                // Structural edits use the engine lock instead of the full-save flag.
                await releaseContextAsync(tab);
            }
            return true;
        }
        catch (Exception ex)
        {
            reportError("Could not close the character: " + ex.Message);
            return false;
        }
    }
}
