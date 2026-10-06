using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class RarityRepairTests
{
    [Theory]
    [InlineData("Very Rare", "Very Rare")]
    [InlineData("very rare", "Very Rare")]
    [InlineData("  VERY   RARE ", "Very Rare")]
    [InlineData("Uncommon", "Uncommon")]
    [InlineData("artifact", "Artifact")]
    public void A_known_rarity_in_any_case_or_padding_has_one_spelling(string raw, string expected)
    {
        RarityRepair.Canonical(raw).Should().Be(expected);
        RarityRepair.Suggest(raw).Should().BeNull("it is not a typo");
        RarityRepair.Normalize(raw).Should().Be(expected);
        RarityRepair.IsAccepted(raw).Should().BeTrue();
    }

    // The three that turned up in the real content, then each kind of single edit.
    [Theory]
    [InlineData("Vert Rare", "Very Rare")]     // letter changed
    [InlineData("Lgendary", "Legendary")]      // letter dropped
    [InlineData("unommon", "Uncommon")]        // letter dropped, and the wrong case
    [InlineData("Legendaryy", "Legendary")]    // letter added
    [InlineData("Very Rar", "Very Rare")]      // dropped at the end
    [InlineData("Raer", "Rare")]               // two letters swapped
    [InlineData("Artifcat", "Artifact")]       // two letters swapped, deeper in
    [InlineData("very-rare", "Very Rare")]     // the space swapped for a hyphen
    public void A_value_one_edit_from_a_rarity_is_read_as_that_rarity(string typo, string expected)
    {
        RarityRepair.Canonical(typo).Should().BeNull();
        RarityRepair.Suggest(typo).Should().Be(expected);
        RarityRepair.Normalize(typo).Should().Be(expected);
        RarityRepair.IsAccepted(typo).Should().BeFalse("a typo is a mistake to fix, not a value to accept");
    }

    [Theory]
    [InlineData("Rarity Varies")]
    [InlineData("Rarity varies by potion type")]
    [InlineData("varies")]
    [InlineData("Variable")]
    [InlineData("Rare, Very Rare, or Legendary")]
    [InlineData("Uncommon to Rare")]
    [InlineData("Common/Uncommon")]
    public void A_value_that_depends_on_something_or_spans_rarities_is_Varies(string raw)
    {
        RarityRepair.Suggest(raw).Should().BeNull();
        RarityRepair.Normalize(raw).Should().Be(RarityRepair.Varies);
        RarityRepair.IsAccepted(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData("Infusion")]
    [InlineData("Artificer Infusion")]
    [InlineData("artificer infusion")]
    public void An_infusion_is_Infusion(string raw)
    {
        RarityRepair.Normalize(raw).Should().Be(RarityRepair.Infusion);
        RarityRepair.IsAccepted(raw).Should().BeTrue();
    }

    [Fact]
    public void The_content_saying_Unknown_is_accepted_as_Unknown()
    {
        RarityRepair.Normalize("Unknown").Should().Be(RarityRepair.Unknown);
        RarityRepair.Normalize("unknown").Should().Be(RarityRepair.Unknown);
        RarityRepair.IsAccepted("Unknown").Should().BeTrue();
    }

    [Theory]
    [InlineData("Mythic")]                     // a rarity the game does not have
    [InlineData("Lgendry")]                    // two letters dropped: not a confident typo
    [InlineData("Vrey Rar")]                   // a swap and a drop
    [InlineData("Ucommon")]                    // one edit from Common AND from Uncommon: no confident answer
    [InlineData("Rare Ish")]
    public void Any_other_value_is_Unknown_and_is_not_guessed_at(string raw)
    {
        RarityRepair.Suggest(raw).Should().BeNull();
        RarityRepair.Normalize(raw).Should().Be(RarityRepair.Unknown);
        RarityRepair.IsAccepted(raw).Should().BeFalse("nothing recognises it, so the Content Doctor should list it");
    }

    [Fact]
    public void Nothing_stays_nothing_and_is_accepted()
    {
        RarityRepair.Normalize(null).Should().BeEmpty();
        RarityRepair.Normalize("   ").Should().BeEmpty();
        RarityRepair.Suggest("").Should().BeNull();
        RarityRepair.IsAccepted(null).Should().BeTrue("a mundane item has no rarity");
    }

    [Fact]
    public void A_single_rarity_mentioned_among_other_words_is_not_a_range()
    {
        // Only a choice of two or more real rarities counts as Varies.
        RarityRepair.Normalize("Rare (requires attunement)").Should().Be(RarityRepair.Unknown);
    }

    [Fact]
    public void Every_normalized_value_ranks_the_real_rarities_first_then_the_groups()
    {
        string[] ladder = [.. RarityRepair.Known, RarityRepair.Varies, RarityRepair.Infusion, RarityRepair.Unknown];

        ladder.Select(RarityRepair.Rank).Should().Equal(Enumerable.Range(0, ladder.Length));
        RarityRepair.Rank("Something else").Should().Be(ladder.Length);
        RarityRepair.Rank(null).Should().Be(ladder.Length);
    }

    [Fact]
    public void Normalizing_twice_changes_nothing()
    {
        string[] raws = ["Vert Rare", "Rarity Varies", "Artificer Infusion", "Mythic", "very rare", "Unknown", ""];

        foreach (string raw in raws)
            RarityRepair.Normalize(RarityRepair.Normalize(raw)).Should().Be(RarityRepair.Normalize(raw), raw);
    }
}
