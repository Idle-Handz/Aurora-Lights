#if AURORA_ANDROID_DIAGNOSTICS
using Android.OS;
using Aurora.App.Services;
using Environment = System.Environment;

namespace Aurora.App;

/// <summary>
/// Diagnostic builds only. A UI pulse and dedicated managed observer distinguish UI-only
/// stalls from delays affecting both threads. Mono's GC log supplies actual GC timings;
/// collection counts and observer gaps alone do not prove that GC caused a pause.
/// </summary>
internal sealed class AndroidResponsivenessProbe : IDisposable
{
    private const string Tag = "AuroraProbe";
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _pulse;
    private readonly ManualResetEvent _stop = new(false);
    private readonly Action<string> _traceSink;
    private readonly Thread _observer;
    private readonly string _session = Guid.NewGuid().ToString("N")[..8];
    private readonly long _deadline = SystemClock.ElapsedRealtime() + 15 * 60 * 1000;
    private long _lastUiPulse;
    private int _foreground;
    private int _disposed;
    private int _finished;

    internal AndroidResponsivenessProbe()
    {
        _pulse = new Java.Lang.Runnable(Pulse);
        _traceSink = Write;
        ContentLoadTrace.Sink = _traceSink;
        _observer = new Thread(Observe) { IsBackground = true, Name = "AuroraProbe" };
        _observer.Start();
        Write($"session-start pid={Android.OS.Process.MyPid()} runtime={Environment.Version} gc={Environment.GetEnvironmentVariable("MONO_GC_PARAMS")}");
    }

    internal void SetForeground(bool foreground)
    {
        Interlocked.Exchange(ref _lastUiPulse, SystemClock.ElapsedRealtime());
        Volatile.Write(ref _foreground, foreground ? 1 : 0);
        _handler.RemoveCallbacks(_pulse);
        if (foreground && Volatile.Read(ref _finished) == 0) _handler.Post(_pulse);
        Write(foreground ? "foreground" : "background");
    }

    private void Pulse()
    {
        Interlocked.Exchange(ref _lastUiPulse, SystemClock.ElapsedRealtime());
        if (Volatile.Read(ref _foreground) == 1 && Volatile.Read(ref _disposed) == 0
            && Volatile.Read(ref _finished) == 0)
            _handler.PostDelayed(_pulse, 250);
    }

    private void Observe()
    {
        long previousWake = SystemClock.ElapsedRealtime();
        long previousReport = 0;
        bool wasForeground = false;
        try
        {
            while (!_stop.WaitOne(500))
            {
                long now = SystemClock.ElapsedRealtime();
                if (now >= _deadline) break;
                bool foreground = Volatile.Read(ref _foreground) == 1;
                long wakeGap = now - previousWake;
                previousWake = now;
                long uiAge = now - Interlocked.Read(ref _lastUiPulse);
                bool delayed = foreground && wasForeground && (uiAge >= 1500 || wakeGap >= 1500);
                if (delayed || (foreground && now - previousReport >= 5000))
                {
                    Write($"heartbeat uiAgeMs={uiAge} observerGapMs={wakeGap} delayed={delayed} " +
                        $"gc0={GC.CollectionCount(0)} gcMajor={GC.CollectionCount(GC.MaxGeneration)} " +
                        $"managedBytes={GC.GetTotalMemory(false)} allocatedBytes={GC.GetTotalAllocatedBytes(false)}");
                    previousReport = now;
                }
                wasForeground = foreground;
            }
        }
        catch (Exception ex) { Write($"probe-error {ex.GetType().Name}"); }
        finally
        {
            Volatile.Write(ref _finished, 1);
            if (ContentLoadTrace.Sink == _traceSink) ContentLoadTrace.Sink = null;
            Write("session-monitor-ended");
            _stop.Dispose();
        }
    }

    private void Write(string message)
    {
        // No file I/O or UI Changed events on this path. The host captures filtered logcat.
        Android.Util.Log.Info(Tag, $"session={_session} uptimeMs={SystemClock.ElapsedRealtime()} mainThread={Looper.MainLooper!.IsCurrentThread} {message}");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Volatile.Write(ref _foreground, 0);
        _handler.RemoveCallbacks(_pulse);
        if (ContentLoadTrace.Sink == _traceSink) ContentLoadTrace.Sink = null;
        try { _stop.Set(); }
        catch (ObjectDisposedException) { /* The bounded observer already finished. */ }
        // Do not join the observer on the UI thread.
        _pulse.Dispose();
        _handler.Dispose();
    }
}
#endif
