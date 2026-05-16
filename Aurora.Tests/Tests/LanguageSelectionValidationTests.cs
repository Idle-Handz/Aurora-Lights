using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Tests the supports-expression evaluation and language-selection validation logic that
/// sits behind the "Customized Language" invalidation bug.  The first group are pure-unit
/// (no database); the second group require Aurora content and skip gracefully when absent.
/// </summary>
public sealed class LanguageSelectionValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    public LanguageSelectionValidationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── DynamicExpressionConverter (pure unit — no database) ─────────────────

    [Fact]
    public void ConvertSupports_Standard_ProducesContainsExpression()
    {
        var converter = new DynamicExpressionConverter();
        var result = converter.ConvertSupportsExpression("Standard");
        result.Should().Be("element.Supports.Contains(\"Standard\")",
            because: "a plain supports token must become an exact-match Contains call");
    }

    [Fact]
    public void ConvertSupports_Starting_ProducesContainsExpression()
    {
        var converter = new DynamicExpressionConverter();
        var result = converter.ConvertSupportsExpression("Starting");
        result.Should().Be("element.Supports.Contains(\"Starting\")",
            because: "the Human language-rule supports='Starting' must convert to an exact Contains");
    }

    [Fact]
    public void ConvertSupports_Comma_BecomesAndExpression()
    {
        var converter = new DynamicExpressionConverter();
        var result = converter.ConvertSupportsExpression("Standard,Starting");
        // comma → && before token replacement, so we get two Contains joined by &&
        result.Should().Contain("Contains(\"Standard\")")
            .And.Contain("Contains(\"Starting\")");
    }

    [Fact]
    public void ConvertSupports_Pipe_BecomesOrExpression()
    {
        var converter = new DynamicExpressionConverter();
        var result = converter.ConvertSupportsExpression("Standard||Starting");
        result.Should().Contain("Contains(\"Standard\")")
            .And.Contain("Contains(\"Starting\")")
            .And.Contain("||");
    }

    // ── Integration — requires Aurora content database ────────────────────────

    [Fact]
    public void Language_Elements_HaveStandardSupport_InElementsCollection()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elvish = DataManager.Current.ElementsCollection.GetElement("ID_LANGUAGE_ELVISH");
        elvish.Should().NotBeNull("Elvish must exist in the database");
        elvish!.Supports.Should().Contain("Standard",
            because: "after DataManager post-processing, Elvish must have 'Standard' in its Supports list");
        elvish.Supports.Should().Contain("Starting",
            because: "after DataManager post-processing, Elvish must have 'Starting' in its Supports list");
    }

    [Fact]
    public void ExpressionInterpreter_Standard_Filter_ReturnsStandardLanguages()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var allLanguages = DataManager.Current.ElementsCollection
            .Where(e => e.Type == "Language")
            .ToList();

        allLanguages.Should().NotBeEmpty("the database must contain Language elements");

        var interpreter = new ExpressionInterpreter();
        var standard = interpreter.EvaluateSupportsExpression<Builder.Data.ElementBase>(
            "Standard", allLanguages, containsElementIDs: false).ToList();

        standard.Should().NotBeEmpty("'Standard' filter must match at least Common, Elvish, Dwarvish…");
        standard.Should().Contain(e => e.Id == "ID_LANGUAGE_ELVISH",
            because: "Elvish is a Standard language");
        standard.Should().Contain(e => e.Id == "ID_LANGUAGE_COMMON",
            because: "Common is a Standard language");
        standard.Should().NotContain(e => e.Id == "ID_LANGUAGE_ABYSSAL",
            because: "Abyssal is Exotic, not Standard");
    }

    [Fact]
    public void ExpressionInterpreter_Starting_Filter_ReturnsStartingLanguages()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var allLanguages = DataManager.Current.ElementsCollection
            .Where(e => e.Type == "Language")
            .ToList();

        var interpreter = new ExpressionInterpreter();
        var starting = interpreter.EvaluateSupportsExpression<Builder.Data.ElementBase>(
            "Starting", allLanguages, containsElementIDs: false).ToList();

        starting.Should().NotBeEmpty("'Starting' filter must match the standard/starting languages");
        starting.Should().Contain(e => e.Id == "ID_LANGUAGE_ELVISH",
            because: "Elvish has the 'Starting' support tag");
        starting.Should().NotContain(e => e.Id == "ID_LANGUAGE_SYLVAN",
            because: "Sylvan is Exotic and should not appear under 'Starting'");
    }

    [Fact]
    public async Task Language_Selection_SurvivesReprocessCharacter()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        // Set up a minimal MAUI context stub so CharacterManager.New() doesn't hang
        // on the WPF expander loop (GetExpandersCount returns 0 immediately).
        var handler = new NoOpExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;

        await CharacterManager.Current.New(initializeFirstLevel: true);

        // Register the Custom Language option so rule requirements pass.
        var option = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id == "ID_WOTC_TCOE_OPTION_CUSTOMIZED_LANGUAGE");
        if (option != null)
            CharacterManager.Current.RegisterElement(option);

        // Register Human race — this adds the Language (Human) select rule.
        var human = DataManager.Current.ElementsCollection.GetElement("ID_RACE_HUMAN");
        if (human == null) { _output.WriteLine("[SKIP] Human race element not found."); return; }
        CharacterManager.Current.RegisterElement(human);

        // Verify the Language (Human) rule exists in SelectionRules.
        var languageRule = CharacterManager.Current.SelectionRules
            .FirstOrDefault(r => r.Attributes.Type == "Language" && r.ElementHeader?.Id == "ID_RACE_HUMAN");
        languageRule.Should().NotBeNull("registering Human must add a Language select rule");

        // Now simulate the user selecting Elvish.
        var elvish = DataManager.Current.ElementsCollection.GetElement("ID_LANGUAGE_ELVISH");
        elvish.Should().NotBeNull("Elvish must exist");
        CharacterManager.Current.RegisterElement(elvish!);

        // Reprocess (same as step 2 in ApplySelectionAndSaveAsync).
        CharacterManager.Current.ReprocessCharacter();

        // Elvish must still be in GetElements() after reprocessing.
        var elements = CharacterManager.Current.GetElements();
        elements.Should().Contain(e => e.Id == "ID_LANGUAGE_ELVISH",
            because: "Elvish must remain registered after ReprocessCharacter");

        // The Language rule must still be in SelectionRules.
        var ruleAfter = CharacterManager.Current.SelectionRules
            .FirstOrDefault(r => r.Attributes.Type == "Language" && r.ElementHeader?.Id == "ID_RACE_HUMAN");
        ruleAfter.Should().NotBeNull(
            "the Language (Human) select rule must persist through ReprocessCharacter");

        // Simulate what GetValidSelectionIds does: evaluate supports expression and check Elvish is valid.
        var allLangs = DataManager.Current.ElementsCollection.Where(e => e.Type == "Language");
        var interpreter = new ExpressionInterpreter();
        interpreter.InitializeWithSelectionRule(languageRule!);
        var supported = interpreter.EvaluateSupportsExpression<Builder.Data.ElementBase>(
            languageRule!.Attributes.Supports, allLangs, containsElementIDs: false).ToList();

        supported.Should().Contain(e => e.Id == "ID_LANGUAGE_ELVISH",
            because: "Elvish must be in the valid options for the Language (Human) rule after selection");
    }

    // ── No-op expander handler so CharacterManager.New() can complete ─────────

    private sealed class NoOpExpanderHandler : Builder.Presentation.Interfaces.ISelectionRuleExpanderHandler
    {
        private readonly Dictionary<string, object> _reg = new();
        public void RegisterSupport(Builder.Presentation.Interfaces.ISupportExpanders support) { }
        public bool HasExpander(string uniqueIdentifier) => true;
        public bool HasExpander(string uniqueIdentifier, int number) => true;
        public object GetRegisteredElement(Builder.Data.Rules.SelectRule selectionRule, int number = 1)
        {
            _reg.TryGetValue($"{selectionRule.UniqueIdentifier}:{number}", out var v);
            return v!;
        }
        public void SetRegisteredElement(Builder.Data.Rules.SelectRule selectionRule, string id, int number = 1)
            => _reg[$"{selectionRule.UniqueIdentifier}:{number}"] = id;
        public void ClearRegisteredElement(Builder.Data.Rules.SelectRule selectionRule, int number = 1)
            => _reg.Remove($"{selectionRule.UniqueIdentifier}:{number}");
        public int GetExpandersCount() => 0;
        public void FocusExpander(Builder.Data.Rules.SelectRule rule, int number = 1) { }
        public void RetrainSpellExpander(Builder.Data.Rules.SelectRule rule, int number, int retrainLevel) { }
        public void RemoveAllExpanders() => _reg.Clear();
        public bool RequiresSelection(Builder.Data.Rules.SelectRule rule, int number = 1) => false;
        public int GetRetrainLevel(Builder.Data.Rules.SelectRule rule, int number) => 0;
    }
}
