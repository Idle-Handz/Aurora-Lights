using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class SelectionRuleValidationTests
{
    [Fact]
    public void ShouldValidateRegisteredSlot_SkipsTheSlotBeingApplied()
    {
        var rule = CreateSelectRule("ID_TEST_SELECTION_OWNER");

        SelectionRuleValidation.ShouldValidateRegisteredSlot(rule, 1, rule, 1)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldValidateRegisteredSlot_ValidatesOtherSlotsOnTheSameRule()
    {
        var rule = CreateSelectRule("ID_TEST_SELECTION_OWNER");

        SelectionRuleValidation.ShouldValidateRegisteredSlot(rule, 2, rule, 1)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldValidateRegisteredSlot_ValidatesSameSlotOnAnotherRule()
    {
        var candidate = CreateSelectRule("ID_TEST_SELECTION_OWNER_A");
        var applied = CreateSelectRule("ID_TEST_SELECTION_OWNER_B");

        SelectionRuleValidation.ShouldValidateRegisteredSlot(candidate, 1, applied, 1)
            .Should().BeTrue();
    }

    private static SelectRule CreateSelectRule(string ownerId)
    {
        var rule = new SelectRule(new ElementHeader(
            "Selection Validation Owner",
            "Feature",
            "Test",
            ownerId));

        rule.Attributes.Type = "Language";
        rule.Attributes.Name = "Language";
        return rule;
    }
}
