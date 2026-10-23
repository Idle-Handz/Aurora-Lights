using System.Net;
using System.Text;
using Aurora.Content;
using Builder.Presentation.Services.Content;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentDownloadRecoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Interrupted_body_preserves_saved_file_and_validators_then_retries(bool existingFile, bool cancel)
    {
        string root = NewDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "source.index"),
                "<index><files><file name='book.xml' url='https://example.test/book.xml'/></files></index>");
            string destination = Path.Combine(root, "source", "book.xml");
            var handler = new InterruptibleHandler();
            using var client = new HttpClient(handler);
            var updater = new ContentIndexUpdateService(client);
            if (existingFile) await updater.UpdateAsync(new(root, ["source.index"]));
            byte[]? original = existingFile ? File.ReadAllBytes(destination) : null;
            string? cacheFile = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).SingleOrDefault();
            byte[]? cache = cacheFile is null ? null : File.ReadAllBytes(cacheFile);

            handler.Interrupt = true;
            handler.Cancel = cancel;
            using var cancellation = new CancellationTokenSource();
            Task<ContentIndexUpdateResult> interrupted = updater.UpdateAsync(
                new(root, ["source.index"]), cancellationToken: cancellation.Token);
            await handler.BodyStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                cancellation.Cancel();
                await FluentActions.Awaiting(() => interrupted).Should().ThrowAsync<OperationCanceledException>();
            }
            else
            {
                var result = await interrupted;
                result.FailedFileCount.Should().Be(1);
                result.Updated.Should().BeFalse();
            }
            if (existingFile) File.ReadAllBytes(destination).Should().Equal(original!);
            else File.Exists(destination).Should().BeFalse();
            if (cacheFile is not null) File.ReadAllBytes(cacheFile).Should().Equal(cache!);
            else Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Should().BeEmpty();
            Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories).Should().BeEmpty();

            handler.Interrupt = false;
            handler.Version = 2;
            // A new updater represents a restarted app; the on-disk cache is the only continuity.
            var retry = await new ContentIndexUpdateService(client).UpdateAsync(new(root, ["source.index"]));
            retry.UpdatedContentFileCount.Should().Be(1);
            retry.FailedFileCount.Should().Be(0);
            File.ReadAllText(destination).Should().Contain("version='2'");
            handler.LastValidator.Should().Be(existingFile ? "\"v1\"" : null);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(ContentImportPhase.Preparing)]
    [InlineData(ContentImportPhase.Writing)]
    [InlineData(ContentImportPhase.Activating)]
    public async Task Cancelled_database_refresh_keeps_accepted_catalog_and_can_retry(ContentImportPhase phase)
    {
        string root = NewDirectory();
        try
        {
            string source = Path.Combine(root, "book.xml");
            string database = Path.Combine(root, "content.sqlite");
            File.WriteAllText(source, Book("Accepted"));
            await ContentImport.ImportAsync(root, database);
            byte[] original = File.ReadAllBytes(database);
            File.WriteAllText(source, Book("Recovered"));
            using var cancellation = new CancellationTokenSource();
            bool reached = false;
            var progress = new InlineProgress(value =>
            {
                if (value.Phase == phase) { reached = true; cancellation.Cancel(); }
            });
            await FluentActions.Awaiting(() => ContentImport.ImportAsync(root, database, progress, cancellation.Token))
                .Should().ThrowAsync<OperationCanceledException>();
            reached.Should().BeTrue();
            File.ReadAllBytes(database).Should().Equal(original);
            ReadName(database).Should().Be("Accepted");
            Directory.GetFiles(root, ".aurora-candidate-*").Should().BeEmpty();

            await ContentImport.ImportAsync(root, database);
            ReadName(database).Should().Be("Recovered");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, recursive: true); }
    }

    private static string NewDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    private static string Book(string name) =>
        $"<elements><element id='ID_TEST_DOWNLOAD_RECOVERY' name='{name}' type='Proficiency' source='Test'/></elements>";
    private static string ReadName(string database)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM elements WHERE aurora_id='ID_TEST_DOWNLOAD_RECOVERY'";
        return (string)command.ExecuteScalar()!;
    }
    private sealed class InlineProgress(Action<ContentImportProgress> report) : IProgress<ContentImportProgress>
    {
        public void Report(ContentImportProgress value) => report(value);
    }
    private sealed class InterruptibleHandler : HttpMessageHandler
    {
        public bool Interrupt { get; set; }
        public bool Cancel { get; set; }
        public int Version { get; set; } = 1;
        public string? LastValidator { get; private set; }
        public TaskCompletionSource BodyStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            LastValidator = request.Headers.IfNoneMatch.SingleOrDefault()?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Interrupt ? new InterruptedContent(BodyStarted, Cancel)
                    : new StringContent($"<elements version='{Version}'/>")
            };
            response.Headers.TryAddWithoutValidation("ETag", Interrupt ? "\"interrupted\"" : $"\"v{Version}\"");
            return Task.FromResult(response);
        }
    }
    private sealed class InterruptedContent(TaskCompletionSource started, bool cancel) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("<elements><element"), token);
            started.TrySetResult();
            if (cancel) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            else throw new HttpRequestException("Synthetic connection ended during the body.");
        }
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
    }
}
