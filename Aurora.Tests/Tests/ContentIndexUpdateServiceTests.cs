using System.Collections.Concurrent;
using System.Net;
using Builder.Presentation.Services.Content;

namespace Aurora.Tests.Tests;

public sealed class ContentIndexUpdateServiceTests
{
    [Fact]
    public async Task UpdateAsync_downloads_nested_index_files_into_legacy_layout()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "core.index"),
                """
                <index>
                  <files>
                    <file name="root.xml" url="https://example.test/root.xml" />
                    <file name="child.index" url="https://example.test/child.index" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond("https://example.test/root.xml", Ok("<elements id=\"root\" />"));
            handler.Respond(
                "https://example.test/child.index",
                Ok(
                    """
                    <index>
                      <files>
                        <file name="leaf.xml" url="https://example.test/leaf.xml" />
                      </files>
                    </index>
                    """));
            handler.Respond("https://example.test/leaf.xml", Ok("<elements id=\"leaf\" />"));

            var service = new ContentIndexUpdateService(new HttpClient(handler));

            ContentIndexUpdateResult result = await service.UpdateAsync(
                new ContentIndexUpdateRequest(root, ["core.index"], MaxConcurrency: 2));

            result.Updated.Should().BeTrue();
            result.UpdatedFileCount.Should().Be(3);
            result.CheckedEntryCount.Should().Be(3);
            result.IndexFileCount.Should().Be(2);
            File.Exists(Path.Combine(root, "core", "root.xml")).Should().BeTrue();
            File.Exists(Path.Combine(root, "core", "child.index")).Should().BeTrue();
            File.Exists(Path.Combine(root, "core", "child", "leaf.xml")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_reuses_cached_etag_on_later_checks()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "content.index"),
                """
                <index>
                  <files>
                    <file name="a.xml" url="https://example.test/a.xml" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond(
                "https://example.test/a.xml",
                Ok("<elements id=\"first\" />", etag: "\"abc\""),
                NotModified());

            var service = new ContentIndexUpdateService(new HttpClient(handler));
            await service.UpdateAsync(new ContentIndexUpdateRequest(root, ["content.index"]));

            ContentIndexUpdateResult second = await service.UpdateAsync(
                new ContentIndexUpdateRequest(root, ["content.index"]));

            second.Updated.Should().BeFalse();
            handler.Requests
                .Where(request => request.Url == "https://example.test/a.xml")
                .Last()
                .IfNoneMatch
                .Should()
                .Contain("\"abc\"");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_redownloads_when_cache_exists_but_local_file_is_missing()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "content.index"),
                """
                <index>
                  <files>
                    <file name="a.xml" url="https://example.test/a.xml" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond(
                "https://example.test/a.xml",
                Ok("<elements id=\"first\" />", etag: "\"abc\""),
                request =>
                {
                    request.Headers.IfNoneMatch.Should().BeEmpty();
                    return Ok("<elements id=\"second\" />", etag: "\"abc\"")(request);
                });

            var service = new ContentIndexUpdateService(new HttpClient(handler));
            await service.UpdateAsync(new ContentIndexUpdateRequest(root, ["content.index"]));
            File.Delete(Path.Combine(root, "content", "a.xml"));

            ContentIndexUpdateResult second = await service.UpdateAsync(
                new ContentIndexUpdateRequest(root, ["content.index"]));

            second.Updated.Should().BeTrue();
            File.ReadAllText(Path.Combine(root, "content", "a.xml")).Should().Contain("second");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Ok(string content, string? etag = null)
    {
        return _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
            if (!string.IsNullOrWhiteSpace(etag))
                response.Headers.TryAddWithoutValidation("ETag", etag);
            response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
            return response;
        };
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> NotModified()
        => _ => new HttpResponseMessage(HttpStatusCode.NotModified);

    private sealed record RecordedRequest(string Url, IReadOnlyList<string> IfNoneMatch, DateTimeOffset? IfModifiedSince);

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Queue<Func<HttpRequestMessage, HttpResponseMessage>>> _responses =
            new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        public void Respond(string url, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            _responses[url] = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString()
                ?? throw new InvalidOperationException("Expected an absolute request URI.");
            Requests.Enqueue(new RecordedRequest(
                url,
                request.Headers.IfNoneMatch.Select(value => value.ToString()).ToList(),
                request.Headers.IfModifiedSince));

            Func<HttpRequestMessage, HttpResponseMessage> responseFactory;
            lock (_gate)
            {
                if (!_responses.TryGetValue(url, out var queue) || queue.Count == 0)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                responseFactory = queue.Dequeue();
            }

            return Task.FromResult(responseFactory(request));
        }
    }
}
