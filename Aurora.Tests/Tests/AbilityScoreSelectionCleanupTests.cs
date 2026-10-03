using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class AbilityScoreSelectionCleanupTests
{
    [Fact]
    public void ExpectedCountOnlyDiscountsRemovedSavedChoices()
    {
        var saved = new[] { "RACIAL", "INT2", "CON1", "BACKGROUND", "MISSING" };
        var before = new[] { "RACIAL", "INT2", "CON1", "BACKGROUND", "NEW" };
        var after = new[] { "BACKGROUND", "NEW" };
        int removed = AbilityScoreSelectionCleanup.CountRemovedSavedElements(saved, before, after);
        removed.Should().Be(3);
        (saved.Length - removed).Should().Be(2, "BACKGROUND and the unrelated MISSING definition still count");
    }

    [Fact]
    public void UnsavedRemovedElementsDoNotConcealMissingDefinitions()
        => AbilityScoreSelectionCleanup.CountRemovedSavedElements(
            ["BACKGROUND", "MISSING"], ["BACKGROUND", "GENERATED"], ["BACKGROUND"]).Should().Be(0);

    [Fact]
    public void SharedIdsAreCountedByOccurrence()
        => AbilityScoreSelectionCleanup.CountRemovedSavedElements(
            ["ASI", "ASI"], ["ASI", "ASI", "ASI"], ["ASI", "ASI"]).Should().Be(1);
}
