using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class ContentDirectoryResolverTests
{
    [Fact]
    public void FileSystemRootIsPreservedWhenNormalizingContentDirectories()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        string fileSystemRoot = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        try
        {
            primaryProperty.SetValue(manager, fileSystemRoot);
            additional.Clear();
            additional.Add(fileSystemRoot);

            ContentDirectoryResolver.GetContentDirectories().Should().Equal(new[] { fileSystemRoot },
                "trimming the separator off a drive root changes it into a drive-relative path");
        }
        finally
        {
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
        }
    }
}
