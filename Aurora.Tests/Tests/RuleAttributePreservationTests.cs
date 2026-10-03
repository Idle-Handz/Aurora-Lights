using Builder.Data;
using Builder.Data.Rules;
using Builder.Data.Rules.Parsers;
using System.Xml;

namespace Aurora.Tests.Tests;

/// <summary>
/// The parsers keep attributes they do not recognise instead of discarding them, so content using
/// an attribute this version does not know still carries it to whoever does.
/// </summary>
public sealed class RuleAttributePreservationTests
{
    private static XmlNode RuleNode(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement!;
    }

    private static readonly ElementHeader Owner = new("Test Feature", "Class Feature", "Test", "ID_TEST_FEATURE");

    [Fact]
    public void GrantRuleKeepsUnrecognizedAttributes()
    {
        var rule = (GrantRule)new GrantRuleParser().Parse(
            RuleNode("<grant type='Spell' id='ID_SPELL' spell-access='always-prepared' made-up='42' />"), Owner);

        rule.PreservedAttributes["spell-access"].Should().Be("always-prepared");
        rule.PreservedAttributes["made-up"].Should().Be("42");
        rule.Setters.ContainsSetter("spell-access").Should().BeFalse("spell attributes are no longer special-cased");
    }

    [Fact]
    public void SelectRuleKeepsUnrecognizedAttributesAndStillParsesKnownOnes()
    {
        var rule = (SelectRule)new SelectRuleParser().Parse(
            RuleNode("<select type='Spell' name='Cantrip' number='2' spell-slots='any' unknown-thing='x' />"), Owner);

        rule.Attributes.Name.Should().Be("Cantrip");
        rule.Attributes.Number.Should().Be(2);
        rule.PreservedAttributes["spell-slots"].Should().Be("any");
        rule.PreservedAttributes["unknown-thing"].Should().Be("x");
    }

    [Fact]
    public void RecognizedAttributesAreNotPreserved()
    {
        var rule = (SelectRule)new SelectRuleParser().Parse(
            RuleNode("<select type='Spell' name='Cantrip' prepared='true' />"), Owner);

        rule.PreservedAttributes.Should().BeEmpty();
        rule.Setters.ContainsSetter("prepared").Should().BeTrue();
    }
}
