using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class SelectionRuleDiagnosticServiceTests
{
    [Fact]
    public void ExplainEmptyOptions_ReturnsNoDiagnosticsWhenOptionsExist()
    {
        var rule = CreateSelectRule("Language", "Language");

        var diagnostics = SelectionRuleDiagnosticService.ExplainEmptyOptions(
            rule,
            number: 1,
            optionCount: 1,
            loadedElements: []);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ExplainEmptyOptions_ExplainsMissingTargetElementType()
    {
        var rule = CreateSelectRule("Imaginary Type", "Imaginary Choice");

        var diagnostics = SelectionRuleDiagnosticService.ExplainEmptyOptions(
            rule,
            number: 1,
            optionCount: 0,
            loadedElements: []);

        diagnostics.Should().Contain(diagnostic =>
            diagnostic.Severity == SelectionRuleDiagnosticSeverity.Error
            && diagnostic.Message.Contains("No loaded elements have type 'Imaginary Type'", StringComparison.Ordinal)
            && diagnostic.SuggestedFix!.Contains("type=\"Imaginary Type\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplainEmptyOptions_ExplainsMalformedListSelect()
    {
        var rule = CreateSelectRule("List", "Personality Trait");

        var diagnostics = SelectionRuleDiagnosticService.ExplainEmptyOptions(
            rule,
            number: 1,
            optionCount: 0,
            loadedElements: []);

        diagnostics.Should().Contain(diagnostic =>
            diagnostic.Severity == SelectionRuleDiagnosticSeverity.Error
            && diagnostic.Message.Contains("No list items", StringComparison.Ordinal)
            && diagnostic.SuggestedFix!.Contains("<item id=\"1\">Text</item>", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplainEmptyOptions_SuggestsSupportTagRepairWhenTypeExistsButNoOptionsMatch()
    {
        var rule = CreateSelectRule("Language", "Language");
        rule.Attributes.Supports = "Exotic";

        var diagnostics = SelectionRuleDiagnosticService.ExplainEmptyOptions(
            rule,
            number: 1,
            optionCount: 0,
            loadedElements:
            [
                new ElementBase("Common", "Language", "Test", "ID_TEST_LANGUAGE_COMMON")
            ]);

        diagnostics.Should().Contain(diagnostic =>
            diagnostic.Severity == SelectionRuleDiagnosticSeverity.Warning
            && diagnostic.Message.Contains("none matched the supports expression", StringComparison.Ordinal)
            && diagnostic.SuggestedFix!.Contains("<supports>Exotic</supports>", StringComparison.Ordinal));
    }

    private static SelectRule CreateSelectRule(string type, string name)
    {
        var rule = new SelectRule(new ElementHeader(
            "Diagnostic Owner",
            "Feature",
            "Test",
            "ID_TEST_DIAGNOSTIC_OWNER"));

        rule.Attributes.Type = type;
        rule.Attributes.Name = name;
        return rule;
    }
}
