using System.Text;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Maui.Storage;

namespace Aurora.Tests.Tests;

public sealed class CharacterImportCollisionTests
{
    [Fact]
    public async Task RepeatedImportsNeverOverwriteAnExistingCharacter()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string originalRoot = settings.DocumentsRootDirectory;
        var originalPicker = FilePicker.Default.Pick;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const string existing = "<character name=\"Existing\" />";
        const string imported = "<character name=\"Imported\" />";
        try
        {
            settings.DocumentsRootDirectory = root;
            DataManager.Current.InitializeDirectories();
            string characterDirectory = DataManager.Current.UserDocumentsRootDirectory;
            File.WriteAllText(Path.Combine(characterDirectory, "Hero.dnd5e"), existing);
            File.WriteAllText(Path.Combine(characterDirectory, "Hero_2.dnd5e"), existing);
            // Cover the timestamp fallback used by prior releases, including a second boundary.
            var now = DateTime.Now;
            for (int seconds = -1; seconds <= 60; seconds++)
                File.WriteAllText(Path.Combine(characterDirectory, $"Hero_{now.AddSeconds(seconds):yyyyMMdd-HHmmss}.dnd5e"), existing);
            var originals = Directory.GetFiles(characterDirectory, "*.dnd5e").ToHashSet(StringComparer.OrdinalIgnoreCase);
            FilePicker.Default.Pick = _ => Task.FromResult<FileResult?>(new FileResult
            {
                FileName = "Hero.dnd5e",
                OpenRead = () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(imported))),
            });

            var service = new CharacterService();
            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.ImportCharacterFromFileAsync()));
            results.Should().OnlyContain(result => result.Error == null && result.File != null);
            var paths = results.Select(result => result.File!.FilePath).ToArray();
            paths.Should().OnlyHaveUniqueItems();
            paths.Should().NotIntersectWith(originals, "even a repeated timestamp collision must choose another file");
            foreach (string path in originals)
                File.ReadAllText(path).Should().Be(existing);
            foreach (string path in paths)
                File.ReadAllText(path).Should().Be(imported);

            FilePicker.Default.Pick = _ => Task.FromResult<FileResult?>(new FileResult
            {
                FileName = "Broken.dnd5e",
                OpenRead = () => Task.FromResult<Stream>(new FailingImportStream()),
            });
            var failed = await service.ImportCharacterFromFileAsync();
            failed.File.Should().BeNull();
            failed.Error.Should().Contain("simulated read failure");
            Directory.GetFiles(characterDirectory, "*.dnd5e").Should().BeEquivalentTo(originals.Concat(paths),
                "a failed copy must not leave a partial character behind");
        }
        finally
        {
            FilePicker.Default.Pick = originalPicker;
            settings.DocumentsRootDirectory = originalRoot;
            DataManager.Current.InitializeDirectories();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FailingImportStream : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            throw new IOException("simulated read failure");
        }
    }
}
