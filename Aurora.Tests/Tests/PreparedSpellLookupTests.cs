using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

/// <summary>
/// Saving a character writes its prepared spells back into the XML, matching by id where the file
/// records one and by name where it does not. Both keys repeat in a real catalog: Bane is a spell in
/// both Player's Handbooks, and one spell can be listed at more than one level, so a plain
/// ToDictionary threw "An item with the same key has already been added" and the save failed.
/// </summary>
public sealed class PreparedSpellLookupTests
{
    private sealed record Spell(string? Id, string? Name, bool IsPrepared);

    private static Dictionary<string, bool> ById(params Spell[] spells) =>
        PreparedSpellLookup.ById(spells, s => s.Id, s => s.IsPrepared);

    private static Dictionary<string, bool> ByName(params Spell[] spells) =>
        PreparedSpellLookup.ByName(spells, s => s.Name, s => s.IsPrepared);

    [Fact]
    public void TwoSpellsSharingANameDoNotFailTheSave()
    {
        var lookup = ByName(
            new("ID_PHB_SPELL_BANE", "Bane", true),
            new("ID_WOTC_PHB24_SPELL_BANE", "Bane", true));

        lookup["Bane"].Should().BeTrue("both spells of that name are prepared, so the name is not ambiguous");
    }

    [Fact]
    public void ASharedNameWhoseSpellsDisagreeAnswersForNeither()
    {
        var lookup = ByName(
            new("ID_PHB_SPELL_BANE", "Bane", true),
            new("ID_WOTC_PHB24_SPELL_BANE", "Bane", false));

        lookup.Should().NotContainKey("Bane",
            "writing either answer would flip a spell the user did not touch; the id match still decides");
    }

    [Fact]
    public void NamesAreMatchedWithoutRegardToCase()
    {
        ByName(new Spell("ID_A", "Bane", true))["bane"].Should().BeTrue();
    }

    [Fact]
    public void ASpellListedTwiceUnderOneIdIsPreparedIfEitherEntryIs()
    {
        // The same spell can reach a character from two grants, or sit at two levels.
        ById(new("ID_PHB_SPELL_BANE", "Bane", false), new("ID_PHB_SPELL_BANE", "Bane", true))["ID_PHB_SPELL_BANE"]
            .Should().BeTrue();
    }

    [Fact]
    public void SpellsWithoutAKeyAreLeftOut()
    {
        ById(new(null, "Bane", true), new("  ", "Bane", true)).Should().BeEmpty();
        ByName(new("ID_A", null, true), new("ID_B", "   ", true)).Should().BeEmpty();
    }

    [Fact]
    public void UnambiguousSpellsAreStillLookedUpBothWays()
    {
        var spells = new Spell[]
        {
            new("ID_PHB_SPELL_BLESS", "Bless", true),
            new("ID_PHB_SPELL_SHIELD", "Shield", false),
        };

        PreparedSpellLookup.ById(spells, s => s.Id, s => s.IsPrepared)
            .Should().HaveCount(2).And.ContainKey("ID_PHB_SPELL_BLESS");
        PreparedSpellLookup.ByName(spells, s => s.Name, s => s.IsPrepared)
            .Should().HaveCount(2).And.Contain(new KeyValuePair<string, bool>("Shield", false));
    }
}
