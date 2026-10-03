using System.Xml;
using System.Xml.Linq;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class SavedGrantRecoveryTests
{
    [Fact]
    public void EquivalentGrantMustHaveIdenticalDefinitionAndBeGrantedByThisOwner()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var old = Parse("OLD", "Original", "Text", "1");
        var replacement = Parse("NEW", "Replacement", "Text", "1");
        var changed = Parse("CHANGED", "Replacement", "Text", "2");
        var parent = new ElementBase("Parent", "Background", "Internal", "PARENT");
        var catalog = DataManager.Current.ElementsCollection;
        catalog.Add(old);
        try
        {
            replacement.Aquisition.GrantedBy(new GrantRule(parent.ElementHeader));
            changed.Aquisition.GrantedBy(new GrantRule(parent.ElementHeader));
            parent.RuleElements.Add(changed);
            SavedGrantRecovery.FindEquivalentGrant(parent, old.Id).Should().BeNull("changed mechanics are not equivalent");
            parent.RuleElements.Add(replacement);
            SavedGrantRecovery.FindEquivalentGrant(parent, old.Id).Should().BeSameAs(replacement);
            replacement.Aquisition.GrantedBy(new GrantRule(new ElementHeader("Other", "Background", "Internal", "OTHER")));
            SavedGrantRecovery.FindEquivalentGrant(parent, old.Id).Should().BeNull("another owner's grant cannot cover this loss");
        }
        finally { catalog.Remove(old); }
    }

    [Fact]
    public void ProficiencyProxyFollowsExplicitUnderlyingAliasWhileLiveIdsKeepPrecedence()
    {
        const string old = "ID_DMG_PROFICIENCY_LASTER_PISTOL";
        const string target = "ID_DMG_PROFICIENCY_LASER_PISTOL";
        const string proxyOld = "ID_DMG_INTERNAL_ITEM_PROFICIENCY_PROXY_PROFICIENCY_LASTER_PISTOL";
        var proficiency = new ElementBase("Laser Pistol", "Proficiency", "Dungeon Master's Guide", target).Construct<Proficiency>();
        var source = new ElementBase("Dungeon Master's Guide", "Source", "Internal", "SOURCE").Construct<Source>();
        source.Abbreviation = "DMG";
        ElementIdAliases.Set(new Dictionary<string, string> { [old] = target });
        try
        {
            var generated = new InternalElementsGenerator().GenerateInternalProficiency([proficiency, source]);
            var catalog = new ElementBaseCollection();
            catalog.AddRange(generated);
            ElementIdAliases.Resolve(catalog, proxyOld).Should().BeSameAs(generated.Single());
            var liveOld = new ElementBase("Existing proxy", "Item", "Internal", proxyOld);
            catalog.Add(liveOld);
            ElementIdAliases.Resolve(catalog, proxyOld).Should().BeSameAs(liveOld);
        }
        finally { ElementIdAliases.Clear(); }
    }

    /// <summary>
    /// Proficiencies are one of five families the generator makes proxies for, and a save refers to
    /// the proxy rather than the definition underneath. Wiring the forwarding into only one family
    /// means an alias on a language, feat, ASI or spell never reaches the id a save actually holds.
    /// </summary>
    [Theory]
    [InlineData("Language", "ID_X_LANGUAGE_OLDTONGUE", "ID_X_LANGUAGE_NEWTONGUE",
        "ID_X_INTERNAL_ITEM_LANGUAGE_PROXY_LANGUAGE_OLDTONGUE")]
    // The feat format joins with a plain underscore, so its proxy id does not repeat the marker.
    [InlineData("Feat", "ID_X_FEAT_OLDNAME", "ID_X_FEAT_NEWNAME",
        "ID_X_INTERNAL_ITEM_FEAT_PROXY_OLDNAME")]
    public void EveryGeneratedProxyFamilyFollowsAnAliasOnTheDefinitionUnderneath(
        string type, string old, string target, string savedProxyId)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var definition = new ElementBase($"Renamed {type}", type, "Test Book", target);
        var source = new ElementBase("Test Book", "Source", "Internal", "SOURCE").Construct<Source>();
        source.Abbreviation = "X";
        ElementIdAliases.Set(new Dictionary<string, string> { [old] = target });
        try
        {
            var generator = new InternalElementsGenerator();
            var generated = type == "Language"
                ? generator.GenerateInternalLanguages([definition.Construct<Language>(), source])
                : generator.GenerateInternalFeats([definition.Construct<Feat>(), source]);
            var catalog = new ElementBaseCollection();
            catalog.AddRange(generated);

            ElementIdAliases.Resolve(catalog, savedProxyId).Should().BeSameAs(generated.Single(),
                "the saved proxy id has to reach the proxy generated from the new definition id");
        }
        finally { ElementIdAliases.Clear(); }
    }

    [Fact]
    public void ProxySourceRenameRequiresOneExactSavedGrantAndOneGeneratedReplacement()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var child = new ElementBase("Intimidation", "Proficiency", "Internal", "ID_PROXY_TEST_CHILD");
        var replacement = new Item { ElementHeader = new ElementHeader("Additional Proficiency", "Item", "Internal",
            "ID_NEW_INTERNAL_ITEM_PROFICIENCY_PROXY_TEST") };
        // The proxy generator stores the granted id in the obsolete Name field, and the recovery
        // under test reads it from there, so the fixture has to populate the same field.
#pragma warning disable CS0618
        replacement.Rules.Add(new GrantRule(replacement.ElementHeader) { Attributes = { Type = child.Type, Name = child.Id } });
#pragma warning restore CS0618
        var catalog = DataManager.Current.ElementsCollection;
        catalog.Add(child);
        catalog.Add(replacement);
        try
        {
            var saved = XElement.Parse("<element type='Item' id='ID_OLD_INTERNAL_ITEM_PROFICIENCY_PROXY_TEST'><element type='Proficiency' id='ID_PROXY_TEST_CHILD'/></element>");
            SavedGrantRecovery.FindEquivalentProxy(saved).Should().BeSameAs(replacement);
            saved.Element("element")!.SetAttributeValue("id", "UNKNOWN");
            SavedGrantRecovery.FindEquivalentProxy(saved).Should().BeNull();
            saved.Element("element")!.SetAttributeValue("id", child.Id);
            saved.SetAttributeValue("id", "ORDINARY_ITEM");
            SavedGrantRecovery.FindEquivalentProxy(saved).Should().BeNull();
        }
        finally
        {
            catalog.Remove(child);
            catalog.Remove(replacement);
        }
    }

    private static ElementBase Parse(string id, string source, string description, string value)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<element name='Same feature' type='Background Feature' source='{source}' id='{id}'><description><p>{description}</p></description><rules><stat name='speed' value='{value}'/></rules></element>");
        return new ElementParser().ParseElement(doc.DocumentElement!);
    }
}
