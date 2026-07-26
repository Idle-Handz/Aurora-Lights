using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class MagicCastingTimeClassifierTests
{
    [Theory]
    [InlineData("Action", "action")]
    [InlineData("1 action", "action")]
    [InlineData("An action taken as part of the spell", "action")]
    [InlineData("1 bonus action", "bonus-action")]
    [InlineData("1 reaction, which you take when hit", "reaction")]
    [InlineData("10 minutes", "longer")]
    [InlineData("Special", "longer")]
    public void ClassifyGroupsCastingTimesByActionEconomy(string castingTime, string expected)
        => MagicCastingTimeClassifier.Classify(castingTime).Should().Be(expected);

    [Fact]
    public void MatchesTreatsAllAsNoFilterAndRejectsMissingCastingTimes()
    {
        MagicCastingTimeClassifier.Matches("", "All").Should().BeTrue();
        MagicCastingTimeClassifier.Matches("1 bonus action", "").Should().BeTrue();
        MagicCastingTimeClassifier.Matches("", "bonus-action").Should().BeFalse();
        MagicCastingTimeClassifier.Matches("1 bonus action", "bonus-action").Should().BeTrue();
        MagicCastingTimeClassifier.Matches("1 action", "bonus-action").Should().BeFalse();
    }
}
