using Builder.Data.Rules;
using Builder.Presentation.Utilities;

namespace Builder.Presentation.Services;

public sealed record SelectionRuleIdentity(
    string ChoiceRowKey,
    string ChoiceKey,
    string SelectId);

/// <summary>
/// Stable identity for rendered selection rows. These names mirror the translator
/// contract while falling back to Aurora's runtime rule identifiers when the
/// translated app-contract fields are not present in the loaded content.
/// </summary>
public static class SelectionRuleIdentityService
{
    public static SelectionRuleIdentity Create(SelectRule rule, int number = 1)
    {
        ArgumentNullException.ThrowIfNull(rule);

        string ownerId = Normalize(rule.ElementHeader?.Id);
        string ownerType = Normalize(rule.ElementHeader?.Type);
        string ownerSource = Normalize(rule.ElementHeader?.Source);
        string ruleType = Normalize(rule.Attributes?.Type);
        string ruleName = Normalize(rule.Attributes?.Name);
        string supports = Normalize(rule.Attributes?.Supports);
        string defaultChoice = Normalize(rule.Attributes?.Default);
        string requiredLevel = (rule.Attributes?.RequiredLevel ?? 1).ToString();
        string numberToChoose = (rule.Attributes?.Number ?? 1).ToString();
        string rowNumber = Math.Max(number, 1).ToString();

        string choiceKey = string.Join("|",
        [
            "choice",
            ownerId,
            ownerType,
            ownerSource,
            ruleName,
            ruleType,
            supports,
            defaultChoice,
            requiredLevel,
            numberToChoose
        ]);

        string selectId = !string.IsNullOrWhiteSpace(rule.UniqueIdentifier)
            ? $"runtime:{Normalize(rule.UniqueIdentifier)}"
            : $"crc:{Normalize(TryGetChecksum(rule, Math.Max(number, 1)))}";

        string choiceRowKey = string.Join("|",
        [
            "choice-row",
            choiceKey,
            $"slot:{rowNumber}",
            selectId
        ]);

        return new SelectionRuleIdentity(choiceRowKey, choiceKey, selectId);
    }

    public static SelectRule? FindBestMatch(
        IEnumerable<SelectRule> candidates,
        int number,
        string? choiceRowKey,
        string? choiceKey,
        string? selectId)
    {
        var candidateList = candidates.ToList();
        return MatchBy(candidateList, number, choiceRowKey, identity => identity.ChoiceRowKey)
            ?? MatchBy(candidateList, number, choiceKey, identity => identity.ChoiceKey)
            ?? MatchBy(candidateList, number, selectId, identity => identity.SelectId);
    }

    private static SelectRule? MatchBy(
        IReadOnlyList<SelectRule> candidates,
        int number,
        string? savedValue,
        Func<SelectionRuleIdentity, string> selector)
    {
        if (string.IsNullOrWhiteSpace(savedValue))
            return null;

        var matches = candidates
            .Where(candidate => string.Equals(
                selector(Create(candidate, number)),
                savedValue,
                StringComparison.Ordinal))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace("\\", "/", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string TryGetChecksum(SelectRule rule, int number)
    {
        try
        {
            return rule.GetCrC(number);
        }
        catch
        {
            return string.Empty;
        }
    }
}
