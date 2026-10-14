using System.Reflection;
using System.Text;
using Builder.Presentation.Services.Storage;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class CloudPendingStateCacheTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "cloud-cache-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UnchangedDirtyReadsReuseHashesInsteadOfReadingTheWholeCharacter()
    {
        var store = new Store(new string('x', 2 * 1024 * 1024));
        using var session = await CloudCharacterSession.OpenAsync(store, _root, "account", new("document"));
        session.HasPendingChanges.Should().BeFalse();
        long firstReads = ReadCount(session);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) session.HasPendingChanges.Should().BeFalse();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long repeatedReads = ReadCount(session) - firstReads;
        output.WriteLine($"2 MiB character, 1000 unchanged dirty reads: {repeatedReads} content reads, {allocated} allocated bytes. Previous implementation: 1000 character reads (at least 2 GiB payload allocation).");
        repeatedReads.Should().Be(0);
        allocated.Should().BeLessThan(8 * 1024 * 1024);
    }

    [Fact]
    public async Task SameSizeSameTimestampExternalEditInvalidatesTheCache()
    {
        var store = new Store("before");
        using var session = await CloudCharacterSession.OpenAsync(store, _root, "account", new("document"));
        session.HasPendingChanges.Should().BeFalse();
        DateTime stamp = File.GetLastWriteTimeUtc(session.FilePath);
        string original = File.ReadAllText(session.FilePath);
        File.WriteAllText(session.FilePath, original.Replace("before", "after!"));
        File.SetLastWriteTimeUtc(session.FilePath, stamp);
        for (int i = 0; i < 100 && !session.HasPendingChanges; i++) await Task.Delay(20);
        session.HasPendingChanges.Should().BeTrue();
        await session.SaveAsync();
        session.HasPendingChanges.Should().BeFalse("the new confirmed receipt must replace the old cached result");
    }

    [Fact]
    public async Task ExplicitInvalidationAndExternalDeletionAreObserved()
    {
        var store = new Store("before");
        using var session = await CloudCharacterSession.OpenAsync(store, _root, "account", new("document"));
        session.HasPendingChanges.Should().BeFalse();
        long before = ReadCount(session);
        session.InvalidatePendingState();
        session.HasPendingChanges.Should().BeFalse();
        ReadCount(session).Should().BeGreaterThan(before);
        File.Delete(session.FilePath);
        session.HasPendingChanges.Should().BeTrue();
    }

    private static long ReadCount(CloudCharacterSession session) =>
        (long)typeof(CloudCharacterSession).GetField("_pendingFileReadCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;

    private sealed class Store(string body) : ICharacterDocumentStore
    {
        public string ProviderId => "test";
        private static CharacterDocumentMetadata Metadata => new(new("document"), "character.dnd5e", "1", null, null, null);
        public Task<CharacterDocument> OpenAsync(CharacterDocumentReference reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CharacterDocument(Metadata, Encoding.UTF8.GetBytes($"<character><build><name>{body}</name></build></character>")));
        public Task<CharacterDocumentMetadata> CreateAsync(string fileName, ReadOnlyMemory<byte> content,
            CharacterDocumentReference? parentFolder = null, CancellationToken cancellationToken = default) => Task.FromResult(Metadata);
        public Task<CharacterDocumentMetadata> SaveAsync(CharacterDocumentMetadata expected, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default) => Task.FromResult(Metadata with { ProviderVersion = "2" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
