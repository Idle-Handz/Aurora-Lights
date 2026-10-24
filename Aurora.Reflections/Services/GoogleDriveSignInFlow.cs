namespace Aurora.Reflections.Services;

/// <summary>Interactive authorization starts only after the notice is accepted.</summary>
public static class GoogleDriveSignInFlow
{
    public static async Task<bool> RunAsync(Func<CancellationToken, Task<bool>> confirm,
        Func<CancellationToken, Task> authorize, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(authorize);
        ct.ThrowIfCancellationRequested();
        if (!await confirm(ct).WaitAsync(ct)) return false;
        ct.ThrowIfCancellationRequested();
        await authorize(ct);
        return true;
    }
}
