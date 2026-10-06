using MudBlazor;

namespace Aurora.App.Services;

/// <summary>
/// One write of a character file patch, with the words a page uses when it goes wrong.
/// </summary>
/// <param name="Operation">A label for the write, used in the coordinator's own messages.</param>
/// <param name="Write">The patch to apply; returns false when it could not be written.</param>
/// <param name="FailureMessage">
/// Shown when the write failed but the change is still in memory, followed by the reason.
/// </param>
/// <param name="ExternalChangeMessage">Shown when the file changed on disk and the character was reloaded.</param>
/// <param name="ReloadFailedAction">
/// Finishes "Close and reopen the character before ..." when the reload itself fails, for example
/// "making more equipment changes".
/// </param>
/// <param name="LogContext">Where a swallowed exception is attributed in the log.</param>
public sealed record CharacterFileWrite(
    string Operation,
    Func<bool> Write,
    string FailureMessage,
    string ExternalChangeMessage,
    string ReloadFailedAction,
    string LogContext);

/// <summary>
/// Writes a patch to a tab's character file and handles every way that can go wrong the same way on
/// every page: a failed write keeps the change in memory and says so; a file that changed on disk
/// since it was loaded is never overwritten, the character is reloaded instead.
/// </summary>
/// <remarks>
/// Reloading is only valid while the caller holds the tab's <see cref="CharacterContext"/> scope, so
/// call this from inside one. The page supplies how it reports (<c>notify</c>, <c>logException</c>)
/// and, per call, what to do once a reload has replaced the character. Equipment, Shop, Session and
/// Manage each carried their own copy of this before.
/// </remarks>
public sealed class CharacterFileWriteHandler
{
    private readonly Action<string, Severity, int> _notify;
    private readonly Action<Exception, string> _logException;
    private readonly Func<CharacterTab, Task> _reload;

    /// <param name="notify">Shows a message: text, severity, and how long to keep it up in milliseconds.</param>
    /// <param name="logException">Records an exception against a context label.</param>
    /// <param name="reload">
    /// Replaces the tab's character from its file. Defaults to the app's reload and re-snapshot; tests
    /// pass a stand-in.
    /// </param>
    public CharacterFileWriteHandler(
        Action<string, Severity, int> notify,
        Action<Exception, string> logException,
        Func<CharacterTab, Task>? reload = null)
    {
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _logException = logException ?? throw new ArgumentNullException(nameof(logException));
        _reload = reload ?? ReloadFromDiskAsync;
    }

    /// <summary>
    /// Applies <paramref name="write"/>. Returns true when the caller can carry on: the write
    /// succeeded, or it failed but the change stays in memory and the user has been told. Returns
    /// false when the file had changed on disk and the character was reloaded instead; the caller
    /// should then stop and show the reloaded character, which <paramref name="afterReload"/> does.
    /// </summary>
    public async Task<bool> WriteAsync(
        CharacterTab tab,
        CharacterFileWrite write,
        Func<Task>? afterReload = null)
    {
        CharacterFileWriteResult result = await CharacterFileWriteCoordinator.WriteAsync(
            tab.FileSaveSemaphore,
            tab.File,
            write.Operation,
            write.Write);

        if (result.Succeeded)
            return true;

        if (result.IsExternalChange)
        {
            _notify(write.ExternalChangeMessage, Severity.Warning, 7000);
            await ReloadAsync(tab, write, afterReload);
            return false;
        }

        if (result.Exception is not null)
            _logException(result.Exception, write.LogContext);

        _notify($"{write.FailureMessage} {result.Message}", Severity.Warning, 7000);
        return true;
    }

    private async Task ReloadAsync(CharacterTab tab, CharacterFileWrite write, Func<Task>? afterReload)
    {
        try
        {
            await _reload(tab);
            if (afterReload is not null)
                await afterReload();
        }
        catch (Exception ex)
        {
            _logException(ex, write.LogContext + ".Reload");
            _notify(
                "The character file changed on disk, but Reflections could not reload it automatically. "
                + $"Close and reopen the character before {write.ReloadFailedAction}.",
                Severity.Error,
                9000);
        }
    }

    private static async Task ReloadFromDiskAsync(CharacterTab tab)
    {
        await CharacterContext.ReloadFromDiskAsync(tab);
        BuildService.ResnapTab(tab);
    }
}
