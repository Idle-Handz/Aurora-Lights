using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public class ContentImportProgressMappingTests
{
    private static ContentImportProgress At(ContentImportPhase phase, int completed, int total, int changed = 0, int written = 0)
        => new(phase, completed, total, changed, written, null);

    private static int Percent(AuroraImportProgress p) => p.Percentage;

    [Theory]
    [InlineData(ContentImportPhase.Preparing, 0, 1189, AuroraImportPhase.Scanning, 0)]
    [InlineData(ContentImportPhase.Preparing, 1189, 1189, AuroraImportPhase.Scanning, 25)]
    [InlineData(ContentImportPhase.Reading, 0, 1189, AuroraImportPhase.Scanning, 25)]
    [InlineData(ContentImportPhase.Reading, 1189, 1189, AuroraImportPhase.Scanning, 50)]
    [InlineData(ContentImportPhase.Comparing, 0, 1189, AuroraImportPhase.Importing, 50)]
    [InlineData(ContentImportPhase.Comparing, 1189, 1189, AuroraImportPhase.Importing, 60)]
    [InlineData(ContentImportPhase.Writing, 10000, 20000, AuroraImportPhase.Importing, 75)]
    [InlineData(ContentImportPhase.Writing, 20000, 20000, AuroraImportPhase.Importing, 90)]
    [InlineData(ContentImportPhase.Resolving, 0, 0, AuroraImportPhase.Resolving, 90)]
    [InlineData(ContentImportPhase.Activating, 0, 0, AuroraImportPhase.Resolving, 90)]
    [InlineData(ContentImportPhase.Complete, 0, 0, AuroraImportPhase.Complete, 100)]
    public void Phases_map_onto_the_settings_progress_bar(ContentImportPhase phase, int completed, int total,
        AuroraImportPhase expectedPhase, int expectedPercent)
    {
        var mapped = ContentDatabaseService.MapProgress(At(phase, completed, total));

        mapped.Phase.Should().Be(expectedPhase);
        mapped.FilesTotal.Should().BePositive("a zero total would make the Settings bar indeterminate");
        Percent(mapped).Should().Be(expectedPercent);
    }

    [Fact]
    public void Progress_never_moves_backwards_across_phases()
    {
        var sequence = new[]
        {
            At(ContentImportPhase.Preparing, 0, 100), At(ContentImportPhase.Preparing, 100, 100),
            At(ContentImportPhase.Reading, 50, 100), At(ContentImportPhase.Comparing, 100, 100),
            At(ContentImportPhase.Writing, 0, 500), At(ContentImportPhase.Writing, 500, 500),
            At(ContentImportPhase.Resolving, 0, 0), At(ContentImportPhase.Activating, 0, 0),
            At(ContentImportPhase.Complete, 0, 0),
        };

        var percents = sequence.Select(p => Percent(ContentDatabaseService.MapProgress(p))).ToList();

        percents.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Writing_with_nothing_to_write_counts_as_its_full_share()
    {
        Percent(ContentDatabaseService.MapProgress(At(ContentImportPhase.Writing, 0, 0))).Should().Be(90);
    }

    [Fact]
    public void Changed_file_and_element_counts_pass_through_for_the_importing_label()
    {
        var mapped = ContentDatabaseService.MapProgress(At(ContentImportPhase.Comparing, 40, 100, changed: 3, written: 0));

        mapped.FilesChanged.Should().Be(3);
        mapped.PhaseLabel.Should().Be("Comparing content files (40 / 100 files)…");
    }

    [Theory]
    [InlineData(ContentImportPhase.Preparing, "Scanning content files (10 / 100 files)…", false)]
    [InlineData(ContentImportPhase.Reading, "Reading content files (10 / 100 files)…", false)]
    [InlineData(ContentImportPhase.Comparing, "Comparing content files (10 / 100 files)…", false)]
    [InlineData(ContentImportPhase.Writing, "Writing content to database (10 / 100 elements)…", false)]
    [InlineData(ContentImportPhase.Resolving, "Resolving relationships…", true)]
    [InlineData(ContentImportPhase.Activating, "Validating and activating database…", true)]
    public void EachPhaseShowsItsOwnWorkAndUnknownTotalsUseAnimation(ContentImportPhase phase, string expectedLabel, bool indeterminate)
    {
        var mapped = ContentDatabaseService.MapProgress(At(phase, 10, 100));
        mapped.PhaseLabel.Should().Be(expectedLabel);
        mapped.IsIndeterminate.Should().Be(indeterminate);
    }

    [Fact]
    public void WritingShowsActualCountsAndCurrentFileEvenWhenRoundedPercentageDoesNotMove()
    {
        var first = ContentDatabaseService.MapProgress(new(ContentImportPhase.Writing, 100, 20000, 2, 100, "core/feats.xml"));
        var next = ContentDatabaseService.MapProgress(new(ContentImportPhase.Writing, 110, 20000, 2, 110, "core/spells.xml"));

        next.Percentage.Should().Be(first.Percentage);
        next.PhaseLabel.Should().NotBe(first.PhaseLabel);
        next.CurrentFile.Should().Be("core/spells.xml");
        next.ElementsImported.Should().Be(110);
        ContentDatabaseService.MapProgress(At(ContentImportPhase.Writing, 0, 0)).IsIndeterminate.Should().BeTrue();
    }

    [Fact]
    public async Task BundledLibraryPublishesIntermediateWritesThroughSyncService()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var contentPath = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        var originalPath = contentPath.GetValue(DataManager.Current);
        string workspace = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "import-progress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            contentPath.SetValue(DataManager.Current, workspace);
            // Exercise the pinned package, not fabricated progress callbacks. Enough actual writes
            // to cross its reporting throttle while remaining a small, isolated database.
            var elements = Enumerable.Range(0, 2000).Select(i =>
                $"<element name='Progress {i}' type='Proficiency' source='Progress test' id='ID_PROGRESS_{i}'><description>Progress test</description></element>");
            File.WriteAllText(Path.Combine(workspace, "progress.xml"), "<elements>" + string.Concat(elements) + "</elements>");
            var service = new ContentDatabaseService();
            var reports = new List<AuroraImportProgress>();
            service.StateChanged += () =>
            {
                if (service.SyncState == ContentDatabaseSyncState.Syncing && service.Progress is { } progress)
                    reports.Add(progress);
            };
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var result = await service.SyncAsync(cancellation.Token);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.ElementsImported.Should().Be(2000);
            reports.Should().Contain(p => p.Phase == AuroraImportPhase.Importing && p.ElementsImported > 0 && p.ElementsImported < 2000,
                "Settings needs real intermediate writes, not just phase boundaries");
            var writing = reports.Where(p => p.PhaseLabel.StartsWith("Writing content", StringComparison.Ordinal)).ToArray();
            writing.Select(p => p.Percentage).Distinct().Count().Should().BeGreaterThan(1);
            writing.Select(p => p.PhaseLabel).Distinct().Count().Should().BeGreaterThan(1);
            writing.Should().OnlyContain(p => p.CurrentFile == "progress.xml");
            reports[^1].Phase.Should().Be(AuroraImportPhase.Complete);
            service.SyncState.Should().Be(ContentDatabaseSyncState.Done);
        }
        finally
        {
            contentPath.SetValue(DataManager.Current, originalPath);
            Directory.Delete(workspace, recursive: true);
        }
    }
}
