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
