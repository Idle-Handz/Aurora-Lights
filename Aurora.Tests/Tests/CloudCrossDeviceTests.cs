using System.Text;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class CloudCrossDeviceTests
{
    [Fact]
    public async Task SeparateDeviceWorkspaces_RoundTripCharacterAndSessionAndPreserveStaleEdits()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-cross-device-" + Guid.NewGuid().ToString("N"));
        try
        {
            var remote = new SharedStore();
            using var desktop = await CloudCharacterSession.OpenAsync(remote, Path.Combine(root, "desktop"), "same-account", remote.Metadata.Reference);
            File.WriteAllText(desktop.FilePath, Character("Desktop edit"));
            File.WriteAllText(desktop.SessionPath, """{"CurrentHp":12,"Slots":2}""");
            await desktop.SaveAsync();

            using var android = await CloudCharacterSession.OpenAsync(remote, Path.Combine(root, "android"), "same-account", remote.Metadata.Reference);
            File.ReadAllText(android.FilePath).Should().Contain("Desktop edit");
            File.ReadAllText(android.SessionPath).Should().Be("""{"CurrentHp":12,"Slots":2}""");
            File.WriteAllText(android.FilePath, Character("Android edit"));
            File.WriteAllText(android.SessionPath, """{"CurrentHp":7,"Slots":1}""");
            await android.SaveAsync();

            // A desktop still holding the previous revision cannot overwrite the Android save.
            File.WriteAllText(desktop.FilePath, Character("Unsent desktop edit"));
            await desktop.Invoking(s => s.SaveAsync()).Should().ThrowAsync<CharacterDocumentConflictException>();
            await desktop.ReloadAuthoritativeAsync();
            File.ReadAllText(desktop.FilePath).Should().Contain("Android edit");
            File.ReadAllText(desktop.SessionPath).Should().Be("""{"CurrentHp":7,"Slots":1}""");
            File.ReadAllText(desktop.RecoveryPath!).Should().Contain("Unsent desktop edit");

            File.WriteAllText(desktop.FilePath, Character("Back to desktop"));
            await desktop.SaveAsync();
            await android.ReloadAuthoritativeAsync();
            File.ReadAllText(android.FilePath).Should().Contain("Back to desktop");
            android.HasPendingChanges.Should().BeFalse();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static string Character(string name) => $"<character><build><name>{name}</name></build></character>";

    private sealed class SharedStore : ICharacterDocumentStore
    {
        private int _version = 1;
        private byte[] _content = Encoding.UTF8.GetBytes(Character("Original"));
        public string ProviderId => "google-drive";
        public CharacterDocumentMetadata Metadata => new(new("shared-file"), "Character.dnd5e", _version.ToString(), null, null, null);
        public Task<CharacterDocument> OpenAsync(CharacterDocumentReference reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CharacterDocument(Metadata, _content.ToArray()));
        public Task<CharacterDocumentMetadata> CreateAsync(string fileName, ReadOnlyMemory<byte> content,
            CharacterDocumentReference? parentFolder = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterDocumentMetadata> SaveAsync(CharacterDocumentMetadata expected, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (expected.ProviderVersion != Metadata.ProviderVersion)
                throw new CharacterDocumentConflictException(expected, Metadata);
            _content = content.ToArray();
            _version++;
            return Task.FromResult(Metadata);
        }
    }
}
