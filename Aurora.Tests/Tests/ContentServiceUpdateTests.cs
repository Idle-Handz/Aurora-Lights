using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Content;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class ContentServiceUpdateTests
{
    [Fact]
    public async Task StartupChecksPreservePublishedIndexesAndOnlyNotifyForRealChanges()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string originalRoot = settings.DocumentsRootDirectory;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string custom = Path.Combine(root, "custom");
        Directory.CreateDirectory(Path.Combine(custom, "source"));
        byte[] indexBytes = Encoding.UTF8.GetBytes("""
            <index>
              <info>
                <author url="http://publisher.test">Publisher</author>
                <update><file name="source.index" url="http://example.test/source.index" /></update>
              </info>
              <files><file name="book.xml" url="http://example.test/book.xml" /></files>
            </index>
            """);
        var handler = new PublishedContentHandler(indexBytes);
        using var client = new HttpClient(handler);
        var updater = new ContentIndexUpdateService(client);
        string indexPath = Path.Combine(custom, "source.index");
        string bookPath = Path.Combine(custom, "source", "book.xml");
        await File.WriteAllBytesAsync(indexPath, indexBytes);
        await File.WriteAllTextAsync(bookPath, handler.BookXml);

        try
        {
            settings.DocumentsRootDirectory = root;
            var characters = new CharacterService();
            var tabs = new CharacterTabService();
            var database = new ContentDatabaseService();
            var compendium = new CompendiumService(database, characters);
            int notifications = 0;
            ContentService NewStartupService()
            {
                var service = new ContentService(characters, tabs, database, compendium, updater);
                service.ContentDownloaded += _ => notifications++;
                return service;
            }

            // New service instances simulate reopening the app, including its startup
            // notification path. The server returns the unchanged publisher bytes each time.
            for (int launch = 0; launch < 2; launch++)
            {
                var service = NewStartupService();
                await service.RunStartupContentRefreshAsync();
                service.ContentUpdatedFileCount.Should().Be(0);
                service.ContentReloadPending.Should().BeFalse();
                notifications.Should().Be(0);
                File.ReadAllBytes(indexPath).Should().Equal(indexBytes);
                File.ReadAllText(bookPath).Should().Be(handler.BookXml);
            }

            // Suppressing a false update must not suppress a real content download.
            handler.BookXml = "<elements><info><name>Updated book</name></info></elements>";
            var changed = NewStartupService();
            await changed.RunStartupContentRefreshAsync();
            changed.ContentReloadPending.Should().BeTrue();
            notifications.Should().Be(1);
            File.ReadAllText(bookPath).Should().Be(handler.BookXml);
            File.ReadAllBytes(indexPath).Should().Equal(indexBytes);
            handler.Requests.Should().HaveCount(6);
            handler.Requests.Should().OnlyContain(url => url.Scheme == "https");
        }
        finally
        {
            settings.DocumentsRootDirectory = originalRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PublishedContentHandler(byte[] indexBytes) : HttpMessageHandler
    {
        public string BookXml { get; set; } = "<elements />";
        public ConcurrentQueue<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri url = request.RequestUri!;
            Requests.Enqueue(url);
            byte[] bytes = url.AbsoluteUri switch
            {
                "https://example.test/source.index" => indexBytes,
                "https://example.test/book.xml" => Encoding.UTF8.GetBytes(BookXml),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        }
    }
}
