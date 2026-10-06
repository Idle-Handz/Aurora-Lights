using System.Collections.Concurrent;
using System.Diagnostics;
using Aurora.App.Services;
using Builder.Presentation.Services.Data;

/// <summary>
/// Runs the real content loader the way a Blazor Hybrid page does: started on a single UI thread
/// whose synchronization context every un-ConfigureAwait(false)'d continuation resumes on.
///
/// Android raises "not responding" when the main thread is held for about five seconds. The loader's
/// heavy stages are wrapped in Task.Run, but the code between them resumes on the UI thread. This
/// records how long each resumed segment holds that thread, because the longest single block is what
/// an ANR watchdog sees, not the total.
/// </summary>
internal static class UiThreadProbe
{
    public static (bool Success, object Report) Run()
    {
        var pump = new Pump();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(pump);
        var elements = DataManager.Current.ElementsCollection;
        DbLoadResult? load = null;
        Exception? failure = null;
        var done = new ManualResetEventSlim();
        var wall = Stopwatch.StartNew();
        pump.Post(async _ =>
        {
            try { load = await DbElementLoader.TryLoadAsync(elements); }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        }, null);
        pump.RunUntil(done);
        wall.Stop();
        SynchronizationContext.SetSynchronizationContext(previous);

        var segments = pump.Segments;
        bool success = failure is null && load?.Success == true;
        return (success, new
        {
            success, failure = failure?.Message ?? load?.FailureReason,
            wallSeconds = wall.Elapsed.TotalSeconds,
            uiThreadSegments = segments.Count,
            uiThreadBusySeconds = segments.Sum(s => s.Milliseconds) / 1000.0,
            longestBlockMilliseconds = segments.Count == 0 ? 0 : segments.Max(s => s.Milliseconds),
            segmentsInOrder = segments.Select(s => new { s.Index, ms = Math.Round(s.Milliseconds, 1) }).ToArray(),
        });
    }

    private sealed class Pump : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
        public List<(int Index, double Milliseconds)> Segments { get; } = [];

        public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunUntil(ManualResetEventSlim done)
        {
            int index = 0;
            while (!done.IsSet || queue.Count > 0)
            {
                if (!queue.TryTake(out var item, 25)) continue;
                var timer = Stopwatch.StartNew();
                item.Callback(item.State);
                timer.Stop();
                Segments.Add((index++, timer.Elapsed.TotalMilliseconds));
            }
        }
    }
}
