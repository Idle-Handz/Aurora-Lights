using Builder.Data.Content.Review;

namespace Aurora.Tests.Tests;

public sealed class ContentReviewContractTests
{
    private const string Element = "<element id='ID_FEAT' name='Feat' type='Feat' source='Book'><description>Same text</description><rules><grant id='ID_A'/></rules></element>";
    private static ContentDocumentSnapshot Document(string path, string elements, string root = "root-a",
        ContentLayer layer = ContentLayer.Authoritative)
        => ContentDocumentSnapshot.FromXml(new(root, path), layer, "<elements>" + elements + "</elements>");

    [Fact]
    public void IdenticalCopiesRetainEveryFileAndDeclarationOccurrence()
    {
        var analysis = CanonicalContentAnalyzer.Analyze([Document("one.xml", Element + Element), Document("two.xml", Element)]);
        analysis.ReviewCases.Should().BeEmpty();
        var copies = analysis.IdenticalDeclarations.Should().ContainSingle().Which.Declarations;
        copies.Should().HaveCount(3);
        copies.Select(d => d.Declaration).Distinct().Should().HaveCount(3);
        copies.Select(d => d.Declaration.FileVersion.File).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void EqualDisplayTextWithDifferentGrantsIsAConflictWithNoReferenceCompletenessClaim()
    {
        var a = Document("one.xml", Element);
        var b = Document("two.xml", Element.Replace("ID_A", "ID_B"));
        var analysis = CanonicalContentAnalyzer.Analyze([a, b]);
        analysis.IdenticalDeclarations.Should().BeEmpty();
        var conflict = analysis.ReviewCases.Should().ContainSingle().Which;
        conflict.Kind.Should().Be(ContentReviewKind.ConflictingDefinitions);
        conflict.Definitions.Should().HaveCount(2);
        conflict.ReferenceImpact.Status.Should().Be(ReferenceInspectionStatus.NotInspected);
        CanonicalContentAnalyzer.Analyze([b, a]).ReviewCases.Single().CaseId.Should().Be(conflict.CaseId);
        var changed = Document("two.xml", Element.Replace("ID_A", "ID_C"));
        CanonicalContentAnalyzer.Analyze([a, changed]).ReviewCases.Single().CaseId.Should().NotBe(conflict.CaseId);
    }

    [Fact]
    public void DistinctIdsWithIdenticalDisplayAndMechanicsRemainDistinct()
    {
        var analysis = CanonicalContentAnalyzer.Analyze([Document("one.xml", Element + Element.Replace("ID_FEAT", "ID_OTHER"))]);
        analysis.IdenticalDeclarations.Should().BeEmpty();
        analysis.ReviewCases.Should().BeEmpty();
    }

    [Fact]
    public void SourceDifferencesAreNotSilentlyDiscarded()
    {
        var analysis = CanonicalContentAnalyzer.Analyze([Document("one.xml", Element), Document("two.xml", Element.Replace("source='Book'", "source='Other'"))]);
        analysis.ReviewCases.Should().ContainSingle().Which.Kind.Should().Be(ContentReviewKind.ConflictingDefinitions);
    }

    [Fact]
    public void LocalOverlaysAreNotMixedIntoCanonicalClassification()
    {
        var action = () => CanonicalContentAnalyzer.Analyze([Document("user/local/fix.xml", Element, layer: ContentLayer.LocalOverride)]);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SameRelativePathInDifferentRootsRetainsDistinctProvenance()
    {
        var one = Document("same.xml", Element, "root-a");
        var two = Document("same.xml", Element, "root-b");
        var copies = CanonicalContentAnalyzer.Analyze([one, two]).IdenticalDeclarations.Single().Declarations;
        copies.Select(d => d.Declaration.FileVersion.File.RootId).Should().BeEquivalentTo("root-a", "root-b");
        var invalid = () => CanonicalContentAnalyzer.Analyze([one, Document("same.xml", Element.Replace("ID_A", "ID_B"))]);
        invalid.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("../file.xml")]
    [InlineData("C:\\file.xml")]
    [InlineData("/file.xml")]
    [InlineData("core//file.xml")]
    public void FileIdentityRejectsAbsoluteOrAmbiguousPaths(string path)
    {
        var action = () => new ContentFileKey("root-a", path);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IdSpellingVariantsRequireReviewWithoutChoosingANormalization()
    {
        var analysis = CanonicalContentAnalyzer.Analyze([Document("one.xml", Element + Element.Replace("ID_FEAT", "id_feat"))]);
        var conflict = analysis.ReviewCases.Should().ContainSingle().Which;
        conflict.Kind.Should().Be(ContentReviewKind.IdentitySpellingNeedsReview);
        conflict.AuroraIds.Should().BeEquivalentTo("ID_FEAT", "id_feat");
        var whitespace = CanonicalContentAnalyzer.Analyze([Document("one.xml", Element.Replace("ID_FEAT", " ID_FEAT "))]);
        whitespace.ReviewCases.Should().ContainSingle().Which.AuroraIds.Should().Contain(" ID_FEAT ");
    }

    [Fact]
    public void MetadataBaselineDoesNotBecomeGameplayAndFileBytesIdentifyTheRevision()
    {
        string metadata = "<al:corrections xmlns:al='urn:aurora-lights:corrections:1'><al:baseline>&lt;element id='ID_HIDDEN'/&gt;</al:baseline></al:corrections>";
        var a = Document("one.xml", Element + metadata, layer: ContentLayer.LocalOverride);
        var b = Document("one.xml", Element + "\n" + metadata, layer: ContentLayer.LocalOverride);
        a.Definitions.Should().ContainSingle();
        a.Version.Sha256.Should().NotBe(b.Version.Sha256);
        a.Definitions.Single().Fingerprint.Should().Be(b.Definitions.Single().Fingerprint);
    }

    [Fact]
    public void AttributeOrderDoesNotChangeDefinitionEqualityAndIgnoredFilesDoNotSupplyContent()
    {
        var a = Document("one.xml", Element);
        var b = Document("two.xml", Element.Replace("id='ID_FEAT' name='Feat'", "name='Feat' id='ID_FEAT'"));
        var ignored = ContentDocumentSnapshot.FromXml(new("root-a", "ignored.xml"), ContentLayer.Authoritative,
            "<elements ignore='true'>" + Element.Replace("ID_A", "ID_BAD") + "</elements>");
        var analysis = CanonicalContentAnalyzer.Analyze([a, b, ignored]);
        analysis.ReviewCases.Should().BeEmpty();
        analysis.IdenticalDeclarations.Single().Declarations.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("<!DOCTYPE elements [<!ENTITY x 'data'>]><elements/>")]
    [InlineData("<elements xmlns='urn:wrong'/>")]
    [InlineData("<elements><element name='Missing ID'/></elements>")]
    public void InvalidSnapshotCannotBeClassifiedAsAValidEmptyCatalog(string xml)
    {
        var action = () => ContentDocumentSnapshot.FromXml(new("root-a", "one.xml"), ContentLayer.Authoritative, xml);
        action.Should().Throw<Exception>();
    }
}
