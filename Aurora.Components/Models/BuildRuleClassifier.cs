namespace Aurora.Components.Models;

public enum BuildRuleBucket
{
    Race,
    Class,
    Background,
    Language,
    Proficiency,
    Feat,
    Companion,
    AbilityScores,
    Overflow,
}

/// <summary>
/// Pure classification logic for routing SelectionRules to Build page tabs.
/// Takes pre-resolved strings so it has no dependency on DataManager or MAUI —
/// making it straightforwardly unit-testable.
/// </summary>
public static class BuildRuleClassifier
{
    public static readonly HashSet<string> AsiTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ability Score Improvement",
    };

    public static readonly HashSet<string> LanguageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Language",
    };

    public static readonly HashSet<string> ProficiencyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Proficiency", "Skill", "Tool Proficiency", "Armor Proficiency", "Weapon Proficiency",
        "Expertise",
    };

    public static readonly HashSet<string> FeatTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Feat", "Feat Feature",
    };

    public static readonly HashSet<string> RaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Race", "Sub Race", "Racial Trait", "Dragonmark", "Variant",
        "Race Variant", "Heritage", "Lineage",
    };

    public static readonly HashSet<string> ClassTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class", "Archetype", "Class Feature", "Archetype Feature", "Multiclass",
    };

    public static readonly HashSet<string> BackgroundTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Background", "Background Feature", "Background Variant", "Background Characteristics",
        "Deity", "Alignment",
        "Bond", "Flaw", "Ideal", "Personality Trait",
    };

    public static readonly HashSet<string> CompanionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Companion", "Companion Feature",
    };

    public static readonly HashSet<string> OptionalFlavorLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Personality Trait",
        "Ideal",
        "Bond",
        "Flaw",
        "Variant Feature",
    };

    /// <summary>
    /// Classifies a selection rule into a Build tab bucket.
    /// </summary>
    /// <param name="ruleType">The rule's own type attribute.</param>
    /// <param name="ruleName">The rule's name attribute (falls back to ruleType).</param>
    /// <param name="ownerType">The type of the element that owns/grants this rule, if any.</param>
    /// <param name="ownerName">The name of the element that owns/grants this rule, if any.</param>
    /// <param name="hasClassManager">True when the rule belongs to a ClassProgressionManager.</param>
    public static BuildRuleBucket Classify(
        string ruleType,
        string ruleName,
        string ownerType,
        string ownerName,
        bool hasClassManager)
    {
        if (IsAbilityScoresRule(ruleType, ruleName, ownerType, ownerName))
            return BuildRuleBucket.AbilityScores;

        // Rule's own type takes priority over any owner — a Feat, Language, Proficiency, or
        // Companion rule granted by any parent still routes to its own dedicated tab.
        if (FeatTypes.Contains(ruleType))
            return BuildRuleBucket.Feat;

        if (LanguageTypes.Contains(ruleType))
            return BuildRuleBucket.Language;

        if (ProficiencyTypes.Contains(ruleType))
            return BuildRuleBucket.Proficiency;

        if (CompanionTypes.Contains(ruleType))
            return BuildRuleBucket.Companion;

        // Owner-based routing: class progression manager takes priority over owner-type checks.
        if (hasClassManager)
            return BuildRuleBucket.Class;

        if (RaceTypes.Contains(ruleType) || RaceTypes.Contains(ownerType))
            return BuildRuleBucket.Race;

        if (ClassTypes.Contains(ruleType) || ClassTypes.Contains(ownerType))
            return BuildRuleBucket.Class;

        if (BackgroundTypes.Contains(ruleType) || BackgroundTypes.Contains(ownerType))
            return BuildRuleBucket.Background;

        return BuildRuleBucket.Overflow;
    }

    /// <summary>
    /// Returns the group label used to sub-group feat entries by their source.
    /// </summary>
    public static string GetFeatGroupLabel(string ownerType)
    {
        if (RaceTypes.Contains(ownerType))       return "Racial";
        if (BackgroundTypes.Contains(ownerType)) return "Background";
        if (ClassTypes.Contains(ownerType))      return "Class";
        return string.Empty;
    }

    public static bool IsOptionalFlavorSelection(string label) =>
        OptionalFlavorLabels.Contains(label) ||
        OptionalFlavorLabels.Any(f => label.StartsWith(f + " (", StringComparison.OrdinalIgnoreCase));

    private static bool IsAbilityScoresRule(string ruleType, string ruleName, string ownerType, string ownerName)
    {
        if (AsiTypes.Contains(ruleType))
            return true;

        return (ruleName.Contains("Ability Score", StringComparison.OrdinalIgnoreCase) ||
                ownerName.Contains("Ability Score", StringComparison.OrdinalIgnoreCase)) &&
               (ruleType.Equals("Racial Trait", StringComparison.OrdinalIgnoreCase) ||
                RaceTypes.Contains(ownerType));
    }
}
