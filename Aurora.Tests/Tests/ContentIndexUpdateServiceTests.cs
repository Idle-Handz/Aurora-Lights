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
            result.UpdatedContentFileCount.Should().Be(2, "only XML files affect the element database");
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

    [Fact]
    public async Task UpdateAsync_reports_bad_index_and_continues_with_other_sources()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "bad.index"), "<index><files>");
            File.WriteAllText(
                Path.Combine(root, "good.index"),
                """
                <index>
                  <files>
                    <file name="a.xml" url="https://example.test/a.xml" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond("https://example.test/a.xml", Ok("<elements id=\"good\" />"));

            var service = new ContentIndexUpdateService(new HttpClient(handler));

            ContentIndexUpdateResult result = await service.UpdateAsync(
                new ContentIndexUpdateRequest(root, ["bad.index", "good.index"]));

            result.Updated.Should().BeTrue();
            result.UpdatedFileCount.Should().Be(1);
            result.FailedFileCount.Should().Be(1);
            result.IndexFileCount.Should().Be(2);
            File.ReadAllText(Path.Combine(root, "good", "a.xml")).Should().Contain("good");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_reports_stalled_download_and_continues_with_other_entries()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "content.index"),
                """
                <index>
                  <files>
                    <file name="slow.xml" url="https://example.test/slow.xml" />
                    <file name="fast.xml" url="https://example.test/fast.xml" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond("https://example.test/slow.xml", Stalled());
            handler.Respond("https://example.test/fast.xml", Ok("<elements id=\"fast\" />"));

            var service = new ContentIndexUpdateService(new HttpClient(handler));

            ContentIndexUpdateResult result = await service.UpdateAsync(
                new ContentIndexUpdateRequest(
                    root,
                    ["content.index"],
                    MaxConcurrency: 1,
                    EntryDownloadTimeout: TimeSpan.FromMilliseconds(50)));

            result.Updated.Should().BeTrue();
            result.UpdatedFileCount.Should().Be(1);
            result.FailedFileCount.Should().Be(1);
            result.CheckedEntryCount.Should().Be(2);
            File.Exists(Path.Combine(root, "content", "slow.xml")).Should().BeFalse();
            File.ReadAllText(Path.Combine(root, "content", "fast.xml")).Should().Contain("fast");
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

    private static Func<HttpRequestMessage, HttpResponseMessage> Stalled()
        => _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StalledContent()
        };

    private sealed record RecordedRequest(string Url, IReadOnlyList<string> IfNoneMatch, DateTimeOffset? IfModifiedSince);

    /// <summary>
    /// The cache records what the server sent, not what is on disk. When a local file stops
    /// matching it - an interrupted write, a restore from a backup, a copy from somewhere older -
    /// sending the cached validators makes the server answer "not modified" and the stale file can
    /// never catch up. This happened for real: an index cached in June still described the current
    /// file on the server, while the copy on disk was the May revision, so content updates
    /// reported "nothing to do" indefinitely.
    /// </summary>
    [Theory]
    [InlineData("stale")]
    [InlineData("earlier")]
    public async Task UpdateAsync_refetches_when_the_cached_entry_no_longer_describes_the_local_file(string restoredId)
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "core.index"),
                """
                <index>
                  <files>
                    <file name="book.xml" url="https://example.test/book.xml" />
                  </files>
                </index>
                """);

            var handler = new SequenceHandler();
            handler.Respond(
                "https://example.test/book.xml",
                Ok("<elements id=\"current\" />", etag: "\"current\""),
                // A real server: it answers 304 to the validator it issued, and only sends the
                // file to a request that does not claim to hold it already.
                request => request.Headers.IfNoneMatch.Any(tag => tag.ToString() == "\"current\"")
                    ? new HttpResponseMessage(HttpStatusCode.NotModified)
                    : Ok("<elements id=\"current\" />", etag: "\"current\"")(request));

            var service = new ContentIndexUpdateService(new HttpClient(handler));
            var request = new ContentIndexUpdateRequest(root, new[] { "core.index" });

            await service.UpdateAsync(request);
            string downloaded = Path.Combine(root, "core", "book.xml");
            File.Exists(downloaded).Should().BeTrue();

            // The file drifts away from what the cache describes.
            File.WriteAllText(downloaded, $"<elements id=\"{restoredId}\" />");

            ContentIndexUpdateResult result = await service.UpdateAsync(request);

            File.ReadAllText(downloaded).Should().Contain("current",
                "the cached entry no longer describes this file, so it must be fetched again");
            result.UpdatedFileCount.Should().Be(1);
            handler.Requests.Last().IfNoneMatch.Should().BeEmpty(
                "claiming to hold content the file does not have is what made the server answer 304");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_does_not_treat_an_uncached_local_write_time_as_a_server_validator()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "core.index"),
                "<index><files><file name='book.xml' url='https://example.test/book.xml'/></files></index>");
            Directory.CreateDirectory(Path.Combine(root, "core"));
            string book = Path.Combine(root, "core", "book.xml");
            File.WriteAllText(book, "<elements id='restored' />");
            var handler = new SequenceHandler();
            handler.Respond("https://example.test/book.xml", request =>
                request.Headers.IfModifiedSince.HasValue
                    ? new HttpResponseMessage(HttpStatusCode.NotModified)
                    : Ok("<elements id='current' />")(request));
            using var client = new HttpClient(handler);

            var result = await new ContentIndexUpdateService(client).UpdateAsync(new(root, ["core.index"]));

            result.UpdatedFileCount.Should().Be(1);
            File.ReadAllText(book).Should().Contain("current");
            handler.Requests.Single().IfModifiedSince.Should().BeNull();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("book.xml", "<elements><element")]
    [InlineData("child.index", "<index><files>")]
    [InlineData("child.index", "<html><body>Temporary server error</body></html>")]
    [InlineData("fix.aurora-correction", "<elements><proposal")]
    [InlineData("fix.aurora-correction", "<html>Temporary server error</html>")]
    public async Task UpdateAsync_preserves_existing_content_when_a_download_is_not_valid_xml_or_index(
        string filename, string invalidDownload)
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "core.index"),
                $"<index><files><file name='{filename}' url='https://example.test/content'/></files></index>");
            Directory.CreateDirectory(Path.Combine(root, "core"));
            string destination = Path.Combine(root, "core", filename);
            string original = filename.EndsWith(".index") ? "<index />" : "<elements id='good' />";
            File.WriteAllText(destination, original);
            var handler = new SequenceHandler();
            handler.Respond("https://example.test/content", Ok(invalidDownload, etag: "\"invalid\""));
            using var client = new HttpClient(handler);

            var result = await new ContentIndexUpdateService(client).UpdateAsync(new(root, ["core.index"]));

            result.Updated.Should().BeFalse();
            result.FailedFileCount.Should().Be(1);
            File.ReadAllText(destination).Should().Be(original);
            Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UpdateAsync_does_not_delete_protected_corrections_listed_as_obsolete()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "core.index"),
                "<index><files><obsolete name='fix.xml'/><obsolete name='old.xml'/></files></index>");
            Directory.CreateDirectory(Path.Combine(root, "core"));
            const string baseline = "<elements><element name='Old' type='Item' source='Test' id='ID_FIX'/></elements>";
            string protectedXml = Aurora.Content.Contracts.LocalCorrectionDocument.Create(
                baseline.Replace("name='Old'", "name='Fixed'"), baseline, "source.xml",
                [new Aurora.Content.Contracts.LocalCorrection("fix", "replace", "ID_FIX", null, null, "review-pending")]);
            string protectedPath = Path.Combine(root, "core", "fix.xml");
            string obsoletePath = Path.Combine(root, "core", "old.xml");
            File.WriteAllText(protectedPath, protectedXml);
            File.WriteAllText(obsoletePath, "<elements />");
            using var client = new HttpClient(new SequenceHandler());

            var result = await new ContentIndexUpdateService(client).UpdateAsync(new(root, ["core.index"]));

            File.Exists(protectedPath).Should().BeTrue("correction protection also applies to automatic deletion");
            File.ReadAllText(protectedPath).Should().Be(protectedXml);
            File.Exists(obsoletePath).Should().BeFalse();
            result.FailedFileCount.Should().Be(1);
            result.UpdatedFileCount.Should().Be(1);
            result.UpdatedContentFileCount.Should().Be(1, "removing obsolete XML also requires a refresh");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Proposal_download_and_retirement_do_not_change_active_content()
    {
        string root = CreateTempDirectory();
        try
        {
            string index = Path.Combine(root, "repairs.index");
            File.WriteAllText(index, "<index><files><file name='fix.aurora-correction' url='https://example.test/fix'/></files></index>");
            var handler = new SequenceHandler();
            handler.Respond("https://example.test/fix", Ok("<elements><info><name>Repair proposal</name></info></elements>"));
            using var client = new HttpClient(handler);
            var service = new ContentIndexUpdateService(client);
            var download = await service.UpdateAsync(new(root, ["repairs.index"]));
            download.Updated.Should().BeTrue();
            download.UpdatedContentFileCount.Should().Be(0);
            File.Exists(Path.Combine(root, "repairs", "fix.aurora-correction")).Should().BeTrue();
            Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories).Should().BeEmpty();

            File.WriteAllText(index, "<index><files><obsolete name='fix.aurora-correction'/></files></index>");
            var removal = await service.UpdateAsync(new(root, ["repairs.index"]));
            removal.Updated.Should().BeTrue();
            removal.UpdatedContentFileCount.Should().Be(0);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

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

    private sealed class StalledContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.Delay(Timeout.InfiniteTimeSpan);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
            => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
