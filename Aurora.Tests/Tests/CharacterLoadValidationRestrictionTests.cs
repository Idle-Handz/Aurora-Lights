using Builder.Data;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

/// <summary>
/// A character that restricts a source stops being granted its content, so elements saved before
/// that restriction are expected to be absent on the next load rather than reported as lost.
/// </summary>
public sealed class CharacterLoadValidationRestrictionTests
{
    private static readonly CharacterLoadValidation.MissingElement FromRestricted =
        new("ID_RESTRICTED_FEAT", 1, "1 > Class > Feat");
    private static readonly CharacterLoadValidation.MissingElement FromAllowed =
        new("ID_ALLOWED_FEAT", 1, "1 > Class > Feat");

    private static ElementBase Element(string id, string source) =>
        new() { ElementHeader = new ElementHeader(id, "Feat", source, id) };

    private static ElementBase? Lookup(string id) => id switch
    {
        "ID_RESTRICTED_FEAT" => Element("ID_RESTRICTED_FEAT", "Restricted Book"),
        "ID_ALLOWED_FEAT" => Element("ID_ALLOWED_FEAT", "Allowed Book"),
        _ => null,
    };

    private static BuildSourceRestrictionSnapshot Restricting(params string[] sources) =>
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ElementsFromARestrictedSourceAreNotReportedAsLost()
    {
        var (lost, restricted) = CharacterLoadValidation.SplitRestricted(
            [FromRestricted, FromAllowed], Restricting("Restricted Book"), Lookup);

        restricted.Select(e => e.Id).Should().Equal("ID_RESTRICTED_FEAT");
        // An element from a source the character still allows is genuinely missing.
        lost.Select(e => e.Id).Should().Equal("ID_ALLOWED_FEAT");
    }

    [Fact]
    public void WithoutRestrictionsEverythingMissingIsStillLost()
    {
        var (lost, restricted) = CharacterLoadValidation.SplitRestricted(
            [FromRestricted, FromAllowed], BuildSourceRestrictionSnapshot.Empty, Lookup);

        restricted.Should().BeEmpty();
        lost.Should().HaveCount(2);
    }

    [Fact]
    public void AnElementMissingFromTheCatalogIsJudgedByItsRestrictedId()
    {
        var byId = new BuildSourceRestrictionSnapshot(
            new HashSet<string>(["ID_GONE"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var (lost, restricted) = CharacterLoadValidation.SplitRestricted(
            [new("ID_GONE", 1, "saved"), new("ID_UNKNOWN", 1, "saved")], byId, Lookup);

        restricted.Select(e => e.Id).Should().Equal("ID_GONE");
        lost.Select(e => e.Id).Should().Equal("ID_UNKNOWN");
    }
}
