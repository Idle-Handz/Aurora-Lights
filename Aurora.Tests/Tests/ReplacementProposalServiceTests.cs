using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Aurora.Content.Contracts;
using Builder.Presentation.Services.Content;

namespace Aurora.Tests.Tests;

public sealed class ReplacementProposalServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-proposals-" + Guid.NewGuid().ToString("N"));
    private readonly ReplacementProposalService service = new();
    private const string Baseline = "<elements><element id='ID_OWNER' name='Original' type='Item' source='Test'><description>old</description></element><element id='ID_TARGET' name='Target' type='Item' source='Test'/></elements>";
    private static string Fixed => Baseline.Replace(">old</description>", ">corrected</description>");
    private string Origin => Path.Combine(root, "core", "items.xml");
    private string ProposalPath => Path.Combine(root, "reflections-replacements", "items.aurora-correction");
    private string Destination => Path.Combine(root, "user", "local", "reflections-replacements", "items.xml");
    private string AdditionalRoot => root + "-additional";

    public ReplacementProposalServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Origin)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ProposalPath)!);
        File.WriteAllText(Origin, Baseline, new UTF8Encoding(false));
        WriteProposal();
    }

    [Fact]
    public void DownloadedProposalIsInertUntilExplicitlyAppliedAndThenUsesManagedLifecycle()
    {
        var reviewed = service.Scan(root).Should().ContainSingle().Subject;
        reviewed.Status.Should().Be(ReplacementProposalStatus.Ready);
        File.Exists(Destination).Should().BeFalse();
        File.ReadAllText(Origin).Should().Be(Baseline);

        service.Apply(root, reviewed).Success.Should().BeTrue();

        File.Exists(ProposalPath).Should().BeTrue("the updater owns the downloaded cache");
        File.ReadAllText(Origin).Should().Be(Baseline);
        var evaluation = LocalCorrectionDocument.Evaluate(File.ReadAllText(Destination), Baseline);
        evaluation.Corrections.Should().ContainSingle().Which.State.Should().Be("review-pending");
        evaluation.EffectiveXml.Should().Contain("corrected");
        service.Scan(root).Should().ContainSingle().Which.Status.Should().Be(ReplacementProposalStatus.Applied);
        File.Delete(ProposalPath);
        File.Exists(Destination).Should().BeTrue("a local correction survives removal of its feed proposal");
    }

    [Fact]
    public void DismissalPersistsAcrossServiceInstancesButARevisedProposalCanBeOffered()
    {
        service.Dismiss(root, service.Scan(root).Single());
        new ReplacementProposalService().Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.Dismissed);
        File.Exists(Destination).Should().BeFalse();
        WriteProposal(revision: "1.1.0");
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.Ready);
    }

    [Fact]
    public void DismissedProposalCanBeReconsideredWithoutClearingAppliedHistory()
    {
        service.Dismiss(root, service.Scan(root).Single());
        var dismissed = service.Scan(root).Single();
        service.Reconsider(root, dismissed);
        var ready = service.Scan(root).Single();
        ready.Status.Should().Be(ReplacementProposalStatus.Ready);
        service.Apply(root, ready).Success.Should().BeTrue();
        service.Reconsider(root, service.Scan(root).Single());
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.Applied);
    }

    [Fact]
    public void AppliedReceiptDoesNotResurrectARetiredCorrection()
    {
        service.Apply(root, service.Scan(root).Single()).Success.Should().BeTrue();
        File.Move(Destination, Destination + ".retired-test");
        var later = service.Scan(root).Single();
        later.Status.Should().Be(ReplacementProposalStatus.Applied);
        service.Apply(root, later).Success.Should().BeFalse();
        File.Exists(Destination).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyRefusesInputsChangedSinceReview(bool changeSource)
    {
        var reviewed = service.Scan(root).Single();
        if (changeSource) File.WriteAllText(Origin, Baseline.Replace("old", "changed by author"));
        else WriteProposal(revision: "2.0.0");
        service.Apply(root, reviewed).Success.Should().BeFalse();
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void ExistingEquivalentManagedCorrectionIsRecognizedDespiteDifferentKeyAndFormatting()
    {
        string existing = LocalCorrectionDocument.Create(Fixed, Baseline, "core/items.xml",
            [new LocalCorrection("older-bundle-key", "replace", "ID_OWNER", null, null, Reason: "Previously reviewed repair")]);
        string path = WriteLocal("existing.xml", existing);
        var proposal = service.Scan(root).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.Applied);
        proposal.Detail.Should().Contain("existing.xml");
        service.Apply(root, proposal).Success.Should().BeFalse();
        File.ReadAllText(path).Should().Be(existing);
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void ExistingDifferentManagedCorrectionIsPreserved()
    {
        string existing = LocalCorrectionDocument.Create(Fixed.Replace("corrected", "my choice"), Baseline, "core/items.xml",
            [new LocalCorrection("personal", "replace", "ID_OWNER", null, null)]);
        string path = WriteLocal("personal.xml", existing);
        var proposal = service.Scan(root).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        service.Apply(root, proposal).Success.Should().BeFalse();
        File.ReadAllText(path).Should().Be(existing);
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void UnmanagedUserOverrideIsNotSilentlySuperseded()
    {
        WriteLocal("personal.xml", Fixed);
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void DirectUserOverrideIsAlsoProtected()
    {
        string user = Path.Combine(root, "user");
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "personal.xml"), Fixed);
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void NewlyCreatedLocalOverrideAfterReviewPreventsApply()
    {
        var reviewed = service.Scan(root).Single();
        WriteLocal("personal.xml", Fixed);
        service.Apply(root, reviewed).Success.Should().BeFalse();
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void MissingRequiredDefinitionPreventsApply()
    {
        WriteProposal(requiredId: "ID_MISSING");
        var proposal = service.Scan(root).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.Unavailable);
        proposal.Detail.Should().Contain("ID_MISSING");
        service.Apply(root, proposal).Success.Should().BeFalse();
    }

    [Fact]
    public void DefinitionInIgnoredDirectoryDoesNotSatisfyRequirement()
    {
        WriteProposal(requiredId: "ID_IGNORED");
        string ignored = Path.Combine(root, "ignore");
        Directory.CreateDirectory(ignored);
        File.WriteAllText(Path.Combine(ignored, "target.xml"), "<elements><element id='ID_IGNORED'/></elements>");
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.Unavailable);
    }

    [Fact]
    public void DefinitionRemovedByManagedCorrectionDoesNotSatisfyRequirement()
    {
        const string targetSource = "<elements><element id='ID_REMOVED' name='Removed' type='Item' source='Test'/></elements>";
        File.WriteAllText(Path.Combine(root, "core", "target.xml"), targetSource);
        WriteLocal("remove.xml", LocalCorrectionDocument.Create("<elements/>", targetSource, "core/target.xml",
            [new LocalCorrection("remove", "remove", "ID_REMOVED", null, null)]));
        WriteProposal(requiredId: "ID_REMOVED");
        var proposal = service.Scan(root).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.Unavailable);
        proposal.Detail.Should().Contain("ID_REMOVED");
    }

    [Fact]
    public void AdditionalContentSuppliesRequirementsAndItsUserOverridesRemainProtected()
    {
        Directory.CreateDirectory(AdditionalRoot);
        File.WriteAllText(Path.Combine(AdditionalRoot, "target.xml"), "<elements><element id='ID_EXTERNAL'/></elements>");
        WriteProposal(requiredId: "ID_EXTERNAL");
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.Unavailable);
        var ready = service.Scan(root, [AdditionalRoot]).Single();
        ready.Status.Should().Be(ReplacementProposalStatus.Ready);
        string user = Path.Combine(AdditionalRoot, "user");
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "choice.xml"), Fixed);
        service.Apply(root, ready, [AdditionalRoot]).Success.Should().BeFalse();
        service.Scan(root, [AdditionalRoot]).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void AdditionalManagedOverrideIsNotConfusedWithTheSameRelativeSourceInPrimaryRoot()
    {
        Directory.CreateDirectory(Path.Combine(AdditionalRoot, "core"));
        Directory.CreateDirectory(Path.Combine(AdditionalRoot, "user", "local"));
        File.WriteAllText(Path.Combine(AdditionalRoot, "core", "items.xml"), Baseline);
        File.WriteAllText(Path.Combine(AdditionalRoot, "user", "local", "choice.xml"),
            LocalCorrectionDocument.Create(Fixed, Baseline, "core/items.xml", [new LocalCorrection("choice", "replace", "ID_OWNER", null, null)]));
        var proposal = service.Scan(root, [AdditionalRoot]).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        proposal.Detail.Should().Contain("choice.xml");
    }

    [Theory]
    [InlineData("../outside.xml")]
    [InlineData("core/../items.xml")]
    [InlineData("user/local/other.xml")]
    [InlineData("C:/outside.xml")]
    public void ProposalCannotTraverseOrTargetAnotherLocalFile(string path)
    {
        WriteProposal(source: path);
        var proposal = service.Scan(root).Single();
        proposal.Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        service.Apply(root, proposal).Success.Should().BeFalse();
        File.Exists(Destination).Should().BeFalse();
    }

    [Fact]
    public void TamperedPayloadAndActiveOuterContentAreRejected()
    {
        string original = File.ReadAllText(ProposalPath);
        File.WriteAllText(ProposalPath, original.Replace("corrected", "tampered"));
        service.Scan(root).Single().Detail.Should().Contain("checksum");
        File.WriteAllText(ProposalPath, original.Replace("</elements>", "<element id='ACTIVE'/></elements>"));
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
    }

    [Fact]
    public void MisrepresentedBaselineAndUnclassifiedEditsAreRejected()
    {
        WriteProposal(baseline: Baseline.Replace("old", "another version"));
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        WriteProposal(corrected: Fixed.Replace("name='Target'", "name='Unreviewed edit'"));
        service.Scan(root).Single().Detail.Should().Contain("explicit");
    }

    [Fact]
    public void MalformedProposalAndHistoryAreVisibleInsteadOfBeingDiscarded()
    {
        File.WriteAllText(ProposalPath, "<!DOCTYPE elements [<!ENTITY test SYSTEM 'file:///nope'>]><elements>&test;</elements>");
        service.Scan(root).Single().Status.Should().Be(ReplacementProposalStatus.NeedsReview);
        WriteProposal();
        string statePath = Path.Combine(root, ".reflections-replacements-state.json");
        File.WriteAllText(statePath, "not valid history");
        var proposal = service.Scan(root).Single();
        proposal.Detail.Should().Contain("history");
        service.Apply(root, proposal).Success.Should().BeFalse();
        File.ReadAllText(statePath).Should().Be("not valid history");
    }

    [Fact]
    public void DuplicateProposalIdentityDoesNotPickAnArbitraryWinner()
    {
        File.Copy(ProposalPath, Path.Combine(root, "duplicate.aurora-correction"));
        service.Scan(root).Should().HaveCount(2).And.OnlyContain(p => p.Status == ReplacementProposalStatus.NeedsReview);
    }

    private string WriteLocal(string name, string xml)
    {
        string path = Path.Combine(root, "user", "local", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, xml);
        return path;
    }

    private void WriteProposal(string revision = "1.0.0", string source = "core/items.xml", string requiredId = "ID_TARGET",
        string? baseline = null, string? corrected = null)
    {
        string payload = LocalCorrectionDocument.Create(corrected ?? Fixed, baseline ?? Baseline, source,
            [new LocalCorrection("repair-owner", "replace", "ID_OWNER", null, null)]).Replace("\r\n", "\n");
        XNamespace ns = ReplacementProposalService.Namespace;
        var document = new XDocument(new XElement("elements", new XElement("info", new XElement("name", "Test proposal")),
            new XElement(ns + "proposal", new XAttribute("schema-version", "1"), new XAttribute("id", "items"), new XAttribute("revision", revision),
                new XElement(ns + "title", "Repair item"), new XElement(ns + "summary", "Replace the reviewed broken item description."),
                new XElement(ns + "variant", new XAttribute("source-path", source), new XAttribute("source-sha256", Hash(File.ReadAllBytes(Origin))),
                    new XElement(ns + "requires", new XAttribute("id", requiredId)),
                    new XElement(ns + "payload", new XAttribute("sha256", Hash(Encoding.UTF8.GetBytes(payload))), payload)))));
        File.WriteAllText(ProposalPath, document.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        if (Directory.Exists(AdditionalRoot)) Directory.Delete(AdditionalRoot, recursive: true);
    }
}
