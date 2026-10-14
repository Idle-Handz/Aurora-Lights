using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

namespace Aurora.App.Services;

public enum CharacterFileWriteStatus
{
    Saved,
    ExternalChange,
    Failed
}

public sealed record CharacterFileWriteResult(
    CharacterFileWriteStatus Status,
    string Message,
    Exception? Exception = null)
{
    public bool Succeeded => Status == CharacterFileWriteStatus.Saved;
    public bool IsExternalChange => Status == CharacterFileWriteStatus.ExternalChange;

    public static CharacterFileWriteResult Saved(string operation) =>
        new(CharacterFileWriteStatus.Saved, $"{operation} saved.");

    public static CharacterFileWriteResult ExternalChange(Exception ex) =>
        new(CharacterFileWriteStatus.ExternalChange, ex.Message, ex);

    public static CharacterFileWriteResult Failed(string operation) =>
        new(CharacterFileWriteStatus.Failed, $"{operation} could not be written to the character file.");

    public static CharacterFileWriteResult Failed(string operation, Exception ex) =>
        new(CharacterFileWriteStatus.Failed, $"{operation} could not be written to the character file: {ex.Message}", ex);

    public void ThrowIfFailed()
    {
        if (Succeeded)
            return;

        if (Exception is not null)
            throw Exception;

        throw new InvalidOperationException(Message);
    }
}

/// <summary>
/// Serializes read-modify-write operations against a character XML file and rejects writes
/// when the file changed on disk after the character was loaded.
/// </summary>
public static class CharacterFileWriteCoordinator
{
    /// <summary>
    /// Completes the sidecar and any changed session-backed XML fields as one awaited operation.
    /// Pending state belongs to the tab, so navigation cannot hide a failure from close handling.
    /// </summary>
    public static async Task<CharacterFileWriteResult> SaveSessionAsync(
        CharacterTab tab, SessionState session, CharacterSnapshot snapshot)
    {
        long version = tab.SessionWrites.BeginChange();
        var edits = SessionCharacterEdits.From(snapshot);
        await tab.FileSaveSemaphore.WaitAsync();
        try
        {
            // A full save may have completed this edit while this request waited for the file.
            if (tab.SessionWrites.IsAlreadySaved(version))
                return CharacterFileWriteResult.Saved("Session");
            tab.SessionWrites.InitializeFromFile(tab.File);
            if (!SessionStore.Save(tab.File.FilePath, session))
                return CharacterFileWriteResult.Failed("Session state");

            bool currencyChanged = tab.SessionWrites.NeedsCurrencyPatch(edits);
            bool notesChanged = tab.SessionWrites.NeedsNotesPatch(edits);
            if (currencyChanged || notesChanged)
            {
                var patch = edits.ToSnapshot();
                var result = await Task.Run(() => WriteWithoutGate(tab.File, "Session currency and notes", () =>
                    (!currencyChanged || tab.File.SaveCurrency(patch)) && (!notesChanged || tab.File.SaveNotes(patch))));
                if (!result.Succeeded) return result;
            }
            tab.SessionWrites.Saved(version, edits);
            return CharacterFileWriteResult.Saved("Session");
        }
        catch (Exception ex)
        {
            return CharacterFileWriteResult.Failed("Session", ex);
        }
        finally
        {
            tab.CloudSession?.InvalidatePendingState();
            tab.FileSaveSemaphore.Release();
        }
    }

    /// <summary>
    /// Writes only when <paramref name="targetFile"/> is the same physical file owned by the
    /// active tab. This is the final guard against a stale service-level file reference sending
    /// one tab's in-memory character to another tab's path.
    /// </summary>
    public static CharacterFileWriteResult WriteOwned(
        SemaphoreSlim gate,
        CharacterFile ownerFile,
        CharacterFile targetFile,
        string operation,
        Func<bool> write)
    {
        try
        {
            CharacterFileIdentity.EnsureSameFile(ownerFile, targetFile);
        }
        catch (Exception ex)
        {
            return CharacterFileWriteResult.Failed(operation, ex);
        }

        return Write(gate, targetFile, operation, write);
    }

    public static CharacterFileWriteResult Write(
        SemaphoreSlim gate,
        CharacterFile file,
        string operation,
        Func<bool> write)
    {
        gate.Wait();
        try
        {
            return WriteWithoutGate(file, operation, write);
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<CharacterFileWriteResult> WriteAsync(
        SemaphoreSlim gate,
        CharacterFile file,
        string operation,
        Func<bool> write,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return WriteWithoutGate(file, operation, write);
        }
        finally
        {
            gate.Release();
        }
    }

    private static CharacterFileWriteResult WriteWithoutGate(
        CharacterFile file,
        string operation,
        Func<bool> write)
    {
        try
        {
            file.EnsureNoExternalFileChanges();
            return write()
                ? CharacterFileWriteResult.Saved(operation)
                : CharacterFileWriteResult.Failed(operation);
        }
        catch (CharacterFileExternalChangeException ex)
        {
            return CharacterFileWriteResult.ExternalChange(ex);
        }
        catch (Exception ex)
        {
            return CharacterFileWriteResult.Failed(operation, ex);
        }
    }
}
