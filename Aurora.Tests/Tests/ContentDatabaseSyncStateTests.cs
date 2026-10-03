using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseSyncStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableContentDirectoryReportsFailedSyncState(bool missingDirectory)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        try
        {
            primaryProperty.SetValue(manager, missingDirectory
                ? Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"))
                : string.Empty);
            additional.Clear();
            var service = new ContentDatabaseService();

            var result = await service.SyncAsync();

            result.Success.Should().BeFalse();
            service.SyncState.Should().Be(ContentDatabaseSyncState.Failed);
            service.LastResult.Should().BeSameAs(result);
        }
        finally
        {
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
        }
    }
}
