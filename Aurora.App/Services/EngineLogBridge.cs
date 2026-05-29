using Builder.Core.Logging;
using Builder.Presentation.Logging;

namespace Aurora.App.Services;

/// <summary>
/// Bridges the engine's <see cref="Builder.Core.Logging.Logger"/> to the in-app
/// <see cref="DebugLogService"/> Console. Without this, engine diagnostics — including the warnings
/// the engine emits when it silently abandons an operation (e.g. "NewMulticlass !Status.CanLevelUp")
/// and exceptions it swallows internally — go only to <c>Debug.WriteLine</c> and never reach the
/// in-app Console. Registered once in <c>MauiProgram</c> via <c>Logger.RegisterLogger</c>.
///
/// Debug/Info levels are intentionally dropped: the engine emits them very frequently and they would
/// flood the Console. Warnings and exceptions — the things you actually want when something "silently"
/// fails — are surfaced.
/// </summary>
internal sealed class EngineLogBridge : ILogger
{
    public void Debug(string message, params object[] args) { /* too chatty for the Console */ }

    public void Info(string message, params object[] args) { /* dropped to keep the Console focused */ }

    public void Warning(string message, params object[] args)
    {
        string formatted = Format(message, args);
        if (EngineLogNoiseFilter.ShouldSuppressWarning(formatted))
            return;

        DebugLogService.Instance.Warn("[engine] " + formatted);
    }

    public void Exception(Exception ex)
        => DebugLogService.Instance.LogException(ex, "engine");

    // Engine messages are sometimes {0}-style format strings with args, sometimes already-interpolated
    // strings with no args. Format defensively so a mismatched template never throws here.
    private static string Format(string message, object[] args)
    {
        if (args is null || args.Length == 0) return message;
        try { return string.Format(message, args); }
        catch { return message; }
    }
}
