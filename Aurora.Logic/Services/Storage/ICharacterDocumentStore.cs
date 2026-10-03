namespace Builder.Presentation.Services.Storage;

/// <summary>
/// A provider-neutral boundary for opening and saving portable Aurora character documents.
/// Hosts own authentication and file-picking; the character engine continues to work against
/// its local working copy.
/// </summary>
public interface ICharacterDocumentStore
{
    string ProviderId { get; }

    Task<CharacterDocument> OpenAsync(
        CharacterDocumentReference reference,
        CancellationToken cancellationToken = default);

    Task<CharacterDocumentMetadata> CreateAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        CharacterDocumentReference? parentFolder = null,
        CancellationToken cancellationToken = default);

    Task<CharacterDocumentMetadata> SaveAsync(
        CharacterDocumentMetadata expected,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable provider identifier returned by a picker or create operation.</summary>
public sealed record CharacterDocumentReference(string DocumentId, string? ResourceKey = null);

/// <summary>
/// Metadata required to save a character back to the same provider document without silently
/// replacing a newer remote revision.
/// </summary>
public sealed record CharacterDocumentMetadata(
    CharacterDocumentReference Reference,
    string FileName,
    string ProviderVersion,
    string? ContentHash,
    long? Size,
    DateTimeOffset? ModifiedTime,
    string? EntityTag = null);

/// <summary>A portable character file and the provider revision it came from.</summary>
public sealed record CharacterDocument(CharacterDocumentMetadata Metadata, byte[] Content);

public sealed class CharacterDocumentConflictException : IOException
{
    public CharacterDocumentMetadata Expected { get; }
    public CharacterDocumentMetadata Actual { get; }

    public CharacterDocumentConflictException(
        CharacterDocumentMetadata expected,
        CharacterDocumentMetadata actual)
        : base($"{actual.FileName} changed in cloud storage after Aurora opened it. " +
               "The current Drive copy must be loaded before saving.")
    {
        Expected = expected;
        Actual = actual;
    }
}
