namespace Aurora.Reflections.Services;

/// <summary>Distinguishes HTTP timeouts and describes failures without logging provider payloads.</summary>
public static class GoogleDriveErrorReporting
{
    public const string RequestTimeoutMessage =
        "Google Drive did not respond in time. Check your connection and refresh Cloud Saves before retrying.";

    // HttpClient wraps its own timeout in OperationCanceledException. Cancellation
    // requested by the caller still takes precedence over a concurrent timeout.
    public static bool IsRequestTimeout(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is OperationCanceledException { InnerException: TimeoutException };

    public static string AndroidAuthorizationFailureMessage(int statusCode, string? providerMessage)
    {
        // Play services can report an unregistered build as INTERNAL_ERROR (8)
        // with this marker rather than DEVELOPER_ERROR (10). Never echo its raw payload.
        if (statusCode == 10 || providerMessage?.Contains("UNREGISTERED_ON_API_CONSOLE", StringComparison.OrdinalIgnoreCase) == true)
            return $"Google Drive is not configured for this Android build (code {statusCode}). "
                + "Register its package name and signing certificate SHA-1 as an Android OAuth client in the same Google Cloud project as the desktop client. "
                + "For Google Play installs, use the Play app-signing certificate, not the upload certificate.";
        return $"Google Drive authorization failed (code {statusCode}). Check your connection and reconnect.";
    }

    public static string DescribeFailure(Exception exception)
    {
        var causes = new List<string>();
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            string description = cause.GetType().Name;
            if (cause is HttpRequestException http)
            {
                if (http.StatusCode is { } status) description += $" (HTTP {(int)status})";
                description += $" ({http.HttpRequestError})";
            }
            causes.Add(description);
        }
        // Exception messages, Data, and ToString() may contain account details,
        // URLs, tokens, or character content. Record only types and status codes.
        return string.Join(" -> ", causes);
    }
}
