using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Builder.Presentation.Services.Storage;

/// <summary>
/// Supplies a current OAuth access token. Native and web hosts implement the authorization flow
/// independently while sharing the same Drive document implementation.
/// </summary>
public interface IGoogleDriveAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class GoogleDriveRequestException : HttpRequestException
{
    public GoogleDriveRequestException(HttpStatusCode statusCode, string message)
        : base($"Google Drive request failed ({(int)statusCode}): {message}", null, statusCode)
    {
    }
}

/// <summary>
/// Reads and writes .dnd5e files with Drive. Conditional writes use v2's explicit file ETag.
/// v3's monotonically increasing version is useful for detection, but is not a write precondition.
/// This service only
/// needs the per-file drive.file OAuth scope; file selection and consent remain host concerns.
/// </summary>
public sealed class GoogleDriveCharacterDocumentStore : ICharacterDocumentStore
{
    public const string CharacterFileExtension = ".dnd5e";
    public const string CharacterMimeType = "application/xml";

    private const string MetadataFields =
        "id,name,mimeType,modifiedTime,md5Checksum,size,version,resourceKey,trashed," +
        "capabilities(canDownload,canEdit)";
    private const string ConditionalFields =
        "id,title,modifiedDate,md5Checksum,fileSize,version,etag,resourceKey,labels(trashed)," +
        "capabilities(canDownload,canEdit)";
    public const int MaximumCharacterBytes = 32 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly IGoogleDriveAccessTokenProvider _accessTokenProvider;

    public GoogleDriveCharacterDocumentStore(
        HttpClient httpClient,
        IGoogleDriveAccessTokenProvider accessTokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    public string ProviderId => "google-drive";

    public async Task<CharacterDocument> OpenAsync(
        CharacterDocumentReference reference,
        CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        DriveFileState state = await GetMetadataAsync(reference, cancellationToken);
        EnsureCharacterFileName(state.Metadata.FileName);

        if (state.CanDownload is false)
            throw new UnauthorizedAccessException($"Google Drive does not allow downloading {state.Metadata.FileName}.");
        if (state.Metadata.Size is null or <= 0 or > MaximumCharacterBytes)
            throw new InvalidDataException("Drive's character size is missing or exceeds 32 MB.");

        string fileId = Uri.EscapeDataString(reference.DocumentId);
        string url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&supportsAllDrives=true";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddResourceKeyHeader(request, reference);
        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var buffer = new MemoryStream();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] block = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(block, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumCharacterBytes)
                throw new InvalidDataException("The downloaded character exceeds 32 MB.");
            buffer.Write(block, 0, count);
        }
        byte[] content = buffer.ToArray();
        VerifyContent(state.Metadata, content);
        DriveFileState after = await GetMetadataAsync(reference, cancellationToken);
        if (after.Metadata.ProviderVersion != state.Metadata.ProviderVersion
            || after.Metadata.EntityTag != state.Metadata.EntityTag)
            throw new CharacterDocumentConflictException(state.Metadata, after.Metadata);
        return new CharacterDocument(state.Metadata, content);
    }

    public async Task<CharacterDocumentMetadata> CreateAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        CharacterDocumentReference? parentFolder = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCharacterFileName(fileName);
        ValidateContentLength(content.Length);
        if (parentFolder is not null)
            ValidateReference(parentFolder);

        string url =
            "https://www.googleapis.com/upload/drive/v3/files" +
            $"?uploadType=multipart&supportsAllDrives=true&fields={Uri.EscapeDataString(MetadataFields)}";

        object metadata = parentFolder is null
            ? new { name = fileName, mimeType = CharacterMimeType }
            : new { name = fileName, mimeType = CharacterMimeType, parents = new[] { parentFolder.DocumentId } };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (parentFolder is not null)
            AddResourceKeyHeader(request, parentFolder);

        using var multipart = new MultipartContent("related");
        multipart.Add(new StringContent(JsonSerializer.Serialize(metadata), Encoding.UTF8, "application/json"));

        var media = new ByteArrayContent(content.ToArray());
        media.Headers.ContentType = MediaTypeHeaderValue.Parse(CharacterMimeType);
        multipart.Add(media);
        request.Content = multipart;

        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var created = await ReadMetadataAsync(response, fallbackReference: null, cancellationToken);
        VerifyContent(created, content.Span);
        return created;
    }

    public async Task<CharacterDocumentMetadata> SaveAsync(
        CharacterDocumentMetadata expected,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ValidateReference(expected.Reference);
        EnsureCharacterFileName(expected.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expected.ProviderVersion);
        ValidateContentLength(content.Length);

        DriveFileState current = await GetMetadataAsync(expected.Reference, cancellationToken);
        if (current.CanEdit is false)
            throw new UnauthorizedAccessException($"Google Drive does not allow editing {current.Metadata.FileName}.");

        if (!string.Equals(
                expected.ProviderVersion,
                current.Metadata.ProviderVersion,
                StringComparison.Ordinal))
        {
            throw new CharacterDocumentConflictException(expected, current.Metadata);
        }

        string fileId = Uri.EscapeDataString(expected.Reference.DocumentId);
        string url =
            $"https://www.googleapis.com/upload/drive/v2/files/{fileId}" +
            $"?uploadType=media&newRevision=true&supportsAllDrives=true&fields={Uri.EscapeDataString(ConditionalFields)}";

        // Never substitute '*' or the version number. Missing/weak tags cannot protect a save.
        if (!EntityTagHeaderValue.TryParse(current.Metadata.EntityTag, out var tag)
            || tag.IsWeak || tag.Tag == "*")
            throw new InvalidDataException("Drive did not provide a safe save token. Reload the cloud character and try again.");
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.IfMatch.Add(tag);
        AddResourceKeyHeader(request, expected.Reference);
        request.Content = new ByteArrayContent(content.ToArray());
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(CharacterMimeType);

        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            throw new CharacterDocumentConflictException(expected, current.Metadata);
        await EnsureSuccessAsync(response, cancellationToken);
        var saved = ToConditionalMetadata(await DeserializeMetadataAsync(response, cancellationToken), expected.Reference);
        VerifyContent(saved, content.Span);
        return saved;
    }

    private async Task<DriveFileState> GetMetadataAsync(
        CharacterDocumentReference reference,
        CancellationToken cancellationToken)
    {
        string fileId = Uri.EscapeDataString(reference.DocumentId);
        string url =
            $"https://www.googleapis.com/drive/v2/files/{fileId}" +
            $"?supportsAllDrives=true&fields={Uri.EscapeDataString(ConditionalFields)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddResourceKeyHeader(request, reference);
        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        DriveFileDto dto = await DeserializeMetadataAsync(response, cancellationToken);
        CharacterDocumentMetadata metadata = ToConditionalMetadata(dto, reference);
        if (dto.Labels?.Trashed == true)
            throw new FileNotFoundException($"{metadata.FileName} is in the Google Drive trash.", metadata.FileName);

        return new DriveFileState(metadata, dto.Capabilities?.CanDownload, dto.Capabilities?.CanEdit);
    }

    public async Task<IReadOnlyList<CharacterDocumentMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        var files = new List<CharacterDocumentMetadata>();
        string? page = null;
        do
        {
            string url = "https://www.googleapis.com/drive/v3/files?pageSize=100&spaces=drive"
                + "&q=" + Uri.EscapeDataString("trashed = false and mimeType != 'application/vnd.google-apps.folder'")
                + "&fields=" + Uri.EscapeDataString($"nextPageToken,files({MetadataFields})")
                + (page is null ? "" : "&pageToken=" + Uri.EscapeDataString(page));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var item in json.RootElement.GetProperty("files").EnumerateArray())
            {
                var dto = item.Deserialize<DriveFileDto>(JsonOptions)!;
                if (dto.Name?.EndsWith(CharacterFileExtension, StringComparison.OrdinalIgnoreCase) == true)
                    files.Add(ToMetadata(dto, null));
            }
            page = json.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        } while (!string.IsNullOrEmpty(page));
        return files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static CharacterDocumentMetadata ToConditionalMetadata(DriveFileDto dto, CharacterDocumentReference reference)
    {
        dto.Name = dto.Title;
        dto.Size = dto.FileSize;
        dto.ModifiedTime = dto.ModifiedDate;
        var metadata = ToMetadata(dto, reference) with { EntityTag = dto.Etag };
        if (metadata.Reference.DocumentId != reference.DocumentId)
            throw new InvalidDataException("Drive returned metadata for a different character.");
        return metadata;
    }

    private static void ValidateContentLength(int length)
    {
        if (length == 0 || length > MaximumCharacterBytes)
            throw new InvalidDataException("A cloud character must contain between 1 byte and 32 MB.");
    }

    private static void VerifyContent(CharacterDocumentMetadata metadata, ReadOnlySpan<byte> content)
    {
        ValidateContentLength(content.Length);
        // Drive's MD5 is an integrity check, not authentication. TLS/OAuth provide authentication.
        if (metadata.Size != content.Length || string.IsNullOrWhiteSpace(metadata.ContentHash)
            || !string.Equals(Convert.ToHexString(MD5.HashData(content)), metadata.ContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Drive's character checksum or size did not match. The save/download was not confirmed.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Google Drive authorization did not return an access token.");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static void AddResourceKeyHeader(
        HttpRequestMessage request,
        CharacterDocumentReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.ResourceKey))
            return;

        request.Headers.TryAddWithoutValidation(
            "X-Goog-Drive-Resource-Keys",
            $"{reference.DocumentId}/{reference.ResourceKey}");
    }

    private static async Task<CharacterDocumentMetadata> ReadMetadataAsync(
        HttpResponseMessage response,
        CharacterDocumentReference? fallbackReference,
        CancellationToken cancellationToken)
    {
        DriveFileDto dto = await DeserializeMetadataAsync(response, cancellationToken);
        return ToMetadata(dto, fallbackReference);
    }

    private static async Task<DriveFileDto> DeserializeMetadataAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        DriveFileDto? dto = await JsonSerializer.DeserializeAsync<DriveFileDto>(
            stream,
            JsonOptions,
            cancellationToken);
        return dto ?? throw new InvalidDataException("Google Drive returned empty file metadata.");
    }

    private static CharacterDocumentMetadata ToMetadata(
        DriveFileDto dto,
        CharacterDocumentReference? fallbackReference)
    {
        string documentId = dto.Id ?? fallbackReference?.DocumentId
            ?? throw new InvalidDataException("Google Drive file metadata did not include an id.");
        string fileName = dto.Name
            ?? throw new InvalidDataException("Google Drive file metadata did not include a name.");
        string providerVersion = dto.Version
            ?? throw new InvalidDataException("Google Drive file metadata did not include a version.");

        long? size = null;
        if (!string.IsNullOrWhiteSpace(dto.Size))
        {
            if (!long.TryParse(dto.Size, out long parsedSize))
                throw new InvalidDataException("Google Drive file metadata included an invalid size.");
            size = parsedSize;
        }

        return new CharacterDocumentMetadata(
            new CharacterDocumentReference(documentId, dto.ResourceKey ?? fallbackReference?.ResourceKey),
            fileName,
            providerVersion,
            dto.Md5Checksum,
            size,
            dto.ModifiedTime);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message = TryReadGoogleErrorMessage(body)
            ?? response.ReasonPhrase
            ?? "Unknown error";
        throw new GoogleDriveRequestException(response.StatusCode, message);
    }

    private static string? TryReadGoogleErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out JsonElement error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out JsonElement message)
                && message.ValueKind == JsonValueKind.String
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ValidateReference(CharacterDocumentReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.DocumentId);
    }

    private static void EnsureCharacterFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!fileName.EndsWith(CharacterFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"'{fileName}' is not an Aurora {CharacterFileExtension} character file.");
        }
    }

    private sealed record DriveFileState(
        CharacterDocumentMetadata Metadata,
        bool? CanDownload,
        bool? CanEdit);

    private sealed class DriveFileDto
    {
        public string? Title { get; set; }
        public string? FileSize { get; set; }
        public DateTimeOffset? ModifiedDate { get; set; }
        public string? Etag { get; set; }
        public DriveLabelsDto? Labels { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? MimeType { get; set; }
        public DateTimeOffset? ModifiedTime { get; set; }
        public string? Md5Checksum { get; set; }
        public string? Size { get; set; }
        public string? Version { get; set; }
        public string? ResourceKey { get; set; }
        public bool Trashed { get; set; }
        public DriveCapabilitiesDto? Capabilities { get; set; }
    }

    private sealed class DriveLabelsDto { public bool Trashed { get; set; } }

    private sealed class DriveCapabilitiesDto
    {
        public bool? CanDownload { get; set; }
        public bool? CanEdit { get; set; }
    }
}
