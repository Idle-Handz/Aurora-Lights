namespace Aurora.Importer;

public enum ContentDatabaseHealthStatus
{
    Healthy,
    Warning,
    Error
}

public sealed record ContentDatabaseHealthReport(
    int ActionableUnresolvedLinks,
    int ClassifiedUnresolvedLinks,
    int SourceIntegrityIssues,
    int MissingResolvedSpellRows,
    int MissingResolvedItemRows,
    int MissingResolvedCompanionRows)
{
    public int ProjectionIssues =>
        MissingResolvedSpellRows + MissingResolvedItemRows + MissingResolvedCompanionRows;

    public int TotalIssues =>
        ActionableUnresolvedLinks + SourceIntegrityIssues + ProjectionIssues;

    public ContentDatabaseHealthStatus Status =>
        SourceIntegrityIssues > 0 || ProjectionIssues > 0
            ? ContentDatabaseHealthStatus.Error
            : ActionableUnresolvedLinks > 0
                ? ContentDatabaseHealthStatus.Warning
                : ContentDatabaseHealthStatus.Healthy;
}
