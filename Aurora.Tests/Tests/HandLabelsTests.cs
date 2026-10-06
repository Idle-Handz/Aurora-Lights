using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class HandLabelsTests
{
    private const string Main = "Primary Hand";
    private const string Off = "Secondary Hand";

    private static IReadOnlyDictionary<string, string> Label(params (string Id, string Name, string? Location)[] rows) =>
        HandLabels.For(rows);

    [Fact]
    public void Two_daggers_one_in_each_hand_read_M_and_O()
    {
        var labels = Label(("a", "Dagger", Main), ("b", "Dagger", Off));

        labels["a"].Should().Be("Dagger (M)");
        labels["b"].Should().Be("Dagger (O)");
    }

    [Fact]
    public void A_dagger_in_the_main_hand_is_told_from_one_still_in_the_pack()
    {
        var labels = Label(("a", "Dagger", Main), ("b", "Dagger", ""), ("c", "Rope", ""));

        labels.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("a", "Dagger (M)"));
    }

    [Fact]
    public void A_name_that_is_the_rows_own_needs_no_hand()
    {
        Label(("a", "Dagger", Main), ("b", "Shield", Off), ("c", "Rope", "")).Should().BeEmpty();
        Label(("a", "Dagger", Main)).Should().BeEmpty();
        Label().Should().BeEmpty();
    }

    [Fact]
    public void Rows_that_are_not_in_a_hand_are_never_labelled()
    {
        // Armor is worn, not wielded, and an unequipped dagger has no hand to name.
        Label(("a", "Dagger", ""), ("b", "Dagger", null), ("c", "Chain Mail", "Armor"), ("d", "Chain Mail", "Armor"))
            .Should().BeEmpty();
    }

    [Fact]
    public void A_row_in_both_hands_says_so()
    {
        var labels = Label(("a", "Greatsword", "Two-Handed"), ("b", "Greatsword", ""), ("c", "Longsword", "Two-Handed (Versatile)"), ("d", "Longsword", Main));

        labels["a"].Should().Be("Greatsword (M+O)");
        labels["c"].Should().Be("Longsword (M+O)");
        labels["d"].Should().Be("Longsword (M)");
        labels.Should().NotContainKey("b");
    }

    [Fact]
    public void Names_are_compared_without_regard_to_case_or_padding()
    {
        var labels = Label(("a", "Dagger", Main), ("b", " dagger ", Off));

        labels["a"].Should().Be("Dagger (M)");
        labels["b"].Should().Be(" dagger  (O)", "the row's own text is kept and only the hand is added");
    }

    [Fact]
    public void A_dagger_the_player_renamed_is_not_confused_with_a_plain_one()
    {
        // The lists show the display name, so a renamed weapon is already distinct.
        Label(("a", "Twin Fang", Main), ("b", "Dagger", Off)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Primary Hand", HandHeld.Main)]
    [InlineData("Secondary Hand", HandHeld.Off)]
    [InlineData("Two-Handed", HandHeld.Both)]
    [InlineData("Two-Handed (Versatile)", HandHeld.Both)]
    [InlineData(" Primary Hand ", HandHeld.Main)]
    [InlineData("Armor", HandHeld.None)]
    [InlineData("", HandHeld.None)]
    [InlineData(null, HandHeld.None)]
    public void The_engines_location_text_maps_to_a_hand(string? location, HandHeld expected) =>
        HandLabels.Parse(location).Should().Be(expected);
}
