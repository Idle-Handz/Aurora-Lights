namespace Builder.Presentation.Services;

public static class SelectionRuleTypePolicy
{
    public const string AbilityScoreImprovementType = "Ability Score Improvement";

    public static bool AllowsStackedSelections(string? ruleType) =>
        string.Equals(ruleType, AbilityScoreImprovementType, StringComparison.OrdinalIgnoreCase);
}
