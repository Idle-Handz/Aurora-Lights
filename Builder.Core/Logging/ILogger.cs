using System;

namespace Builder.Core.Logging;

public interface ILogger
{
    void Debug(string message, params object[] args);

    void Info(string message, params object[] args);

    void Warning(string message, params object[] args);

    void Exception(Exception ex);
}
