namespace Builder.Presentation.Services;

public static class SelectionRuleTypePolicy
{
    public const string AbilityScoreImprovementType = "Ability Score Improvement";

    public static bool AllowsStackedSelections(string? ruleType) =>
        string.Equals(ruleType, AbilityScoreImprovementType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Types where two elements of one name are the same thing published twice - the 2014 and 2024
    /// Player's Handbooks share 358 spell names alone - so a character may hold only one of them.
    /// Which one is still the player's choice: both are offered until one is held, and the pick
    /// being edited can always be swapped for its twin.
    /// </summary>
    private static readonly HashSet<string> UniqueNameTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Spell", "Class", "Archetype", "Feat", "Race", "Background",
    };

    public static bool EnforcesUniqueNames(string? ruleType) =>
        ruleType is not null && UniqueNameTypes.Contains(ruleType);
}
