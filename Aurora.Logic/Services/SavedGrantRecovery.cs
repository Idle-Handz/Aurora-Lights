using System.Xml.Linq;
using Builder.Data;
using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Services;

internal static class SavedGrantRecovery
{
    internal static ElementBase? FindEquivalentProxy(XElement savedRoot)
    {
        string savedId = (string?)savedRoot.Attribute("id") ?? "";
        string? marker = new[] { "_INTERNAL_ITEM_PROFICIENCY_PROXY_", "_INTERNAL_ITEM_LANGUAGE_PROXY_" }
            .SingleOrDefault(savedId.Contains);
        if (marker is null || (string?)savedRoot.Attribute("type") != "Item") return null;
        var children = savedRoot.Elements("element").ToArray();
        if (children.Length != 1 || children[0].Attribute("registered") is not null) return null;
        var catalog = DataManager.Current.ElementsCollection;
        var child = ElementIdAliases.Resolve(catalog, (string?)children[0].Attribute("id"));
        if (child is null || child.Type != (string?)children[0].Attribute("type")) return null;
        var restrictions = BuildSourceRestrictionSnapshot.CaptureCurrent();
        if (!restrictions.Allows(child)) return null;
        var matches = catalog.Where(e => e.Type == "Item" && e.Id.Contains(marker) &&
            e.Rules.Count == 1 && restrictions.Allows(e) &&
            e.GetGrantRules().SingleOrDefault()?.Attributes.Id == child.Id).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static ElementBase? FindEquivalentGrant(ElementBase? owner, string savedId)
    {
        var previous = DataManager.Current.ElementsCollection.GetElement(savedId);
        if (owner is null || previous is null) return null;
        string? signature = Definition(previous);
        if (signature is null) return null;
        var matches = owner.RuleElements.Where(e => e.Id != savedId && e.Aquisition.WasGranted &&
            e.Aquisition.GrantRule.ElementHeader.Id == owner.Id &&
            BuildSourceRestrictionSnapshot.CaptureCurrent().Allows(e) && Definition(e) == signature).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string? Definition(ElementBase element)
    {
        if (string.IsNullOrWhiteSpace(element.ElementNodeString)) return null;
        try
        {
            var xml = XElement.Parse(element.ElementNodeString);
            // Same authored behavior and wording, now granted from another source.
            // Names alone, absent old definitions, or changed rules cannot establish this.
            xml.Attribute("id")?.Remove();
            xml.Attribute("source")?.Remove();
            return xml.ToString(SaveOptions.DisableFormatting);
        }
        catch { return null; }
    }
}
