namespace Aurora.Logic;

/// <summary>
/// Semaphore-protected guard for a single "active" item. Ensures that only one item is
/// materialised in a process-wide singleton at a time, with safe swap semantics:
/// active is nulled before the swap callback runs, so an exception during the swap leaves
/// active = null rather than pointing to a tab whose state has been partially torn down.
/// </summary>
public sealed class SingletonGuard<T> where T : class
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private T? _active;

    public T? Active => _active;

    /// <summary>
    /// Maximum time to wait for the guard before assuming a deadlock — almost always a *reentrant*
    /// acquire: a guarded operation that called another guarded operation while already holding the
    /// lock. The guard is a non-reentrant semaphore, so that would otherwise hang the UI forever; this
    /// converts it into a diagnosable exception. Generous enough that a legitimately slow operation
    /// (e.g. loading a large character) won't trip it. Settable so tests can use a short timeout.
    /// </summary>
    public static TimeSpan AcquireTimeout { get; set; } = TimeSpan.FromSeconds(30);

    private async Task AcquireAsync()
    {
        if (!await _lock.WaitAsync(AcquireTimeout))
            throw new InvalidOperationException(
                $"SingletonGuard<{typeof(T).Name}> could not be acquired within " +
                $"{AcquireTimeout.TotalSeconds:0}s. This usually means a guarded operation re-entered " +
                "the guard (it called another guarded operation while already holding it). A call chain " +
                "must hold the context only once — split the inner work out of the guarded scope.");
    }

    /// <summary>Records item as active without acquiring the lock. Safe to call immediately
    /// after an external load on the thread that performed the load.</summary>
    public void Claim(T item) => _active = item;

    /// <summary>Clears active if it is the same object as <paramref name="item"/>.</summary>
    public async Task ReleaseAsync(T item)
    {
        await AcquireAsync();
        try { if (ReferenceEquals(_active, item)) _active = null; }
        finally { _lock.Release(); }
    }

    /// <summary>Clears active unconditionally.</summary>
    public async Task InvalidateAsync()
    {
        await AcquireAsync();
        try { _active = null; }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Acquires the lock, optionally captures the current active item via
    /// <paramref name="beforeClear"/>, then clears active and releases the lock.
    /// </summary>
    public async Task CaptureAndInvalidateAsync(Action<T?>? beforeClear = null)
    {
        await AcquireAsync();
        try
        {
            beforeClear?.Invoke(_active);
            _active = null;
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Acquires the lock. If <paramref name="item"/> is not already active:
    /// <list type="number">
    ///   <item>Captures the outgoing reference before clearing.</item>
    ///   <item>Clears active immediately (so a throw leaves active = null).</item>
    ///   <item>Calls <paramref name="onSwap"/>(outgoing, item).</item>
    ///   <item>Sets active = item.</item>
    /// </list>
    /// Returns a scope whose Dispose releases the lock.
    /// </summary>
    public async Task<IDisposable> EnterAsync(T item, Func<T?, T, Task> onSwap)
    {
        await AcquireAsync();
        try
        {
            if (!ReferenceEquals(_active, item))
            {
                var outgoing = _active;
                _active = null;
                await onSwap(outgoing, item);
                _active = item;
            }
            return new LockScope(_lock);
        }
        catch
        {
            _lock.Release();
            throw;
        }
    }

    /// <summary>
    /// Acquires the lock, optionally captures the current active item, clears active, and
    /// returns a scope that holds the lock until disposed. Use for external load flows that
    /// must hold exclusive access for the duration of the load. Call <see cref="Claim"/>
    /// after the load completes to register the new active item.
    /// </summary>
    public async Task<IDisposable> EnterForLoadAsync(Action<T?>? beforeClear = null)
    {
        await AcquireAsync();
        try
        {
            beforeClear?.Invoke(_active);
            _active = null;
            return new LockScope(_lock);
        }
        catch
        {
            _lock.Release();
            throw;
        }
    }

    private sealed class LockScope : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        internal LockScope(SemaphoreSlim sem) => _sem = sem;
        public void Dispose() => _sem.Release();
    }
}
