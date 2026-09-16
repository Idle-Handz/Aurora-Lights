using Aurora.App.Services;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;
using System.Xml;

internal static class FallbackRegressionChecks
{
    public static object Run()
    {
        var checks = new List<string>();
        var first = Parse("Original");
        var rule = first.Rules.OfType<SelectRule>().Single();
        XmlContentFallbackService.PrepareProjection(new[] { first })();
        Check(Label(rule) == "Original", "list lookup", checks);
        Check(XmlContentFallbackService.GetStartingEquipmentBlock(first.Id).FixedGold == 15,
            "starting equipment lookup", checks);
        first.ElementNode.SelectSingleNode("rules/select/item")!.InnerText = "Mutated";
        first.ElementNodeString = first.ElementNode.OuterXml;
        Check(Label(rule) == "Original", "live DOM and string mutation isolation", checks);

        var restore = XmlContentFallbackService.CaptureRestore();
        var second = Parse("Replacement");
        var publish = XmlContentFallbackService.PrepareProjection(new[] { second });
        Check(Label(rule) == "Original", "candidate staging does not publish", checks);
        publish();
        Check(Label(rule) == "Replacement", "candidate activation", checks);
        restore();
        Check(Label(rule) == "Original", "rollback restores previous index", checks);

        var materialize = new SelectRule(first.ElementHeader);
        materialize.Attributes.Type = "Feat";
        materialize.Attributes.Supports = "Fallback Fixture";
        var collection = DataManager.Current.ElementsCollection;
        collection.Clear();
        var recovered = XmlContentFallbackService.GetElementFallbacks(materialize).Single();
        Check(recovered.ContentFilePath == "fixture.xml", "materialization provenance", checks);
        recovered.ElementNode.SelectSingleNode("rules/select/item")!.InnerText = "Mutated recovered";
        collection.Clear();
        var recoveredAgain = XmlContentFallbackService.GetElementFallbacks(materialize).Single();
        Check(recoveredAgain.ElementNode.SelectSingleNode("rules/select/item")!.InnerText == "Original",
            "materialization does not mutate cached definition", checks);

        XmlContentFallbackService.PrepareProjection(Array.Empty<ElementBase>())();
        Check(XmlContentFallbackService.GetListFallbackOptions(rule).Count == 0,
            "excluded definition cannot reappear", checks);
        collection.Clear();
        XmlContentFallbackService.Invalidate();
        return new { passed = checks.Count, checks };
    }

    private static string Label(SelectRule rule) => XmlContentFallbackService.GetListFallbackOptions(rule).Single().Name;
    private static void Check(bool condition, string name, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException("Fallback regression: " + name);
        checks.Add(name);
    }

    private static ElementBase Parse(string label)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<element id='ID_FALLBACK_FIXTURE' name='Fixture' type='Feat' source='Fixture'><supports>Fallback Fixture</supports><starting-equipment><gold amount='15'/></starting-equipment><rules><select type='List' name='Fixture choice'><item id='1'>{label}</item></select></rules></element>");
        var element = new ElementParser().ParseElement(doc.DocumentElement!);
        element.ContentFilePath = "fixture.xml";
        return element;
    }
}
