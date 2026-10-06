using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class BuildSelectionTrustTests : IDisposable
{
    private const string OptionType = "Content Trust Test Option";
    private readonly ElementBase _candidate = new("Blocked option", OptionType, "Test", "ID_CONTENT_TRUST_OPTION");

    public BuildSelectionTrustTests()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        _candidate.Supports.Add("Blocked");
        DataManager.Current.ElementsCollection.Add(_candidate);
    }

    [Theory]
    [InlineData("!Blocked")]
    [InlineData("Blocked,Missing")]
    [InlineData("Missing")]
    public void ValidEmptySupportsResultCannotBeReplacedByHeuristicOrHostFallback(string supports)
    {
        var rule = Rule(supports);
        var interpreter = new ExpressionInterpreter();
        interpreter.InitializeWithSelectionRule(rule);
        interpreter.EvaluateSupportsExpression(supports, new[] { _candidate }).Should().BeEmpty();
        // Prove this synthetic catalog can produce options; an unrelated swallowed error
        // must not make the negative assertions below pass accidentally.
        BuildSelectionOptionResolver.ResolveOptions(Rule("Blocked"))
            .Should().ContainSingle(option => option.Id == _candidate.Id);
        bool fallbackCalled = false;

        var options = BuildSelectionOptionResolver.ResolveOptions(rule,
            settings: new BuildSelectionOptionResolverSettings
            {
                ElementFallbackProvider = _ =>
                {
                    fallbackCalled = true;
                    return [_candidate];
                }
            });

        options.Should().BeEmpty("a valid exclusion must remain authoritative even when every option is excluded");
        fallbackCalled.Should().BeFalse("host recovery must not override a successfully evaluated rule");
    }

    [Fact]
    public void MalformedSupportsKeepsTheExistingRecoveryBehavior()
    {
        var options = BuildSelectionOptionResolver.ResolveOptions(Rule("blocked && ("));

        options.Should().ContainSingle(option => option.Id == _candidate.Id);
    }

    [Fact]
    public void ValidEmptySpellSupportsCannotRecoverTheEntireSpellList()
    {
        var spell = new ElementBase("Test cantrip", "Spell", "Test", "ID_CONTENT_TRUST_CANTRIP");
        spell.Supports.Add("TrustWizard");
        DataManager.Current.ElementsCollection.Add(spell);
        try
        {
            var rule = Rule("TrustWizard,Missing");
            rule.Attributes.Type = "Spell";
            rule.Attributes.Name = "Cantrip";
            rule.Attributes.SpellcastingName = "TrustWizard";
            bool fallbackCalled = false;

            var options = BuildSelectionOptionResolver.ResolveOptions(rule,
                settings: new BuildSelectionOptionResolverSettings
                {
                    SpellAccessMap = new Dictionary<string, IReadOnlySet<string>>
                    {
                        ["TrustWizard"] = new HashSet<string> { spell.Id }
                    },
                    ElementFallbackProvider = _ =>
                    {
                        fallbackCalled = true;
                        return [spell];
                    }
                });

            options.Should().BeEmpty("list membership cannot replace the other authored supports conditions");
            fallbackCalled.Should().BeFalse();
        }
        finally { DataManager.Current.ElementsCollection.Remove(spell); }
    }

    private static SelectRule Rule(string supports)
    {
        var rule = new SelectRule(new ElementHeader("Test", "Feature", "Test", "ID_CONTENT_TRUST_OWNER"));
        rule.Attributes.Type = OptionType;
        rule.Attributes.Name = "Choose an option";
        rule.Attributes.Supports = supports;
        return rule;
    }

    public void Dispose() => DataManager.Current.ElementsCollection.Remove(_candidate);
}
