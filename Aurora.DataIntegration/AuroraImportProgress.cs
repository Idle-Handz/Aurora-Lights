namespace Aurora.App.Services;

/// <summary>Progress the Settings page shows while the content database is rebuilt.</summary>
public enum AuroraImportPhase
{
    Scanning,
    Importing,
    Resolving,
    Complete
}

public sealed record AuroraImportProgress(
    AuroraImportPhase Phase,
    int FilesScanned,
    int FilesTotal,
    int FilesChanged,
    int ElementsImported,
    string? CurrentFile)
{
    public string PhaseLabel => Phase switch
    {
        AuroraImportPhase.Scanning  => "Scanning content files…",
        AuroraImportPhase.Importing when FilesTotal == 0 => "Importing content…",
        AuroraImportPhase.Importing => $"Importing content ({FilesChanged} file{(FilesChanged == 1 ? "" : "s")} changed)…",
        AuroraImportPhase.Resolving => "Resolving relationships…",
        AuroraImportPhase.Complete  => "Content database up to date.",
        _ => ""
    };
}

public sealed record AuroraImportResult(
    bool Success,
    int FilesProcessed,
    int FilesUnchanged,
    int ElementsImported,
    string? ErrorMessage,
    int FilesSkipped = 0)
{
    public static AuroraImportResult Succeeded(int filesProcessed, int filesUnchanged, int elements, int filesSkipped = 0) =>
        new(true, filesProcessed, filesUnchanged, elements, null, filesSkipped);

    public static AuroraImportResult Failed(string reason) =>
        new(false, 0, 0, 0, reason);

    public string Summary => Success
        ? $"Content database updated: {ElementsImported} elements from {FilesProcessed} changed file{(FilesProcessed == 1 ? "" : "s")} ({FilesUnchanged} unchanged)."
          + (FilesSkipped > 0 ? $" {FilesSkipped} file{(FilesSkipped == 1 ? " was" : "s were")} skipped and need attention." : "")
        : $"Content database update failed: {ErrorMessage}";
}
