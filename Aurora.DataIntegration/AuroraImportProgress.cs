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
    public string? StatusText { get; init; }
    public bool IsIndeterminate { get; init; }

    // One shared calculation for Settings and its regression tests. These are weighted phase
    // shares, not a time estimate; unknown work must also show an animated activity indicator.
    public int Percentage => FilesTotal <= 0 ? 0 : Phase switch
    {
        AuroraImportPhase.Scanning => Math.Clamp((int)(FilesScanned * 50.0 / FilesTotal), 0, 50),
        AuroraImportPhase.Importing => 50 + Math.Clamp((int)(FilesScanned * 40.0 / FilesTotal), 0, 40),
        AuroraImportPhase.Resolving => 90,
        AuroraImportPhase.Complete => 100,
        _ => 0
    };

    public string PhaseLabel => StatusText ?? (Phase switch
    {
        AuroraImportPhase.Scanning  => "Scanning content files…",
        AuroraImportPhase.Importing when FilesTotal == 0 => "Importing content…",
        AuroraImportPhase.Importing => $"Importing content ({FilesChanged} file{(FilesChanged == 1 ? "" : "s")} changed)…",
        AuroraImportPhase.Resolving => "Resolving relationships…",
        AuroraImportPhase.Complete  => "Content database up to date.",
        _ => ""
    });
}

public sealed record AuroraImportResult(
    bool Success,
    int FilesProcessed,
    int FilesUnchanged,
    int ElementsImported,
    string? ErrorMessage,
    int FilesSkipped = 0,
    int AppendOperationsSkipped = 0,
    int UnavailableDefinitions = 0)
{
    public static AuroraImportResult Succeeded(int filesProcessed, int filesUnchanged, int elements, int filesSkipped = 0, int appendOperationsSkipped = 0, int unavailableDefinitions = 0) =>
        new(true, filesProcessed, filesUnchanged, elements, null, filesSkipped, appendOperationsSkipped, unavailableDefinitions);

    public static AuroraImportResult Failed(string reason) =>
        new(false, 0, 0, 0, reason);

    public string Summary => Success
        ? $"Content database updated: {ElementsImported} elements from {FilesProcessed} changed file{(FilesProcessed == 1 ? "" : "s")} ({FilesUnchanged} unchanged)."
          + (FilesSkipped > 0 ? $" {FilesSkipped} file{(FilesSkipped == 1 ? " was" : "s were")} skipped and need{(FilesSkipped == 1 ? "s" : "")} attention." : "")
          + (AppendOperationsSkipped > 0 ? $" {AppendOperationsSkipped} append operation{(AppendOperationsSkipped == 1 ? " was" : "s were")} skipped; other content was imported." : "")
          + (UnavailableDefinitions > 0 ? $" {UnavailableDefinitions} conflicting element ID{(UnavailableDefinitions == 1 ? " is" : "s are")} unavailable; no definition was selected, and unaffected content was imported." : "")
        : $"Content database update failed: {ErrorMessage}";
}
