using System.Diagnostics;

namespace Aurora.App.Services;

/// <summary>Optional phase trace for device diagnostics; inactive unless a host attaches a sink.</summary>
internal static class ContentLoadTrace
{
    private static Action<string>? _sink;
    private static int _nextId;

    internal static Action<string>? Sink
    {
        get => Volatile.Read(ref _sink);
        set => Volatile.Write(ref _sink, value);
    }

    internal static IDisposable? Begin(string phase)
    {
        var sink = Sink;
        return sink is null ? null : new Scope(sink, phase);
    }

    internal static void Mark(string phase)
    {
        var sink = Sink;
        if (sink is not null) Emit(sink, phase);
    }

    private static void Emit(Action<string> sink, string message)
    {
        // A diagnostic sink must never affect content loading or its error handling.
        try { sink($"{message} thread={Environment.CurrentManagedThreadId} context={SynchronizationContext.Current?.GetType().Name ?? "none"}"); }
        catch { }
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action<string> _sink;
        private readonly string _phase;
        private readonly long _start = Stopwatch.GetTimestamp();
        private readonly int _id = Interlocked.Increment(ref _nextId);
        private int _disposed;

        internal Scope(Action<string> sink, string phase)
        {
            _sink = sink;
            _phase = phase;
            Emit(sink, $"begin id={_id} phase={phase}");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Emit(_sink, $"end id={_id} phase={_phase} elapsedMs={Stopwatch.GetElapsedTime(_start).TotalMilliseconds:F1}");
        }
    }
}
