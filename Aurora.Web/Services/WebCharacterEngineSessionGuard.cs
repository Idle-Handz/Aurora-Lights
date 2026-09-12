namespace Aurora.Web.Services;

/// <summary>
/// Prevents separate Blazor circuits from sharing the legacy process-global character engine.
/// This is a release-safety boundary, not the final multi-user architecture: one circuit may
/// own an active character at a time until the underlying runtime is made session-local.
/// </summary>
public sealed class WebCharacterEngineSessionGuard
{
    private readonly object _syncRoot = new();
    private Guid? _ownerSessionId;

    /// <summary>
    /// Acquires ownership for a web session. Returns true for a new acquisition and false when
    /// the same session already owns the engine.
    /// </summary>
    public bool Acquire(Guid sessionId)
    {
        lock (_syncRoot)
        {
            if (_ownerSessionId == sessionId)
                return false;

            if (_ownerSessionId.HasValue)
            {
                throw new InvalidOperationException(
                    "Another browser session currently has a character open. Close that session before opening a character here.");
            }

            _ownerSessionId = sessionId;
            return true;
        }
    }

    public void Release(Guid sessionId)
    {
        lock (_syncRoot)
        {
            if (_ownerSessionId == sessionId)
                _ownerSessionId = null;
        }
    }
}
