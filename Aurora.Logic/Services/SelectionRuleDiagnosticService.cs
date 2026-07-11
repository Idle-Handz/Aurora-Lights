using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Services;

public enum SelectionRuleDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record SelectionRuleDiagnostic(
    SelectionRuleDiagnosticSeverity Severity,
    string Message,
    string? SuggestedFix = null);

/// <summary>
/// Explains builder choice rows that cannot produce selectable options. The
/// messages are intentionally repair-oriented so they can later feed a content
/// doctor without mutating user XML automatically.
/// </summary>
public static class SelectionRuleDiagnosticService
{
    public static IReadOnlyList<SelectionRuleDiagnostic> ExplainEmptyOptions(
        SelectRule? rule,
        int number,
        int optionCount)
    {
        return ExplainEmptyOptions(
            rule,
            number,
            optionCount,
            DataManager.Current.ElementsCollection);
    }

    public static IReadOnlyList<SelectionRuleDiagnostic> ExplainEmptyOptions(
        SelectRule? rule,
        int number,
        int optionCount,
        IEnumerable<ElementBase>? loadedElements)
    {
        if (rule is null || optionCount > 0)
            return [];

        var diagnostics = new List<SelectionRuleDiagnostic>();
        var elements = loadedElements?.ToList() ?? [];
        string selectType = rule.Attributes.Type ?? string.Empty;
        string selectName = string.IsNullOrWhiteSpace(rule.Attributes.Name)
            ? (string.IsNullOrWhiteSpace(selectType) ? "choice" : selectType)
            : rule.Attributes.Name;
        string ownerId = rule.ElementHeader?.Id ?? string.Empty;
        string ownerLabel = string.IsNullOrWhiteSpace(rule.ElementHeader?.Name)
            ? ownerId
            : $"{rule.ElementHeader.Name} ({ownerId})";

        if (!string.IsNullOrWhiteSpace(ownerId)
            && !elements.Any(element => element.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(new SelectionRuleDiagnostic(
                SelectionRuleDiagnosticSeverity.Warning,
                $"The owner element for '{selectName}' was not found in the loaded content.",
                $"Check that the rule owner exists: <element id=\"{ownerId}\" ...>."));
        }

        if (rule.Attributes.IsList)
        {
            if (rule.Attributes.ListItems?.Count > 0)
                return diagnostics;

            diagnostics.Add(new SelectionRuleDiagnostic(
                SelectionRuleDiagnosticSeverity.Error,
                $"No list items were found for '{selectName}'.",
                $"Expected shape under {ownerLabel}: <select type=\"List\" name=\"{selectName}\"><item id=\"1\">Text</item></select>."));
            return diagnostics;
        }

        if (string.IsNullOrWhiteSpace(selectType))
        {
            diagnostics.Add(new SelectionRuleDiagnostic(
                SelectionRuleDiagnosticSeverity.Error,
                $"The select row '{selectName}' does not declare a target type.",
                "Add a type attribute such as <select type=\"Language\" ...> so the builder knows which elements to list."));
            return diagnostics;
        }

        int sameTypeCount = elements.Count(element =>
            element.Type.Equals(selectType, StringComparison.OrdinalIgnoreCase));

        if (sameTypeCount == 0)
        {
            diagnostics.Add(new SelectionRuleDiagnostic(
                SelectionRuleDiagnosticSeverity.Error,
                $"No loaded elements have type '{selectType}', so '{selectName}' has no option pool.",
                $"Add or repair target elements with this shape: <element name=\"...\" type=\"{selectType}\" source=\"...\" id=\"...\">."));
            return diagnostics;
        }

        if (rule.Attributes.ContainsSupports())
        {
            diagnostics.Add(new SelectionRuleDiagnostic(
                SelectionRuleDiagnosticSeverity.Warning,
                $"The loaded content contains {sameTypeCount} '{selectType}' element(s), but none matched the supports expression for '{selectName}'.",
                BuildSupportsSuggestion(rule.Attributes.Supports)));
            return diagnostics;
        }

        diagnostics.Add(new SelectionRuleDiagnostic(
            SelectionRuleDiagnosticSeverity.Warning,
            $"The loaded content contains {sameTypeCount} '{selectType}' element(s), but no selectable options were produced for '{selectName}'.",
            "Check requirements, duplicate-selection rules, source restrictions, and whether the target elements have display names."));
        return diagnostics;
    }

    private static string BuildSupportsSuggestion(string supports)
    {
        string trimmed = supports?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return "Add matching <supports> tags to the option elements or remove the select supports filter.";

        string suggestion = $"Check that option elements include a matching <supports>{trimmed}</supports> tag, or update the select supports=\"{trimmed}\" expression.";
        if (trimmed.Contains("$(", StringComparison.Ordinal))
            suggestion += " This expression contains Aurora macro syntax; verify the reconstructed DB rule still expands the same way as XML.";

        return suggestion;
    }
}
