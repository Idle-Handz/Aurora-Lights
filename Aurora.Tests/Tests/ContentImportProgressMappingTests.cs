using Aurora.App.Services;
using Aurora.Content;

namespace Aurora.Tests.Tests;

public class ContentImportProgressMappingTests
{
    private static ContentImportProgress At(ContentImportPhase phase, int completed, int total, int changed = 0, int written = 0)
        => new(phase, completed, total, changed, written, null);

    // Mirrors Settings.razor: Scanning is 0-50%, Importing 50-90%, Resolving 90%.
    private static int Percent(AuroraImportProgress p) => p.Phase switch
    {
        AuroraImportPhase.Scanning => (int)(p.FilesScanned * 50.0 / p.FilesTotal),
        AuroraImportPhase.Importing => 50 + (int)(p.FilesScanned * 40.0 / Math.Max(1, p.FilesTotal)),
        AuroraImportPhase.Resolving => 90,
        _ => 100,
    };

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
        mapped.PhaseLabel.Should().Be("Importing content (3 files changed)…");
    }
}
