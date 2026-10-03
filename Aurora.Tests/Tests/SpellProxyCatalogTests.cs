using System.Xml;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

/// <summary>
/// The "Additional &lt;list&gt; Spell" proxies are a cross product of every spell and every
/// spellcasting list - 79,032 items for the shipped catalog, measured at roughly eleven seconds and
/// four hundred megabytes of a twenty-two second content load. They are built per category on first
/// use instead. What that must not break: the picker still listing every category, a saved character
/// still resolving a proxy it names directly, and a deliberately unavailable identity staying gone.
/// </summary>
public sealed class SpellProxyCatalogTests : IDisposable
{
    // Resolved in the constructor, not in a field initializer: touching DataManager before the
    // application context is installed throws from its static initializer.
    private readonly ElementBaseCollection catalog;
    private readonly ElementBase[] seeded;

    public SpellProxyCatalogTests()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        catalog = DataManager.Current.ElementsCollection;
        seeded =
        [
            Parse("""
                <element name="Test Book" type="Source" source="Internal" id="ID_SPX_SOURCE">
                  <setters>
                    <set name="abbreviation">SPX</set>
                    <set name="url">https://example.test/spx</set>
                  </setters>
                </element>
                """),
            Parse("""
                <element name="Acid Splash" type="Spell" source="Test Book" id="ID_SPX_SPELL_ACID_SPLASH">
                  <setters><set name="level">0</set><set name="school">Conjuration</set><set name="time">1 action</set><set name="duration">Instantaneous</set><set name="range">60 feet</set></setters>
                </element>
                """),
            Parse("""
                <element name="Fireball" type="Spell" source="Test Book" id="ID_SPX_SPELL_FIREBALL">
                  <setters><set name="level">0</set><set name="school">Conjuration</set><set name="time">1 action</set><set name="duration">Instantaneous</set><set name="range">60 feet</set></setters>
                </element>
                """),
            Parse("""
                <element name="Spellcasting" type="Class Feature" source="Test Book" id="ID_SPX_CF_WIZARD">
                  <spellcasting name="Testmage" ability="Intelligence"><list>Testmage</list></spellcasting>
                </element>
                """),
        ];
        foreach (var element in seeded) catalog.Add(element);
    }

    public void Dispose()
    {
        SpellProxyCatalog.Reset();
        foreach (var element in catalog.Where(IsGeneratedProxy).ToArray()) catalog.Remove(element);
        foreach (var element in seeded) catalog.Remove(element);
    }

    private static bool IsGeneratedProxy(ElementBase e) =>
        (e.Id ?? "").Contains("_SPELL_PROXY_", StringComparison.Ordinal);

    // Materializing a list builds a proxy for every spell in the shared catalog, and a full test
    // run has real content loaded in it. Counting only this fixture's own source keeps these
    // assertions about the behaviour under test rather than about who ran first.
    private static bool IsMine(ElementBase e) =>
        (e.Id ?? "").StartsWith("ID_SPX_INTERNAL_ITEM_", StringComparison.Ordinal);

    private IEnumerable<ElementBase> MineInCatalog() => catalog.Where(IsMine);

    private static IEnumerable<string> MyResults(IEnumerable<ItemSearchResult> results) =>
        results.Where(r => r.Id.StartsWith("ID_SPX_INTERNAL_ITEM_", StringComparison.Ordinal))
            .Select(r => r.Name);

    private void Prime(IEnumerable<string>? unavailable = null) =>
        SpellProxyCatalog.Prime(catalog,
            new InternalElementsGenerator().GetSpellcastingListNames(catalog), unavailable);

    /// <summary>
    /// The point of the change: priming costs nothing, and the picker's dropdown is still complete.
    /// </summary>
    [Fact]
    public void PrimingOffersEveryCategoryWithoutBuildingAnything()
    {
        Prime();

        MineInCatalog().Should().BeEmpty("priming must not build the cross product");
        SpellProxyCatalog.Categories.Should().Contain(["Additional Spell", "Additional Testmage Spell"]);
        EquipmentService.GetCustomFeatureCategories()
            .Should().Contain(["Additional Spell", "Additional Testmage Spell"],
                "the picker lists categories that have not been built yet");
    }

    /// <summary>
    /// Searching a category builds that category and no other, and repeating it does not stack a
    /// second copy of every proxy onto the catalog.
    /// </summary>
    [Fact]
    public void SearchingACategoryBuildsOnlyThatCategoryAndIsIdempotent()
    {
        Prime();

        var results = EquipmentService.SearchCustomFeatures("Additional Testmage Spell", "");

        MyResults(results).Should().BeEquivalentTo(["Acid Splash", "Fireball"]);
        MineInCatalog().Should().HaveCount(2, "only the requested list is built");
        MineInCatalog().Should().OnlyContain(e => e.Id.Contains("TESTMAGE"));

        EquipmentService.SearchCustomFeatures("Additional Testmage Spell", "");
        MineInCatalog().Should().HaveCount(2, "building twice must not duplicate");

        EquipmentService.SearchCustomFeatures("Additional Spell", "");
        MineInCatalog().Should().HaveCount(4, "a second category adds its own");
    }

    /// <summary>
    /// A saved character can name a proxy directly - one character in the sixty-character corpus
    /// carries "Additional Spell, Acid Splash" as an item in its build tree. Nothing opens the
    /// picker during a load, so resolution has to build the category itself rather than report the
    /// element as lost.
    /// </summary>
    [Fact]
    public void ASavedProxyReferenceResolvesByBuildingItsCategory()
    {
        Prime();
        const string savedId = "ID_SPX_INTERNAL_ITEM__SPELL_PROXY_SPELL_ACID_SPLASH";
        catalog.GetElement(savedId).Should().BeNull("nothing has built it yet");

        var resolved = ElementIdAliases.Resolve(catalog, savedId);

        resolved.Should().NotBeNull("a saved reference must build what it names");
        resolved!.Id.Should().Be(savedId);
        resolved.Name.Should().Be("Additional Spell, Acid Splash");
    }

    /// <summary>
    /// The regression this caught: the loader's saved-item path used a plain <c>GetElement</c>, not
    /// the alias resolver, so a deferred proxy was reported as a lost element on first load. Every
    /// catalog lookup on a load path goes through ResolveOrBuild now, and this covers it directly
    /// rather than through only one of its callers.
    /// </summary>
    [Fact]
    public void ResolveOrBuildIsTheOneFunnelEveryLoadPathCanUse()
    {
        Prime();
        const string savedId = "ID_SPX_INTERNAL_ITEM__SPELL_PROXY_SPELL_ACID_SPLASH";
        catalog.GetElement(savedId).Should().BeNull("a plain lookup is exactly what regressed");

        SpellProxyCatalog.ResolveOrBuild(catalog, savedId)!.Id.Should().Be(savedId);

        // Already-present ids and ids that are not proxies both pass straight through.
        SpellProxyCatalog.ResolveOrBuild(catalog, savedId)!.Id.Should().Be(savedId);
        SpellProxyCatalog.ResolveOrBuild(catalog, "ID_SPX_SPELL_FIREBALL")!.Name.Should().Be("Fireball");
        SpellProxyCatalog.ResolveOrBuild(catalog, "ID_NOT_A_THING").Should().BeNull();
        SpellProxyCatalog.ResolveOrBuild(catalog, null).Should().BeNull();
    }

    /// <summary>
    /// Only the primed catalog may grow. A projection or a test passing its own collection must not
    /// have generated content quietly added to it.
    /// </summary>
    [Fact]
    public void AnUnprimedCollectionIsNeverGrown()
    {
        Prime();
        var other = new ElementBaseCollection();

        ElementIdAliases.Resolve(other, "ID_SPX_INTERNAL_ITEM__SPELL_PROXY_SPELL_ACID_SPLASH")
            .Should().BeNull();

        other.Should().BeEmpty();
        MineInCatalog().Should().BeEmpty("the primed catalog was not the one asked");
    }

    /// <summary>
    /// Eager generation was followed by a sweep that removed deliberately unavailable identities.
    /// Building later has to honour the same exclusion, or lazily is a way back in.
    /// </summary>
    [Fact]
    public void AnUnavailableIdentityStaysGoneWhenBuiltLater()
    {
        const string excluded = "ID_SPX_INTERNAL_ITEM_TESTMAGE_SPELL_PROXY_SPELL_FIREBALL";
        Prime([excluded]);

        var results = EquipmentService.SearchCustomFeatures("Additional Testmage Spell", "");

        MyResults(results).Should().BeEquivalentTo(["Acid Splash"]);
        catalog.GetElement(excluded).Should().BeNull("an excluded identity must not come back");
        ElementIdAliases.Resolve(catalog, excluded).Should().BeNull();
    }

    /// <summary>
    /// A content refresh replaces the catalog. Whatever the previous one had built must not count as
    /// built against the new one, or the new catalog silently lacks a category it claims to offer.
    /// </summary>
    [Fact]
    public void ReprimingForgetsWhatThePreviousCatalogBuilt()
    {
        Prime();
        EquipmentService.SearchCustomFeatures("Additional Testmage Spell", "");
        MineInCatalog().Should().HaveCount(2);
        foreach (var element in catalog.Where(IsGeneratedProxy).ToArray()) catalog.Remove(element);

        Prime();

        EquipmentService.SearchCustomFeatures("Additional Testmage Spell", "");
        MineInCatalog().Should().HaveCount(2, "a reprimed catalog builds again");
    }

    private static ElementBase Parse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var node = doc.DocumentElement!;
        var parsers = ElementParserFactory.GetParsers().ToList();
        var fallback = new ElementParser();
        var header = fallback.ParseElementHeader(node);
        var parser = parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? fallback;
        return parser.ParseElement(node);
    }
}
