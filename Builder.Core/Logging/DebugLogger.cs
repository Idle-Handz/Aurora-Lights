using System;

namespace Builder.Core.Logging;

public class DebugLogger : ILogger
{
    public bool EnableTimestamp { get; set; }

    public bool SurpressDebugLogging { get; set; }

    public bool SurpressInfoLogging { get; set; }

    public DebugLogger(bool enableTimestamp)
    {
        EnableTimestamp = enableTimestamp;
    }

    public void Debug(string message, params object[] args)
    {
        if (!SurpressDebugLogging)
        {
            Write(GeneratePrefix(Log.Debug) + message, args);
        }
    }

    public void Info(string message, params object[] args)
    {
        if (!SurpressInfoLogging)
        {
            Write(GeneratePrefix(Log.Info) + message, args);
        }
    }

    public void Warning(string message, params object[] args)
    {
        Write(GeneratePrefix(Log.Warning) + message, args);
    }

    public void Exception(Exception ex)
    {
        Write(
            GeneratePrefix(Log.Exception) + "{0} {1}: {2}",
            ex.GetType().Name,
            ex.InnerException != null ? "has inner exception" : "with no inner exception",
            ex.Message);
        Write("Source: {0}", ex.Source);
        Write("Trace: {0}", ex.StackTrace);
        if (ex.InnerException != null)
        {
            Write("Inner Exception: {0}", ex.InnerException);
        }
    }

    private static void Write(string message, params object[] args)
    {
        _ = args?.LongLength;
    }

    private string GeneratePrefix(Log log)
    {
        if (EnableTimestamp)
        {
            return DateTime.UtcNow.ToString("u") + " | " + log.ToString().ToUpper() + " | ";
        }

        return log.ToString().ToUpper() + " | ";
    }
}
