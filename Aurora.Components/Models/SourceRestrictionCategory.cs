using System.Globalization;

namespace Aurora.Components.Models;

public enum SourceRestrictionCategory
{
    ThirdParty,
    Homebrew,
    Official5E,
    Official55E
}

public sealed record SourceRestrictionCategoryToggle(
    SourceRestrictionCategory Category,
    bool IsEnabled);

public static class SourceRestrictionCategoryClassifier
{
    private static readonly DateOnly RevisedRulesReleaseDate = new(2024, 9, 17);

    public static SourceRestrictionCategory? Classify(
        bool isOfficial,
        bool isThirdParty,
        bool isHomebrew,
        string? author,
        string? name,
        string? releaseDate)
    {
        if (isThirdParty)
            return SourceRestrictionCategory.ThirdParty;

        if (isHomebrew)
            return SourceRestrictionCategory.Homebrew;

        bool isWizardsSource = author?.Contains(
            "Wizards of the Coast",
            StringComparison.OrdinalIgnoreCase) == true;
        if (!isOfficial && !isWizardsSource)
            return null;

        return UsesRevisedRulesEra(name, releaseDate)
            ? SourceRestrictionCategory.Official55E
            : SourceRestrictionCategory.Official5E;
    }

    private static bool UsesRevisedRulesEra(string? name, string? releaseDate)
    {
        if (name?.Contains("(2024)", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return TryParseReleaseDate(releaseDate, out DateOnly parsed)
            && parsed >= RevisedRulesReleaseDate;
    }

    private static bool TryParseReleaseDate(string? value, out DateOnly parsed)
    {
        if (DateOnly.TryParseExact(
                value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
        {
            return true;
        }

        return DateOnly.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed);
    }
}
