using System.Net;
using Aurora.Reflections.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Content;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class ContentDownloadPendingRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_retains_refresh_reminder_even_without_new_downloads(bool startupDownload)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string previousRoot = settings.DocumentsRootDirectory;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "custom"));
        File.WriteAllText(Path.Combine(root, "custom", "source.index"),
            "<index><files><file name='book.xml' url='https://example.test/book.xml'/></files></index>");
        using var client = new HttpClient(new CachedBookHandler());
        try
        {
            settings.DocumentsRootDirectory = root;
            ContentService NewService()
            {
                var characters = new CharacterService();
                var db = new ContentDatabaseService();
                return new ContentService(characters, new CharacterTabService(), db,
                    new CompendiumService(db, characters), new ContentIndexUpdateService(client));
            }
            var first = NewService();
            (await first.CheckForUpdatesAsync()).Outcome.Should().Be(ContentUpdateOutcome.Updated);
            first.ContentReloadPending.Should().BeTrue();

            var reopened = NewService();
            int notifications = 0;
            reopened.ContentDownloaded += _ => notifications++;
            await reopened.RunStartupContentRefreshAsync(() => startupDownload);
            reopened.ContentReloadPending.Should().BeTrue("downloaded files still need activation after a restart");
            reopened.ContentUpdatedFileCount.Should().Be(0);
            notifications.Should().Be(1);

            reopened.ClearContentReloadPending();
            var acknowledged = NewService();
            await acknowledged.RunStartupContentRefreshAsync(() => false);
            acknowledged.ContentReloadPending.Should().BeFalse();
        }
        finally
        {
            settings.DocumentsRootDirectory = previousRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }
    private sealed class CachedBookHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Headers.IfNoneMatch.Any())
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<elements><element id='ID_TEST_PENDING_DOWNLOAD' name='Pending download' type='Proficiency' source='Test'/></elements>")
            };
            response.Headers.TryAddWithoutValidation("ETag", "\"v1\"");
            return Task.FromResult(response);
        }
    }
}
