using System.Net;
using Aurora.Content;
using Builder.Presentation.Services.Content;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentStoragePressureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_storage_write_failure_removes_temp_preserves_accepted_bytes_and_retries(bool existing)
    {
        using var fixture = new DownloadFixture(existing);
        await fixture.EstablishAsync();
        byte[]? before = existing ? File.ReadAllBytes(fixture.Destination) : null;
        byte[]? cache = fixture.CacheBytes();
        var updater = new ContentIndexUpdateService(fixture.Client, path => new FullStream(path));

        for (int attempt = 0; attempt < 3; attempt++)
        {
            var result = await updater.UpdateAsync(fixture.Request);
            result.FailedFileCount.Should().Be(1);
            result.Updated.Should().BeFalse();
            if (existing) File.ReadAllBytes(fixture.Destination).Should().Equal(before!);
            else File.Exists(fixture.Destination).Should().BeFalse();
            if (cache is null) fixture.CacheBytes().Should().BeNull();
            else fixture.CacheBytes().Should().Equal(cache);
            Directory.GetFiles(fixture.Root, "*.tmp-*", SearchOption.AllDirectories).Should().BeEmpty();
        }
        var retry = await new ContentIndexUpdateService(fixture.Client).UpdateAsync(fixture.Request);
        retry.FailedFileCount.Should().Be(0);
        File.ReadAllText(fixture.Destination).Should().Contain("version='2'");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Marker_creation_failure_refuses_replacement_and_can_retry(bool existing)
    {
        using var fixture = new DownloadFixture(existing);
        await fixture.EstablishAsync();
        byte[]? before = existing ? File.ReadAllBytes(fixture.Destination) : null;
        string marker = Path.Combine(fixture.Root, ContentIndexUpdateService.PendingContentChangesFileName);
        File.Delete(marker);
        Directory.CreateDirectory(marker);
        var result = await new ContentIndexUpdateService(fixture.Client).UpdateAsync(fixture.Request);
        result.FailedFileCount.Should().Be(1);
        result.Updated.Should().BeFalse();
        if (existing) File.ReadAllBytes(fixture.Destination).Should().Equal(before!);
        else File.Exists(fixture.Destination).Should().BeFalse();
        Directory.Delete(marker);
        (await new ContentIndexUpdateService(fixture.Client).UpdateAsync(fixture.Request)).FailedFileCount.Should().Be(0);
        File.ReadAllText(fixture.Destination).Should().Contain("version='2'");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cache_write_failure_keeps_complete_download_and_next_check_repairs_metadata(bool existing)
    {
        using var fixture = new DownloadFixture(existing);
        await fixture.EstablishAsync();
        string cacheDirectory = Path.Combine(fixture.Root, ".aurora-index-cache");
        if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, true);
        string cachePath = Path.Combine(cacheDirectory, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("https://example.test/book.xml"))).ToLowerInvariant() + ".json");
        Directory.CreateDirectory(cachePath);
        var result = await new ContentIndexUpdateService(fixture.Client).UpdateAsync(fixture.Request);
        result.FailedFileCount.Should().Be(0);
        result.UpdatedContentFileCount.Should().Be(1);
        File.ReadAllText(fixture.Destination).Should().Contain("version='2'");
        File.Exists(Path.Combine(fixture.Root, ContentIndexUpdateService.PendingContentChangesFileName)).Should().BeTrue();
        Directory.Delete(cachePath);
        (await new ContentIndexUpdateService(fixture.Client).UpdateAsync(fixture.Request)).FailedFileCount.Should().Be(0);
        Directory.GetFiles(cacheDirectory, "*.json").Should().HaveCount(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sqlite_full_during_candidate_refresh_preserves_live_catalog_and_retries(bool existing)
    {
        string root = NewDirectory();
        try
        {
            string source = Path.Combine(root, "book.xml"), database = Path.Combine(root, "accepted.sqlite");
            File.WriteAllText(source, Book("Accepted"));
            if (existing) await ContentImport.ImportAsync(root, database);
            byte[]? before = existing ? File.ReadAllBytes(database) : null;
            File.WriteAllText(source, Book("Recovered"));
            bool reached = false;
            var progress = new InlineProgress(value =>
            {
                if (value.Phase != ContentImportPhase.Activating) return;
                reached = true;
                string candidate = Directory.GetFiles(root, ".aurora-candidate-*.sqlite").Single();
                using var connection = new SqliteConnection($"Data Source={candidate};Pooling=False");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA page_count";
                long pages = (long)command.ExecuteScalar()!;
                command.CommandText = $"PRAGMA max_page_count={pages}";
                command.ExecuteScalar();
                command.CommandText = "CREATE TABLE storage_probe (payload BLOB); INSERT INTO storage_probe VALUES (zeroblob(1048576))";
                command.ExecuteNonQuery(); // Real SQLITE_FULL, confined to this disposable candidate.
            });
            var failure = await FluentActions.Awaiting(() => ContentImport.ImportAsync(root, database, progress))
                .Should().ThrowAsync<SqliteException>();
            failure.Which.SqliteErrorCode.Should().Be(13);
            reached.Should().BeTrue();
            if (existing) File.ReadAllBytes(database).Should().Equal(before!);
            else File.Exists(database).Should().BeFalse();
            Directory.GetFiles(root, ".aurora-candidate-*").Should().BeEmpty();
            await ContentImport.ImportAsync(root, database);
            using var accepted = new SqliteConnection($"Data Source={database};Pooling=False");
            accepted.Open();
            using var query = accepted.CreateCommand();
            query.CommandText = "PRAGMA integrity_check";
            query.ExecuteScalar().Should().Be("ok");
            query.CommandText = "SELECT name FROM elements WHERE aurora_id='ID_STORAGE_TEST'";
            query.ExecuteScalar().Should().Be("Recovered");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    private static string Book(string name) => $"<elements><element id='ID_STORAGE_TEST' name='{name}' type='Proficiency' source='Test'/></elements>";
    private static string NewDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    private sealed class InlineProgress(Action<ContentImportProgress> action) : IProgress<ContentImportProgress>
    {
        public void Report(ContentImportProgress value) => action(value);
    }
    private sealed class FullStream(string path) : FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            await base.WriteAsync(buffer[..Math.Min(buffer.Length, 12)], token);
            throw new IOException("Synthetic ENOSPC after a partial write.", 28);
        }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Version = 1;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<elements version='{Version}'/>") };
            response.Headers.TryAddWithoutValidation("ETag", $"\"v{Version}\"");
            return Task.FromResult(response);
        }
    }
    private sealed class DownloadFixture : IDisposable
    {
        private readonly bool _existing;
        private readonly Handler _handler = new();
        public string Root { get; } = NewDirectory();
        public HttpClient Client { get; }
        public string Destination => Path.Combine(Root, "source", "book.xml");
        public ContentIndexUpdateRequest Request => new(Root, ["source.index"]);
        public DownloadFixture(bool existing)
        {
            _existing = existing;
            Client = new HttpClient(_handler);
            File.WriteAllText(Path.Combine(Root, "source.index"), "<index><files><file name='book.xml' url='https://example.test/book.xml'/></files></index>");
        }
        public async Task EstablishAsync()
        {
            if (_existing) await new ContentIndexUpdateService(Client).UpdateAsync(Request);
            _handler.Version = 2;
        }
        public byte[]? CacheBytes()
        {
            string? path = Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories).SingleOrDefault();
            return path is null ? null : File.ReadAllBytes(path);
        }
        public void Dispose() { Client.Dispose(); Directory.Delete(Root, true); }
    }
}
