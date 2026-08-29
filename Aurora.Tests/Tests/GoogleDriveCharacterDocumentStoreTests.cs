using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveCharacterDocumentStoreTests
{
    [Fact]
    public async Task OpenAsync_DownloadsSelectedCharacterAndReturnsConflictMetadata()
    {
        var handler = new QueueHandler();
        handler.Enqueue((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.Should().Be("/drive/v3/files/drive-file-1");
            Uri.UnescapeDataString(request.RequestUri.Query).Should().Contain("fields=id,name");
            request.Headers.Authorization.Should().BeEquivalentTo(
                new AuthenticationHeaderValue("Bearer", "test-token"));
            request.Headers.GetValues("X-Goog-Drive-Resource-Keys").Should()
                .ContainSingle().Which.Should().Be("drive-file-1/resource-key-1");
            return JsonResponse(MetadataJson(version: "7"));
        });
        handler.Enqueue((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.Query.Should().Contain("alt=media");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("<Character />"u8.ToArray()),
            };
        });

        var store = CreateStore(handler);
        CharacterDocument document = await store.OpenAsync(
            new CharacterDocumentReference("drive-file-1", "resource-key-1"));

        Encoding.UTF8.GetString(document.Content).Should().Be("<Character />");
        document.Metadata.FileName.Should().Be("Aria.dnd5e");
        document.Metadata.ProviderVersion.Should().Be("7");
        document.Metadata.ContentHash.Should().Be("md5-7");
        document.Metadata.Size.Should().Be(42);
        document.Metadata.Reference.ResourceKey.Should().Be("resource-key-1");
        handler.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_UploadsCharacterAndFolderMetadataTogether()
    {
        var handler = new QueueHandler();
        handler.Enqueue(async (request, cancellationToken) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/upload/drive/v3/files");
            request.RequestUri.Query.Should().Contain("uploadType=multipart");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/related");
            request.Headers.GetValues("X-Goog-Drive-Resource-Keys").Should()
                .ContainSingle().Which.Should().Be("folder-1/folder-key");

            string body = await request.Content.ReadAsStringAsync(cancellationToken);
            body.Should().Contain("\"name\":\"Aria.dnd5e\"");
            body.Should().Contain("\"parents\":[\"folder-1\"]");
            body.Should().Contain("<Character />");
            return JsonResponse(MetadataJson(version: "1"));
        });

        var store = CreateStore(handler);
        CharacterDocumentMetadata created = await store.CreateAsync(
            "Aria.dnd5e",
            "<Character />"u8.ToArray(),
            new CharacterDocumentReference("folder-1", "folder-key"));

        created.Reference.DocumentId.Should().Be("drive-file-1");
        created.ProviderVersion.Should().Be("1");
        handler.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WhenVersionMatches_UpdatesTheSameDriveFile()
    {
        var handler = new QueueHandler();
        handler.Enqueue((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            return JsonResponse(MetadataJson(version: "7"));
        });
        handler.Enqueue(async (request, cancellationToken) =>
        {
            request.Method.Should().Be(HttpMethod.Patch);
            request.RequestUri!.AbsolutePath.Should().Be("/upload/drive/v3/files/drive-file-1");
            request.RequestUri.Query.Should().Contain("uploadType=media");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/xml");
            (await request.Content.ReadAsStringAsync(cancellationToken)).Should().Be("<Character level=\"2\" />");
            return JsonResponse(MetadataJson(version: "8", checksum: "md5-8"));
        });

        var store = CreateStore(handler);
        CharacterDocumentMetadata saved = await store.SaveAsync(
            ExpectedMetadata(version: "7"),
            "<Character level=\"2\" />"u8.ToArray());

        saved.ProviderVersion.Should().Be("8");
        saved.ContentHash.Should().Be("md5-8");
        handler.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WhenDriveVersionChanged_RejectsOverwriteBeforeUpload()
    {
        var handler = new QueueHandler();
        handler.Enqueue((_, _) => JsonResponse(MetadataJson(version: "8", checksum: "remote-change")));
        var store = CreateStore(handler);

        Func<Task> save = () => store.SaveAsync(
            ExpectedMetadata(version: "7"),
            "<Character level=\"2\" />"u8.ToArray());

        CharacterDocumentConflictException conflict = (await save.Should()
                .ThrowAsync<CharacterDocumentConflictException>())
            .Which;
        conflict.Expected.ProviderVersion.Should().Be("7");
        conflict.Actual.ProviderVersion.Should().Be("8");
        conflict.Message.Should().Contain("changed in cloud storage");
        handler.Remaining.Should().Be(0, "a conflicting character must not be uploaded");
    }

    [Fact]
    public async Task OpenAsync_WhenDriveRejectsRequest_SurfacesTheGoogleErrorMessage()
    {
        var handler = new QueueHandler();
        handler.Enqueue((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Access denied by file owner\"}}"),
        });
        var store = CreateStore(handler);

        Func<Task> open = () => store.OpenAsync(new CharacterDocumentReference("drive-file-1"));

        GoogleDriveRequestException error = (await open.Should()
                .ThrowAsync<GoogleDriveRequestException>())
            .Which;
        error.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        error.Message.Should().Contain("Access denied by file owner");
    }

    private static GoogleDriveCharacterDocumentStore CreateStore(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new StaticTokenProvider());

    private static CharacterDocumentMetadata ExpectedMetadata(string version) =>
        new(
            new CharacterDocumentReference("drive-file-1", "resource-key-1"),
            "Aria.dnd5e",
            version,
            $"md5-{version}",
            42,
            DateTimeOffset.Parse("2026-08-29T12:00:00Z"));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static string MetadataJson(string version, string? checksum = null) => $$"""
        {
          "id": "drive-file-1",
          "name": "Aria.dnd5e",
          "mimeType": "application/xml",
          "modifiedTime": "2026-08-29T12:00:00Z",
          "md5Checksum": "{{checksum ?? $"md5-{version}"}}",
          "size": "42",
          "version": "{{version}}",
          "trashed": false,
          "capabilities": {
            "canDownload": true,
            "canEdit": true
          }
        }
        """;

    private sealed class StaticTokenProvider : IGoogleDriveAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult("test-token");
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = [];

        public int Remaining => _responses.Count;

        public void Enqueue(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> response) =>
            _responses.Enqueue((request, cancellationToken) => Task.FromResult(response(request, cancellationToken)));

        public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) =>
            _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");

            return _responses.Dequeue()(request, cancellationToken);
        }
    }
}
