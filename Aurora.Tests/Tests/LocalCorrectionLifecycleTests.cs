using Aurora.Content.Preparation;
using Aurora.Content;
using Builder.Data.Files;
using LocalCorrection = Aurora.Content.Contracts.LocalCorrection;
using LocalCorrectionDocument = Aurora.Content.Contracts.LocalCorrectionDocument;
using Builder.Presentation.Services.Content;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Aurora.Tests.Tests;

public sealed class LocalCorrectionLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-lifecycle-" + Guid.NewGuid().ToString("N"));
    private string Origin => Path.Combine(root, "core", "features.xml");
    private string Local => Path.Combine(root, "user", "local", "fix.xml");
    private string Database => Path.Combine(root, "content.sqlite");
    private const string Baseline = "<elements><element name='Old' type='Item' source='Test' id='ID_FIX'><description>old</description></element><element name='Companion' type='Item' source='Test' id='ID_COMPANION'><description>old companion</description></element></elements>";
    private static string Fixed => Baseline.Replace("name='Old'", "name='Fixed'").Replace(">old</description>", ">corrected</description>");
    private static LocalCorrection Replacement(string state = "review-pending") => new("fix", "replace", "ID_FIX", null, null, state);

    public LocalCorrectionLifecycleTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Origin)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Local)!);
        File.WriteAllText(Origin, Baseline);
    }

    [Fact]
    public void PinnedCorrection_SurvivesUpstreamChange_WhileCompanionFollowsUpstream()
    {
        string local = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        string incoming = Baseline.Replace(">old</description>", ">new upstream</description>")
            .Replace("old companion", "new companion");
        var result = LocalCorrectionDocument.Evaluate(local, incoming);
        result.EffectiveXml.Should().Contain("corrected").And.Contain("new companion").And.NotContain("new upstream");
        result.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void RuntimeFailure_InManagedFileCannotBeDowngradedToASkippedElement()
    {
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]));
        var parseFailure = new InvalidDataException("A corrected element could not be parsed.");
        var action = () => LocalCorrectionDocument.RethrowManagedRuntimeFailure(Local, parseFailure);
        action.Should().Throw<InvalidDataException>().Which.InnerException.Should().BeSameAs(parseFailure);
        File.WriteAllText(Local, File.ReadAllText(Local).Replace("version=\"1\"", "version=\"99\""));
        action.Should().Throw<InvalidDataException>();
        File.WriteAllText(Local, File.ReadAllText(Local).Replace("</elements>", ""));
        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void RuntimeFailure_InLegacyUnmarkedLocalFileKeepsExistingSkipPolicy()
    {
        File.WriteAllText(Local, Fixed);
        var action = () => LocalCorrectionDocument.RethrowManagedRuntimeFailure(Local, new InvalidDataException("Legacy XML failure"));
        action.Should().NotThrow();
        File.WriteAllText(Local, "<elements><element");
        action.Should().NotThrow();
        File.WriteAllText(Local, "<not-elements/>");
        action.Should().NotThrow();
    }

    [Fact]
    public void IncorporatedCorrection_StaysPinnedUntilExplicitReview()
    {
        var pinned = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        var result = LocalCorrectionDocument.Evaluate(pinned, Fixed);
        result.CanRetire.Should().BeFalse();
        result.ReviewReasons.Should().Contain(r => r.Contains("incorporated"));
        var accepted = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement("accepted-upstream")]);
        LocalCorrectionDocument.Evaluate(accepted, Fixed).CanRetire.Should().BeTrue();
    }

    [Fact]
    public void UnclassifiedLocalChangesAndAdditions_ArePreservedAndPreventRetirement()
    {
        string local = Fixed.Replace("old companion", "my companion").Replace("</elements>",
            "<element name='Mine' type='Item' source='Local' id='ID_MINE'/></elements>");
        var result = LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, Baseline,
            "core/features.xml", [Replacement("accepted-upstream")]), Fixed);
        result.CanRetire.Should().BeFalse();
        result.EffectiveXml.Should().Contain("my companion").And.Contain("ID_MINE");
    }

    [Fact]
    public void UnmarkedLocalAddition_CanRetireOnceAuthoritativeContentMatchesItExactly()
    {
        string local = Baseline.Replace("</elements>", "<element name='Mine' type='Item' source='Local' id='ID_MINE'/></elements>");
        var result = LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, Baseline,
            "core/features.xml", []), local);
        result.CanRetire.Should().BeTrue();
    }

    [Fact]
    public void LocalRootAttributeChanges_AreNotSilentlyDiscardedOrRetired()
    {
        string local = Baseline.Replace("<elements>", "<elements app='2.0'>");
        var result = LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, Baseline,
            "core/features.xml", []), Baseline);
        result.EffectiveXml.Should().Contain("app=\"2.0\"");
        result.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void DisabledLocalFile_DoesNotApplyOrRetireItsCorrections()
    {
        string local = Fixed.Replace("<elements>", "<elements ignore='true'>");
        var result = LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, Baseline,
            "core/features.xml", [Replacement()]), Baseline);
        result.EffectiveXml.Should().Be(Baseline);
        result.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void DuplicateIdRename_UsesFingerprintAndKeepsTheOtherDefinition()
    {
        string baseline = "<elements><element id='ID_CLOUD' name='Cloud' type='Racial Trait' source='Test'/><element id='ID_CLOUD' name='Breath' type='Racial Trait' source='Test'/><element id='ID_PARENT' name='Parent' type='Race' source='Test'><rules><grant type='Racial Trait' id='ID_CLOUD'/><grant type='Racial Trait' id='ID_CLOUD'/></rules></element></elements>";
        string local = baseline.Replace("id='ID_CLOUD' name='Breath'", "id='ID_BREATH' name='Breath'")
            .Replace("id='ID_CLOUD'/></rules>", "id='ID_BREATH'/></rules>");
        string fingerprint = LocalCorrectionDocument.Fingerprint(XDocument.Parse(baseline).Root!.Elements("element").ElementAt(1));
        var corrections = new[] { new LocalCorrection("rename", "rename", "ID_CLOUD", "ID_BREATH", fingerprint, Group: "breath"),
            new LocalCorrection("grant", "replace", "ID_PARENT", null, null, Group: "breath") };
        var result = LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, baseline, "core/features.xml", corrections), baseline);
        var document = XDocument.Parse(result.EffectiveXml);
        document.Root!.Elements("element").Select(e => (string?)e.Attribute("id")).Should().BeEquivalentTo("ID_CLOUD", "ID_BREATH", "ID_PARENT");
        document.Descendants("grant").Select(e => (string?)e.Attribute("id")).Should().Equal("ID_CLOUD", "ID_BREATH");
        Action partialReview = () => LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(local, baseline,
            "core/features.xml", [corrections[0] with { State = "accepted-upstream" }, corrections[1]]), local);
        partialReview.Should().Throw<InvalidDataException>().WithMessage("*reviewed together*");
    }

    [Fact]
    public void DuplicateRemoval_DoesNotRemoveRetainedSinglePieceAfterUpstreamFix()
    {
        string baseline = "<elements><element id='ID_BALL' name='One' type='Item' source='Test'/><element id='ID_BALL' name='Twenty' type='Item' source='Test'/></elements>";
        var doc = XDocument.Parse(baseline);
        var removed = doc.Root!.Elements().Last();
        string fingerprint = LocalCorrectionDocument.Fingerprint(removed);
        removed.Remove();
        string local = doc.ToString(SaveOptions.DisableFormatting);
        var annotated = LocalCorrectionDocument.Create(local, baseline, "core/features.xml",
            [new("remove-pack", "remove", "ID_BALL", null, fingerprint)]);
        foreach (string incoming in new[] { baseline, local })
        {
            var result = LocalCorrectionDocument.Evaluate(annotated, incoming);
            XDocument.Parse(result.EffectiveXml).Root!.Elements("element").Should().ContainSingle()
                .Which.Attribute("name")!.Value.Should().Be("One");
        }
    }

    [Fact]
    public void UnsupportedMetadata_AndAmbiguousOriginals_AreRejected()
    {
        string annotated = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        Action unknown = () => LocalCorrectionDocument.Evaluate(annotated.Replace("version=\"1\"", "version=\"2\""), Baseline);
        unknown.Should().Throw<InvalidDataException>();
        string duplicate = Baseline.Replace("</elements>", "<element name='Another' type='Item' source='Test' id='ID_FIX'/></elements>");
        Action ambiguous = () => LocalCorrectionDocument.Evaluate(LocalCorrectionDocument.Create(Fixed, duplicate,
            "core/features.xml", [Replacement()]), Baseline);
        ambiguous.Should().Throw<InvalidDataException>().WithMessage("*Ambiguous*");
    }

    [Theory]
    [InlineData("../outside.xml")]
    [InlineData("user/local/other.xml")]
    [InlineData("core/../../outside.xml")]
    public void SourcePaths_CannotEscapeOrTargetAnotherOverride(string path)
    {
        Action resolve = () => LocalCorrectionDocument.ResolveSourcePath(root, path);
        resolve.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Sync_MirrorsOriginalsAndEffectiveContent_AndRebuildsMirrorFromXml()
    {
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]));
        RunImport();
        Query("SELECT name FROM elements WHERE aurora_id='ID_FIX'").Should().Be("Fixed");
        Query("SELECT COUNT(*) FROM elements WHERE aurora_id='ID_FIX'").Should().Be("1");
        Query("SELECT upstream_xml FROM local_override_files").Should().Be(Baseline);
        Query("SELECT state FROM local_corrections").Should().Be("review-pending");
        File.ReadAllText(Origin).Should().Be(Baseline);
        ContentDatabaseReader.IsStale([root], Database).Should().BeFalse();
        ContentDatabaseReader.ReadLocalCorrectionContent(Local, Database)!.EffectiveXml.Should().Contain("corrected");
        File.AppendAllText(Local, "\n");
        ContentDatabaseReader.ReadLocalCorrectionContent(Local, Database).Should().BeNull();
        File.Delete(Database);
        RunImport();
        Query("SELECT state FROM local_corrections").Should().Be("review-pending");
    }

    [Fact]
    public void Sync_RetiresOnlyAfterReviewAndSuccessfulImport_AndDoesNotReimportArchive()
    {
        File.WriteAllText(Origin, Fixed);
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]));
        RunImport();
        File.Exists(Local).Should().BeTrue();
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement("accepted-upstream")]));
        RunImport();
        File.Exists(Local).Should().BeFalse();
        Directory.GetFiles(Path.GetDirectoryName(Local)!, "*.retired-*").Should().ContainSingle();
        Query("SELECT status FROM local_override_files").Should().Be("retired");
        ContentDatabaseReader.IsStale([root], Database).Should().BeFalse();
        RunImport();
        Query("SELECT COUNT(*) FROM elements WHERE aurora_id='ID_FIX'").Should().Be("1");
        Query("SELECT COUNT(*) FROM local_corrections").Should().Be("1");
    }

    [Fact]
    public void RemovingLocalFile_RemovesEffectiveCorrectionFromDatabase()
    {
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]));
        RunImport();
        File.Delete(Local);
        RunImport();
        Query("SELECT name FROM elements WHERE aurora_id='ID_FIX'").Should().Be("Old");
        Query("SELECT COUNT(*) FROM local_corrections").Should().Be("0");
        File.WriteAllText(Origin, Baseline.Replace("old companion", "updated companion"));
        RunImport();
        ContentDatabaseReader.IsStale([root], Database).Should().BeFalse();
    }

    [Fact]
    public void GenericXmlSave_CannotDiscardOrReplaceCorrectionMetadata()
    {
        string annotated = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        File.WriteAllText(Local, annotated);
        Action save = () => new ElementsFile(Baseline).SaveContent(new FileInfo(Local));
        save.Should().Throw<InvalidDataException>();
        File.ReadAllText(Local).Should().Be(annotated);
    }

    [Fact]
    public void ExplicitReview_RequiresTheVersionsActuallyReviewed()
    {
        File.WriteAllText(Origin, Fixed);
        File.WriteAllText(Local, LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]));
        string localHash = LocalCorrectionDocument.FileFingerprint(Local);
        string upstreamHash = LocalCorrectionDocument.FileFingerprint(Origin);
        File.AppendAllText(Origin, "\n");
        Action stale = () => LocalCorrectionDocument.AcceptUpstream(Local, localHash, upstreamHash, ["fix"]);
        stale.Should().Throw<IOException>();
        LocalCorrectionDocument.AcceptUpstream(Local, localHash, LocalCorrectionDocument.FileFingerprint(Origin), ["fix"]);
        LocalCorrectionDocument.ForRuntime(Local)!.CanRetire.Should().BeTrue();
    }

    [Fact]
    public async Task IndexUpdater_CannotOverwriteAFileWithCorrectionMetadata()
    {
        string annotated = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        // An index could target an unusual location. Protection is content-based too.
        File.WriteAllText(Origin, annotated);
        File.WriteAllText(Path.Combine(root, "core.index"), "<index><info><name>Test</name><update version='1'><file name='core.index' url='https://test.invalid/core.index'/></update></info><files><file name='features.xml' url='https://test.invalid/features.xml'/></files></index>");
        using var http = new HttpClient(new ResponseHandler(Baseline));
        var service = new ContentIndexUpdateService(http);
        var result = await service.UpdateAsync(new(root, ["core.index"]));
        result.FailedFileCount.Should().BeGreaterThan(0);
        File.ReadAllText(Origin).Should().Be(annotated);
    }

    [Fact]
    public async Task IndexUpdater_UpdatesAPlainLocalFile()
    {
        // Local homebrew without correction markup is an ordinary content file.
        string homebrew = Baseline.Replace("name='Old'", "name='Homebrew'");
        File.WriteAllText(Local, homebrew);
        var result = await UpdateLocalFolder(Fixed);
        result.FailedFileCount.Should().Be(0);
        File.ReadAllText(Local).Should().Be(Fixed);
    }

    [Fact]
    public async Task IndexUpdater_CannotOverwriteALocalCorrection()
    {
        string annotated = LocalCorrectionDocument.Create(Fixed, Baseline, "core/features.xml", [Replacement()]);
        File.WriteAllText(Local, annotated);
        var result = await UpdateLocalFolder(Baseline);
        result.FailedFileCount.Should().BeGreaterThan(0);
        File.ReadAllText(Local).Should().Be(annotated);
    }

    private async Task<ContentIndexUpdateResult> UpdateLocalFolder(string downloaded)
    {
        File.WriteAllText(Path.Combine(root, "user", "local.index"), "<index><info><name>Local</name><update version='1'><file name='local.index' url='https://test.invalid/local.index'/></update></info><files><file name='fix.xml' url='https://test.invalid/fix.xml'/></files></index>");
        using var http = new HttpClient(new ResponseHandler(downloaded));
        return await new ContentIndexUpdateService(http).UpdateAsync(new(root, [Path.Combine("user", "local.index")]));
    }

    /// <summary>Imports the fixture content the way the app does, in process through the library.</summary>
    private ContentImportResult RunImport() =>
        ContentImport.ImportAsync(root, Database).GetAwaiter().GetResult();

    private string Query(string sql)
    {
        using var connection = ContentDatabase.OpenReadableConnection(Database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }

    private sealed class ResponseHandler(string xml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith(".index") ? HttpStatusCode.NotModified : HttpStatusCode.OK)
            { Content = new StringContent(xml) });
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}
