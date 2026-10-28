namespace Aurora.Reflections.Services;

/// <summary>
/// Bridges native close requests to the layout's existing character-close workflow.
/// Repeated native close clicks share one request/dialog. The final close action runs on the
/// layout's dispatcher only after every character has accepted closing.
/// </summary>
public sealed class WindowCloseCoordinator(CharacterTabService tabs, CharacterCloseCoordinator characters)
{
    private readonly object _gate = new();
    private Task<bool>? _pending;
    public sealed record CloseHandler(
        Func<Func<Task>, Task> DispatchAsync,
        Func<CharacterTab, Task<bool>> CloseCharacterAsync,
        Func<bool> IsActive);

    public CloseHandler? Handler { get; set; }

    public Task<bool> RequestCloseAsync(Action close)
    {
        lock (_gate)
        {
            if (_pending is not null) return _pending;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = completion.Task;
            _ = CompleteAsync(close, completion);
            return completion.Task;
        }
    }

    private async Task CompleteAsync(Action close, TaskCompletionSource<bool> completion)
    {
        bool accepted = false;
        Exception? failure = null;
        try
        {
            if (Handler is { } handler)
            {
                await handler.DispatchAsync(async () =>
                {
                    if (!handler.IsActive()) return;
                    while (true)
                    {
                        while (tabs.Tabs.FirstOrDefault() is { } tab)
                        {
                            if (!await handler.CloseCharacterAsync(tab) || !handler.IsActive()) return;
                        }
                        // A tab-X operation can have removed its tab while still awaiting
                        // engine release. Join it even though it is no longer in the tab list.
                        Task<bool>[] pending = characters.GetPendingClosures();
                        if (pending.Length == 0) break;
                        if ((await Task.WhenAll(pending)).Any(result => !result) || !handler.IsActive()) return;
                    }
                    // No await between the final empty-tab check and native close. Include
                    // characters opened while a previous prompt/save was awaiting completion.
                    close();
                    accepted = true;
                });
            }
            else
            {
                // Before Blazor initializes, an empty app can still close. If its UI is
                // unavailable with character state present, fail closed to retain that state.
                accepted = tabs.Tabs.Count == 0 && !characters.HasPendingClosures;
                if (accepted) close();
            }
        }
        catch (Exception ex) { failure = ex; }
        finally { lock (_gate) _pending = null; }
        if (failure is not null) completion.TrySetException(failure);
        else completion.TrySetResult(accepted);
    }
}
