using Aurora.App.Services;
using Builder.Presentation.Models;

namespace Aurora.Tests.Tests;

public sealed class CharacterFileWriteCoordinatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"aurora_write_gate_{Guid.NewGuid():N}");

    public CharacterFileWriteCoordinatorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    [Fact]
    public async Task WriteAsync_SerializesConcurrentWriters()
    {
        var gate = new SemaphoreSlim(1, 1);
        var file = CreateTrackedFile("serialized.dnd5e");
        int activeWriters = 0;
        int maxActiveWriters = 0;

        var writes = Enumerable.Range(0, 6)
            .Select(_ => CharacterFileWriteCoordinator.WriteAsync(gate, file, "Test write", () =>
            {
                int active = Interlocked.Increment(ref activeWriters);
                maxActiveWriters = Math.Max(maxActiveWriters, active);
                Thread.Sleep(25);
                Interlocked.Decrement(ref activeWriters);
                return true;
            }));

        CharacterFileWriteResult[] results = await Task.WhenAll(writes);

        results.Should().OnlyContain(result => result.Succeeded);
        maxActiveWriters.Should().Be(1, "the file write gate must serialize read-modify-write callbacks");
    }

    [Fact]
    public async Task WriteAsync_ReturnsExternalChangeWithoutCallingWriter_WhenFileChangedAfterStamp()
    {
        var gate = new SemaphoreSlim(1, 1);
        var file = CreateTrackedFile("stale.dnd5e");
        File.AppendAllText(file.FilePath, " ");
        bool writerCalled = false;

        CharacterFileWriteResult result = await CharacterFileWriteCoordinator.WriteAsync(
            gate,
            file,
            "Stale write",
            () =>
            {
                writerCalled = true;
                return true;
            });

        result.Status.Should().Be(CharacterFileWriteStatus.ExternalChange);
        writerCalled.Should().BeFalse("stale files should be reloaded or reviewed instead of overwritten");
    }

    [Fact]
    public async Task WriteAsync_ReleasesGateAfterFailedWriter()
    {
        var gate = new SemaphoreSlim(1, 1);
        var file = CreateTrackedFile("failed.dnd5e");

        CharacterFileWriteResult failed = await CharacterFileWriteCoordinator.WriteAsync(
            gate,
            file,
            "Failing write",
            () => throw new InvalidOperationException("simulated"));

        CharacterFileWriteResult next = await CharacterFileWriteCoordinator.WriteAsync(
            gate,
            file,
            "Next write",
            () => true);

        failed.Status.Should().Be(CharacterFileWriteStatus.Failed);
        next.Succeeded.Should().BeTrue("a failed write must not leave the gate permanently held");
    }

    private CharacterFile CreateTrackedFile(string fileName)
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, "<character><build /></character>");
        var file = new CharacterFile(path);
        file.RefreshKnownDiskStamp();
        return file;
    }
}
