using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class ContentImportWorkspaceTests
{
    [Fact]
    public void Cleanup_removes_only_exact_candidates_and_sidecars_and_is_repeatable()
    {
        using var directory = new TestDirectory();
        string prefix = ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite";
        string[] removed = [prefix, prefix + "-journal", prefix + "-wal", prefix + "-shm",
            ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite-journal"];
        string[] kept = ["aurora-elements.sqlite", "aurora-elements.sqlite-journal", "character.dnd5e",
            "correction.xml.retired-abc", ".aurora-candidate-not-a-guid.sqlite", prefix + ".backup"];
        foreach (string name in removed.Concat(kept)) File.WriteAllText(Path.Combine(directory.Path, name), "safe");
        string nested = System.IO.Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(System.IO.Path.Combine(nested, prefix), "safe");
        using var workspace = new ContentImportWorkspace(directory.Path);

        workspace.CleanCandidates().Should().Be((removed.Length, removed.Length * 4L));
        workspace.CleanCandidates().Should().Be((0, 0L));
        foreach (string name in kept) File.ReadAllText(Path.Combine(directory.Path, name)).Should().Be("safe");
        File.ReadAllText(Path.Combine(nested, prefix)).Should().Be("safe");
    }

    [Fact]
    public void Another_workspace_cannot_clean_an_active_refresh_and_can_retry_after_release()
    {
        using var directory = new TestDirectory();
        string candidate = Path.Combine(directory.Path, ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.WriteAllText(candidate, "active");
        using (var active = new ContentImportWorkspace(directory.Path))
        {
            FluentActions.Invoking(() => new ContentImportWorkspace(directory.Path)).Should().Throw<IOException>();
            File.ReadAllText(candidate).Should().Be("active");
        }
        using var retry = new ContentImportWorkspace(directory.Path);
        retry.CleanCandidates().Should().Be((1, 6L));
    }

    [Fact]
    public void Locked_candidate_preserves_its_entire_group_but_does_not_block_other_cleanup()
    {
        using var directory = new TestDirectory();
        string candidate = Path.Combine(directory.Path, ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite");
        string other = Path.Combine(directory.Path, ".aurora-candidate-" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.WriteAllText(candidate + "-journal", "journal"); // Enumerate the journal first.
        File.WriteAllText(candidate, "active");
        File.WriteAllText(other, "abandoned");
        using var workspace = new ContentImportWorkspace(directory.Path);
        using (var handle = new FileStream(candidate, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failures = new List<string>();
            workspace.CleanCandidates((path, _) => failures.Add(path)).Should().Be((1, 9L));
            failures.Should().HaveCount(1);
            File.ReadAllText(candidate + "-journal").Should().Be("journal");
        }
        workspace.CleanCandidates().Should().Be((2, 13L));
    }

    [Fact]
    public void Preparation_cleanup_removes_only_exact_temporary_trees()
    {
        using var directory = new TestDirectory();
        string cache = Path.Combine(directory.Path, "cache"), root = Path.Combine(directory.Path, "content");
        string staged = Path.Combine(cache, "translator-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(staged, "0", "custom"));
        File.WriteAllText(Path.Combine(staged, "0", "custom", "copy.xml"), "copy");
        string kept = Path.Combine(cache, "translator-preparation-personal");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "draft.dnd5e"), "safe");
        using var workspace = new ContentImportWorkspace(root, cache);
        workspace.CleanPreparationDirectories().Should().Be((1, 4L));
        workspace.CleanPreparationDirectories().Should().Be((0, 0L));
        File.ReadAllText(Path.Combine(kept, "draft.dnd5e")).Should().Be("safe");
    }

    [Fact]
    public void Shared_preparation_lock_protects_imports_even_when_content_roots_differ()
    {
        using var directory = new TestDirectory();
        string cache = Path.Combine(directory.Path, "cache");
        string staged = Path.Combine(cache, "translator-preparation-" + Guid.NewGuid().ToString("N"));
        using (var active = new ContentImportWorkspace(Path.Combine(directory.Path, "first"), cache))
        {
            Directory.CreateDirectory(staged);
            File.WriteAllText(Path.Combine(staged, "active.xml"), "copy");
            FluentActions.Invoking(() => new ContentImportWorkspace(Path.Combine(directory.Path, "second"), cache))
                .Should().Throw<IOException>();
            File.ReadAllText(Path.Combine(staged, "active.xml")).Should().Be("copy");
        }
        using var retry = new ContentImportWorkspace(Path.Combine(directory.Path, "second"), cache);
        retry.CleanPreparationDirectories().Should().Be((1, 4L));
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
