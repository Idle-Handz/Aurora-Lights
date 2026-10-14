using Aurora.PdfImport;

namespace Aurora.Tests.Tests;

public sealed class StagedImportPortraitTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aurora-portrait-tests-{Guid.NewGuid():N}");

    public StagedImportPortraitTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    public async Task SameNamePreviewsAndCancellationPreserveExistingBytes(string extension)
    {
        string existing = Path.Combine(_directory, "Hero" + extension);
        await File.WriteAllBytesAsync(existing, [1, 2, 3]);
        using var first = await StagedImportPortrait.CreateAsync([4, 5], extension);
        using var second = await StagedImportPortrait.CreateAsync([6, 7], extension);
        first.Path.Should().NotBe(second.Path);
        string pending = first.CopyForCommit(_directory, "Hero");
        first.Dispose();
        File.Exists(first.Path).Should().BeFalse();
        File.Exists(pending).Should().BeFalse();
        File.ReadAllBytes(existing).Should().Equal(1, 2, 3);
        File.ReadAllBytes(second.Path).Should().Equal(6, 7);
    }

    [Fact]
    public async Task FailedCommitRemovesOnlyItsOwnCopyAndCanRetry()
    {
        using var portrait = await StagedImportPortrait.CreateAsync([8, 9], ".png");
        string failed = portrait.CopyForCommit(_directory, "Hero");
        portrait.RollbackCommit();
        File.Exists(failed).Should().BeFalse();
        File.Exists(portrait.Path).Should().BeTrue();
        string accepted = portrait.CopyForCommit(_directory, "Hero");
        portrait.AcceptCommit();
        portrait.Dispose();
        File.ReadAllBytes(accepted).Should().Equal(8, 9);
        File.Exists(portrait.Path).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentSameNameCommitsHaveIndependentDestinations()
    {
        using var first = await StagedImportPortrait.CreateAsync([1], ".png");
        using var second = await StagedImportPortrait.CreateAsync([2], ".png");
        var destinations = await Task.WhenAll(
            Task.Run(() => first.CopyForCommit(_directory, "Hero")),
            Task.Run(() => second.CopyForCommit(_directory, "Hero")));
        destinations[0].Should().NotBe(destinations[1]);
        first.AcceptCommit();
        second.RollbackCommit();
        File.ReadAllBytes(destinations[0]).Should().Equal(1);
        File.Exists(destinations[1]).Should().BeFalse();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
