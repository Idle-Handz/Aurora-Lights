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
    public async Task Feed_registration_is_offline_and_removal_survives_startup()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string previousRoot = settings.DocumentsRootDirectory;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "custom"));
        try
        {
            settings.DocumentsRootDirectory = root;
            var characters = new CharacterService();
            var database = new ContentDatabaseService();
            var handler = new PublishedContentHandler([]);
            using var client = new HttpClient(handler);
            var service = new ContentService(characters, new CharacterTabService(), database,
                new CompendiumService(database, characters), new ContentIndexUpdateService(client));
            service.EnsureReplacementFeedInstalled();
            service.InstalledIndexNames.Should().Contain(ContentService.ReplacementFeedFileName);
            await service.RunStartupContentRefreshAsync(() => false);
            handler.Requests.Should().BeEmpty();
            service.ContentReloadPending.Should().BeFalse();

            service.RemoveIndex(ContentService.ReplacementFeedFileName).Should().BeNull();
            service.EnsureReplacementFeedInstalled();
            service.InstalledIndexNames.Should().NotContain(ContentService.ReplacementFeedFileName);
            handler.Requests.Should().BeEmpty();
        }
        finally
        {
            settings.DocumentsRootDirectory = previousRoot;
            DataManager.Current.InitializeDirectories();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Startup_proposal_download_does_not_request_database_refresh()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string previousRoot = settings.DocumentsRootDirectory;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string custom = Path.Combine(root, "custom");
        Directory.CreateDirectory(custom);
        byte[] index = Encoding.UTF8.GetBytes("<index><files><file name='fix.aurora-correction' url='https://example.test/book.xml'/></files></index>");
        await File.WriteAllBytesAsync(Path.Combine(custom, "source.index"), index);
        var handler = new PublishedContentHandler(index)
        {
            // Invalid proposals are reported for review; they must never be active XML.
            BookXml = "<elements><info><name>Downloaded proposal</name></info></elements>"
        };
        using var client = new HttpClient(handler);
        try
        {
            settings.DocumentsRootDirectory = root;
            var characters = new CharacterService();
            var database = new ContentDatabaseService();
            var service = new ContentService(characters, new CharacterTabService(), database,
                new CompendiumService(database, characters), new ContentIndexUpdateService(client));
            int refreshNotifications = 0;
            service.ContentDownloaded += _ => refreshNotifications++;
            await service.RunStartupContentRefreshAsync(() => true);

            handler.Requests.Should().HaveCount(1);
            File.Exists(Path.Combine(custom, "source", "fix.aurora-correction")).Should().BeTrue();
            service.ContentReloadPending.Should().BeFalse();
            refreshNotifications.Should().Be(0);
            service.StartupContentUpdateStatus.Should().NotContain("Refresh the database");
            service.ReplacementProposals.Should().ContainSingle(p => p.Status == ReplacementProposalStatus.NeedsReview);
        }
        finally
        {
            settings.DocumentsRootDirectory = previousRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.index")]
    [InlineData("..\\outside.index")]
    [InlineData("C:\\outside.index")]
    [InlineData("/outside.index")]
    [InlineData("source.index:stream")]
    [InlineData("character.dnd5e")]
    [InlineData("")]
    public void InstalledSourceNamesCannotEscapeTheContentDirectory(string filename)
    {
        Action resolve = () => ContentService.ResolveInstalledIndexPath(Path.GetTempPath(), filename);
        resolve.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void InstalledSourceNamesPreserveNormalPublisherFilenames()
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        ContentService.ResolveInstalledIndexPath(root, "The Book of Xellarant.index")
            .Should().Be(Path.Combine(root, "The Book of Xellarant.index"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledStartupMakesNoRequestsButManualChecksStillWork(bool enabledWhenQueued)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string originalRoot = settings.DocumentsRootDirectory;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string custom = Path.Combine(root, "custom");
        Directory.CreateDirectory(Path.Combine(custom, "source"));
        byte[] indexBytes = Encoding.UTF8.GetBytes("""
            <index>
              <info><update><file name="source.index" url="https://example.test/source.index" /></update></info>
              <files><file name="book.xml" url="https://example.test/book.xml" /></files>
            </index>
            """);
        var handler = new PublishedContentHandler(indexBytes)
        {
            BookXml = "<elements><info><name>Available update</name></info></elements>"
        };
        using var client = new HttpClient(handler);
        string bookPath = Path.Combine(custom, "source", "book.xml");
        await File.WriteAllBytesAsync(Path.Combine(custom, "source.index"), indexBytes);
        await File.WriteAllTextAsync(bookPath, "<elements />");

        try
        {
            settings.DocumentsRootDirectory = root;
            var characters = new CharacterService();
            var database = new ContentDatabaseService();
            var service = new ContentService(characters, new CharacterTabService(), database,
                new CompendiumService(database, characters), new ContentIndexUpdateService(client));
            int notifications = 0;
            service.ContentDownloaded += _ => notifications++;

            bool enabled = enabledWhenQueued;
            Func<Task> queuedStartup = () => service.RunStartupContentRefreshAsync(() => enabled);
            // A cached proposal scan can run before the network check. The preference
            // may be switched off during that scan, after startup has been queued.
            service.Changed += () =>
            {
                if (service.IsReviewingReplacements) enabled = false;
            };
            await queuedStartup();

            handler.Requests.Should().BeEmpty();
            File.ReadAllText(bookPath).Should().Be("<elements />");
            notifications.Should().Be(0);
            service.ContentReloadPending.Should().BeFalse();
            service.ContentUpdateStartedUtc.Should().BeNull();
            service.StartupAutoDownloadEnabled.Should().BeFalse();
            service.StartupContentUpdateStatus.Should().Be("Skipped: auto-download was disabled.");
            service.LastContentUpdateTrigger.Should().BeNull();

            var manual = await service.CheckForUpdatesAsync();
            manual.Outcome.Should().Be(ContentUpdateOutcome.Updated);
            handler.Requests.Should().HaveCount(2);
            File.ReadAllText(bookPath).Should().Be(handler.BookXml);
            service.ContentReloadPending.Should().BeTrue();
            service.LastContentUpdateTrigger.Should().Be("Manual");
            service.StartupContentUpdateStatus.Should().Be("Skipped: auto-download was disabled.");
            notifications.Should().Be(0, "manual checks must not raise the startup notification");
        }
        finally
        {
            settings.DocumentsRootDirectory = originalRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }

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
                await service.RunStartupContentRefreshAsync(() => true);
                service.ContentUpdatedFileCount.Should().Be(0);
                service.ContentReloadPending.Should().BeFalse();
                notifications.Should().Be(0);
                File.ReadAllBytes(indexPath).Should().Equal(indexBytes);
                File.ReadAllText(bookPath).Should().Be(handler.BookXml);
            }

            // Suppressing a false update must not suppress a real content download.
            handler.BookXml = "<elements><info><name>Updated book</name></info></elements>";
            var changed = NewStartupService();
            await changed.RunStartupContentRefreshAsync(() => true);
            changed.ContentReloadPending.Should().BeTrue();
            changed.StartupAutoDownloadEnabled.Should().BeTrue();
            changed.LastContentUpdateTrigger.Should().Be("Startup");
            notifications.Should().Be(1);
            File.ReadAllText(bookPath).Should().Be(handler.BookXml);
            File.ReadAllBytes(indexPath).Should().Equal(indexBytes);
            handler.Requests.Should().HaveCount(6);
            handler.Requests.Should().OnlyContain(url => url.Scheme == "https");

            changed.ClearContentReloadPending();
            handler.BookXml = "<elements><info><name>Manually checked update</name></info></elements>";
            var manual = await changed.CheckForUpdatesAsync();
            manual.Outcome.Should().Be(ContentUpdateOutcome.Updated);
            changed.ContentReloadPending.Should().BeTrue("manual downloads also need a database refresh");

            // Delay SynchronizationContext.Post callbacks until the check is complete. Progress<T>
            // used to leave these queued, allowing them to overwrite the final status afterwards.
            var queuedContext = new QueuedContext();
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            Task<(ContentUpdateOutcome Outcome, string Message)> check;
            try
            {
                SynchronizationContext.SetSynchronizationContext(queuedContext);
                check = changed.CheckForUpdatesAsync();
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            await check;
            string? completedStatus = changed.ContentUpdateStatus;
            queuedContext.Drain();
            changed.ContentUpdateStatus.Should().Be(completedStatus);
            changed.ContentUpdateProgress.Should().Be(100);
        }
        finally
        {
            settings.DocumentsRootDirectory = originalRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> callbacks = new();
        public override void Post(SendOrPostCallback callback, object? state) => callbacks.Enqueue((callback, state));
        public void Drain()
        {
            while (callbacks.TryDequeue(out var pending)) pending.Callback(pending.State);
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
