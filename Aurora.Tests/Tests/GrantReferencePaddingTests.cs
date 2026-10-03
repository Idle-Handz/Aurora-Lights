using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Extensions;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class GrantReferencePaddingTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    public void PaddedGrantReferenceResolvesWithoutRewritingTheDefinition(string attribute)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml($"""
            <element name="Reference owner" type="Feat" source="Internal" id="ID_GRANT_PADDING_OWNER">
              <rules><grant type="Proficiency" {attribute}="&#x9; ID_Grant_PADDING_TARGET &#xA;" /></rules>
            </element>
            """);
        string originalXml = document.OuterXml;
        string originalElementXml = document.DocumentElement!.ToElementString();
        var owner = new ElementParser().ParseElement(document.DocumentElement!);
        var rule = owner.GetGrantRules().Single();
        var target = new ElementBase("Target", "Proficiency", "Internal", "ID_Grant_PADDING_TARGET");
        var catalog = DataManager.Current.ElementsCollection;
        var priorPolicy = GrantPolicyContext.Current;
        catalog.Add(target);
        GrantPolicyContext.Current = null;
        try
        {
            var progression = new ProgressionManager { ProgressionLevel = 1 };
            progression.Process(owner);

            owner.RuleElements.Should().ContainSingle().Which.Should().BeSameAs(target);
            target.Aquisition.WasGranted.Should().BeTrue();
            target.Aquisition.GrantRule.Should().BeSameAs(rule);
            rule.Attributes.Id.Should().Be("ID_Grant_PADDING_TARGET");
            owner.Id.Should().Be("ID_GRANT_PADDING_OWNER");
            target.Id.Should().Be("ID_Grant_PADDING_TARGET");
            document.OuterXml.Should().Be(originalXml);
            owner.ElementNodeString.Should().Be(originalElementXml);
        }
        finally
        {
            owner.RuleElements.Clear();
            catalog.Remove(target);
            GrantPolicyContext.Current = priorPolicy;
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    public void ReferenceNormalizationDoesNotChangeCaseOrInteriorWhitespace(string attribute)
    {
        var document = new XmlDocument();
        document.LoadXml($"<element name='Owner' type='Feat' source='Internal' id='ID_OWNER'><rules><grant type='Proficiency' {attribute}=' ID_Mixed Case_TARGET ' /></rules></element>");

        var owner = new ElementParser().ParseElement(document.DocumentElement!);

        owner.GetGrantRules().Single().Attributes.Id.Should().Be("ID_Mixed Case_TARGET");
        document.SelectSingleNode("element/rules/grant")!.Attributes![attribute]!.Value
            .Should().Be(" ID_Mixed Case_TARGET ");
    }
}
