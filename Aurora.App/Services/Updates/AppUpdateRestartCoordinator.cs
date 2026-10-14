namespace Aurora.App.Services.Updates;

/// <summary>
/// Keeps character work out of an immediate app restart, including work started during download.
/// The final check and restart run in one synchronous UI callback so another editor event cannot
/// arrive between them. Open tabs are blocked even when clean: structural edits can be in flight
/// without setting the tab's full-save flag.
/// </summary>
public sealed class AppUpdateRestartCoordinator(CharacterTabService tabs, CharacterCloseCoordinator characters)
{
    public const string CloseCharactersMessage =
        "Close all character tabs before installing and restarting. Save or discard unsaved changes when closing each tab, and wait for active saves to finish.";

    public async Task<string?> RunAsync(
        Func<Task<string?>> downloadAsync,
        Action restart,
        Func<Action, Task> dispatchAsync,
        Func<bool> isCurrentView)
    {
        string? error = null;
        string? Check() => !isCurrentView()
            ? "Restart canceled because you left Settings. Return to Settings to install the update when ready."
            : tabs.Tabs.Count > 0 || characters.HasPendingClosures ? CloseCharactersMessage : null;

        await dispatchAsync(() => error = Check()).ConfigureAwait(false);
        if (error is not null) return error;

        error = await downloadAsync().ConfigureAwait(false);
        if (error is not null) return error;

        await dispatchAsync(() =>
        {
            error = Check();
            if (error is null)
                restart();
        }).ConfigureAwait(false);
        return error;
    }
}
