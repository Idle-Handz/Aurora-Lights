using Builder.Presentation.Services;
using System.Xml.Linq;

namespace Aurora.Tests.Tests;

public sealed class CharacterLoadValidationTests
{
    private static XElement Build(string elements, string summary = "") => XElement.Parse(
        $"<build><elements>{elements}</elements><sum>{summary}</sum></build>");
    private static string Element(string id, string type = "Feat") => $"<element id='{id}' type='{type}'/>";

    [Fact]
    public void AddedGrantsAreNotMissingAndDoNotMaskMissingSavedIds()
    {
        var build = Build(Element("SELECTED"), Element("SELECTED"));
        CharacterLoadValidation.FindMissing(build, ["SELECTED", "NEW_GRANT"]).Should().BeEmpty();
        CharacterLoadValidation.FindMissing(build, ["NEW_GRANT", "OTHER"])
            .Should().ContainSingle().Which.Id.Should().Be("SELECTED");
    }

    [Fact]
    public void SourceAvailabilityOnlyRecordsAreIgnored()
    {
        var build = Build("<element type='Source' id='BOOK'>" + Element("PASSIVE") + "</element>",
            Element("BOOK", "Source") + Element("PASSIVE"));
        CharacterLoadValidation.FindMissing(build, []).Should().BeEmpty();
    }

    [Fact]
    public void BuildUsageTakesPrecedenceOverSourceBookkeeping()
    {
        var build = Build("<element type='Source' id='BOOK'>" + Element("USED") + "</element>" +
            "<element type='Class' registered='BARBARIAN'><element type='Proficiency' id='USED'/></element>",
            Element("USED", "Proficiency"));
        var missing = CharacterLoadValidation.FindMissing(build, ["BARBARIAN"]);
        missing.Should().ContainSingle().Which.Id.Should().Be("USED");
        missing[0].SavedPath.Should().Contain("BARBARIAN");
    }

    [Fact]
    public void IndirectGrantsRemainRequiredEvenIfTheirSourceIsNoLongerAvailable()
    {
        var build = Build("<element type='Class' registered='BARBARIAN'><element type='Proficiency' id='SIMPLE'>" +
            Element("CLAW", "Proficiency") + "</element></element>", Element("CLAW", "Proficiency"));
        var missing = CharacterLoadValidation.FindMissing(build, ["BARBARIAN", "SIMPLE"]);
        missing.Should().ContainSingle().Which.Id.Should().Be("CLAW");
        missing[0].SavedPath.Should().Be("BARBARIAN > SIMPLE > CLAW");
    }

    [Fact]
    public void ExplicitChoicesAreCheckedEvenWhenTheOldSumOmitsThem()
    {
        var build = Build("<element type='Feat' registered='CHOICE'/><element type='List' registered='2' isList='true'/>");
        CharacterLoadValidation.FindMissing(build, []).Should().ContainSingle().Which.Id.Should().Be("CHOICE");
    }

    [Fact]
    public void MissingOccurrencesCannotBeOffsetByAnotherId()
    {
        var build = Build("", Element("ASI") + Element("ASI"));
        CharacterLoadValidation.FindMissing(build, ["ASI", "OTHER"])
            .Should().ContainSingle().Which.Should().Be(new CharacterLoadValidation.MissingElement(
                "ASI", 1, "saved character summary (origin unavailable)"));
    }

    [Fact]
    public void NormalizationDiscountsOnlyExactRemovedOccurrences()
    {
        var build = Build("", Element("INACTIVE_ASI") + Element("DUPLICATE") + Element("DUPLICATE") + Element("MISSING"));
        var missing = CharacterLoadValidation.FindMissing(build, ["DUPLICATE", "NEW"],
            ["INACTIVE_ASI", "DUPLICATE", "DUPLICATE"], ["DUPLICATE"]);
        missing.Should().ContainSingle().Which.Id.Should().Be("MISSING");
    }

    [Fact]
    public void NormalizationCannotHideAChoiceThatNeverLoaded()
    {
        var build = Build(Element("MISSING_ASI", "Ability Score Improvement"));
        CharacterLoadValidation.FindMissing(build, [], ["OTHER_ASI"], [])
            .Should().ContainSingle().Which.Id.Should().Be("MISSING_ASI");
    }

    [Fact]
    public void RemovingAnUnsavedDuplicateDoesNotDiscountTheRetainedSavedOccurrence()
    {
        var build = Build("", Element("FEAT"));
        CharacterLoadValidation.FindMissing(build, [], ["FEAT", "FEAT"], ["FEAT"])
            .Should().ContainSingle().Which.Id.Should().Be("FEAT");
    }

    [Fact]
    public void LateResolvedFeaturesDoNotProduceMissingDiagnostics()
    {
        var build = Build(Element("RACE") + Element("BREATH"), Element("RACE") + Element("BREATH"));
        CharacterLoadValidation.FindMissing(build, ["RACE"]).Should().ContainSingle();
        CharacterLoadValidation.FindMissing(build, ["RACE", "BREATH"]).Should().BeEmpty();
    }
}
