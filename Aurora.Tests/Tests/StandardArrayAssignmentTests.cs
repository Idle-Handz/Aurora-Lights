using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class StandardArrayAssignmentTests
{
    [Fact]
    public void Completed_array_can_swap_any_two_scores_without_duplicates()
    {
        var scores = new Dictionary<string, int>
        {
            ["STR"] = 15, ["DEX"] = 14, ["CON"] = 13,
            ["INT"] = 12, ["WIS"] = 10, ["CHA"] = 8
        };
        var assignments = new Dictionary<string, int>(scores);

        var changed = StandardArrayAssignment.Assign(assignments, scores, "STR", 14);

        changed["STR"].Should().Be(14);
        changed["DEX"].Should().Be(15);
        changed.Values.Should().BeEquivalentTo(scores.Values);
        assignments.Should().BeEquivalentTo(changed);
        scores["STR"].Should().Be(15, "the caller controls when live scores are applied");
    }

    [Fact]
    public void Taking_an_occupied_value_from_an_unassigned_row_swaps_the_existing_score()
    {
        var scores = new Dictionary<string, int> { ["STR"] = 18, ["DEX"] = 15 };
        var assignments = new Dictionary<string, int> { ["STR"] = 0, ["DEX"] = 15 };

        var changed = StandardArrayAssignment.Assign(assignments, scores, "STR", 15);

        changed["STR"].Should().Be(15);
        changed["DEX"].Should().Be(18);
        assignments["STR"].Should().Be(15);
        assignments["DEX"].Should().Be(0);
    }

    [Fact]
    public void Choosing_an_unused_value_only_changes_that_row()
    {
        var scores = new Dictionary<string, int> { ["STR"] = 8, ["DEX"] = 10 };
        var assignments = new Dictionary<string, int> { ["STR"] = 0, ["DEX"] = 10 };

        var changed = StandardArrayAssignment.Assign(assignments, scores, "STR", 15);

        changed["STR"].Should().Be(15);
        changed["DEX"].Should().Be(10);
    }
}
