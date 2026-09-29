using Aurora.App.Services;
using LocalCorrection = Aurora.Content.Contracts.LocalCorrection;
using LocalCorrectionDocument = Aurora.Content.Contracts.LocalCorrectionDocument;

namespace Aurora.Tests.Tests;

/// <summary>
/// The page exists so a reader can tell a correction that is still doing work from one upstream
/// has since adopted, and clear the latter. Getting that distinction wrong either hides a redundant
/// correction forever or offers to throw away one that is the only thing supplying a fix.
/// </summary>
public sealed class ContentDoctorServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-doctor-" + Guid.NewGuid().ToString("N"));

    private string Upstream => Path.Combine(root, "core", "features.xml");
    private string Override => Path.Combine(root, "user", "local", "fix.xml");

    private const string Baseline =
        "<elements>" +
        "<element name='Old' type='Item' source='Test' id='ID_FIX'><description>old</description></element>" +
        "<element name='Companion' type='Item' source='Test' id='ID_COMPANION'><description>companion</description></element>" +
        "</elements>";

    private static string Corrected => Baseline
        .Replace("name='Old'", "name='Fixed'")
        .Replace(">old</description>", ">corrected</description>");

    public ContentDoctorServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Upstream)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Override)!);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private void WriteOverride(string state = "review-pending") =>
        File.WriteAllText(Override, LocalCorrectionDocument.Create(
            Corrected, Baseline, "core/features.xml",
            [new LocalCorrection("fix", "replace", "ID_FIX", null, null, state)]));

    [Fact]
    public void ACorrectionUpstreamHasNotAdoptedIsStillNeeded()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().BeNull();
        file.Corrections.Should().ContainSingle();
        file.Corrections[0].Incorporated.Should().BeFalse("upstream still carries the uncorrected element");
        file.Corrections[0].Summary.Should().Be("Still needed");
        file.IncorporatedCount.Should().Be(0);
        file.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void ACorrectionUpstreamHasAdoptedIsOfferedForClearing()
    {
        // Upstream now ships the same fix the correction was making.
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Corrections[0].Incorporated.Should().BeTrue();
        file.Corrections[0].Summary.Should().Be("Upstream has adopted this");
        file.IncorporatedCount.Should().Be(1);
        file.Status.Should().Be("1 ready to clear");
    }

    /// <summary>
    /// The whole point of clearing: once nothing is left doing work, the file can retire itself on
    /// the next content refresh rather than sitting there forever.
    /// </summary>
    [Fact]
    public void ClearingTheLastCorrectionLeavesTheFileReadyToRetire()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        var service = new ContentDoctorService(new ContentDatabaseService());

        OverrideFileModel before = ContentDoctorService.Evaluate(Override);
        service.AcceptCorrection(before, before.Corrections[0]);

        OverrideFileModel after = ContentDoctorService.Evaluate(Override);
        after.Corrections[0].Accepted.Should().BeTrue();
        after.Corrections[0].Summary.Should().Be("Accepted upstream");
        after.IncorporatedCount.Should().Be(0);
        after.CanRetire.Should().BeTrue();
        after.Status.Should().Be("Ready to retire");
    }

    /// <summary>
    /// The hashes carried on the model are the state the reader was shown. If either file moved
    /// since, the write has to be refused rather than applied to something they did not look at.
    /// </summary>
    [Fact]
    public void ClearingIsRefusedWhenTheFileChangedSinceItWasRead()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        var service = new ContentDoctorService(new ContentDatabaseService());
        OverrideFileModel stale = ContentDoctorService.Evaluate(Override);

        File.WriteAllText(Upstream, Baseline.Replace(">old<", ">changed underneath<"));

        Action clearing = () => service.AcceptCorrection(stale, stale.Corrections[0]);
        clearing.Should().Throw<IOException>();
    }

    [Fact]
    public void AnOverrideWhoseUpstreamIsNotInstalledSaysSoInsteadOfFailing()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        File.Delete(Upstream);

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().NotBeNull();
        file.Status.Should().Be("Cannot be read");
    }

    [Fact]
    public void AFileThatIsGoneIsReportedRatherThanThrowing()
    {
        OverrideFileModel file = ContentDoctorService.Evaluate(Path.Combine(root, "user", "local", "never.xml"));

        file.Problem.Should().Contain("no longer on disk");
        file.Corrections.Should().BeEmpty();
    }
}
