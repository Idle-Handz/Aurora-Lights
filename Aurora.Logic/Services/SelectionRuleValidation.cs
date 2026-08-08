using Builder.Data.Rules;

namespace Builder.Presentation.Services;

/// <summary>
/// Shared decisions for validating registered selection slots after a build-page mutation.
/// </summary>
public static class SelectionRuleValidation
{
    public static bool ShouldValidateRegisteredSlot(
        SelectRule candidateRule,
        int candidateNumber,
        SelectRule appliedRule,
        int appliedNumber)
    {
        ArgumentNullException.ThrowIfNull(candidateRule);
        ArgumentNullException.ThrowIfNull(appliedRule);

        if (candidateNumber != appliedNumber)
            return true;

        if (ReferenceEquals(candidateRule, appliedRule))
            return false;

        return string.IsNullOrWhiteSpace(candidateRule.UniqueIdentifier) ||
               string.IsNullOrWhiteSpace(appliedRule.UniqueIdentifier) ||
               !candidateRule.UniqueIdentifier.Equals(
                   appliedRule.UniqueIdentifier,
                   StringComparison.Ordinal);
    }
}
