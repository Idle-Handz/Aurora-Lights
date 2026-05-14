namespace Aurora.Importer;

public static class AuroraDatabaseVersions
{
    public const int SchemaVersion = 1;
    public const int DataVersion = 9;
}

public sealed record ContentDatabaseMetadata(
    int SchemaVersion,
    int DataVersion,
    string ImporterVersion,
    string BuiltUtc,
    int SourceFileCount,
    int ElementCount,
    string? ContentRootHash);
