namespace Aurora.Web.Services;

public sealed class PhaseZeroSessionOptions
{
    public const string SectionName = "PhaseZeroSessions";

    public string RootDirectory { get; set; } = string.Empty;
    public int IdleExpirationMinutes { get; set; } = 120;
    public int CleanupIntervalMinutes { get; set; } = 30;
    public int MaxFileCount { get; set; } = 64;
    public long MaxSingleFileBytes { get; set; } = 64L * 1024L * 1024L;
    public int MaxArchiveEntryCount { get; set; } = 512;
    public long MaxArchiveExpandedBytes { get; set; } = 256L * 1024L * 1024L;
}
