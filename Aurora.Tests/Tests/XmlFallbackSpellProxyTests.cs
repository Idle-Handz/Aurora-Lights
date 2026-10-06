using System.Reflection;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using ApplicationContext = Builder.Presentation.ApplicationContext;

namespace Aurora.Tests.Tests;

public sealed class XmlFallbackSpellProxyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task XmlFallbackHonorsLazySpellProxiesAndKeepsEagerCompatibility(bool lazy)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var catalog = manager.ElementsCollection;
        var previousElements = catalog.ToArray();
        var previousPopulated = manager.IsElementsCollectionPopulated;
        var previousCustomPath = manager.UserDocumentsCustomElementsDirectory;
        var additionalDirectories = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        var previousAdditionalDirectories = additionalDirectories.ToArray();
        var previousDeveloperMode = ApplicationContext.Current.IsInDeveloperMode;
        var previousEnabled = SpellProxyCatalog.Enabled;
        // Other tests may already have a primed catalog with exclusions and materialized lists.
        var stateFields = new[] { "_catalog", "_listNames", "_unavailable" }
            .Select(name => typeof(SpellProxyCatalog).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!)
            .ToArray();
        var previousProxyState = stateFields.Select(field => field.GetValue(null)).ToArray();
        var materialized = (HashSet<string>)typeof(SpellProxyCatalog)
            .GetField("Materialized", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var previousMaterialized = materialized.ToArray();
        var customPath = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "XmlFallbackProxy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(customPath);

        using var aliasScope = ElementIdAliases.BeginScope();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(customPath, "fixture.xml"), """
                <elements>
                  <element name="Fallback Test Book" type="Source" source="Internal" id="ID_XFP_SOURCE">
                    <setters><set name="abbreviation">XFP</set><set name="url">https://example.test/xfp</set></setters>
                  </element>
                  <element name="Spark" type="Spell" source="Fallback Test Book" id="ID_XFP_SPELL_SPARK">
                    <setters><set name="level">0</set><set name="school">Evocation</set><set name="time">1 action</set><set name="duration">Instantaneous</set><set name="range">60 feet</set></setters>
                  </element>
                  <element name="Spellcasting" type="Class Feature" source="Fallback Test Book" id="ID_XFP_CF_SPELLCASTING">
                    <spellcasting name="Fallbackmage" ability="Intelligence"><list>Fallbackmage</list></spellcasting>
                  </element>
                </elements>
                """);
            typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!
                .SetValue(manager, customPath);
            additionalDirectories.Clear();
            ApplicationContext.Current.IsInDeveloperMode = false;
            SpellProxyCatalog.Reset();
            SpellProxyCatalog.Enabled = lazy;

            // Exercise the actual XML fallback, including embedded Core resources and finalization.
            await manager.InitializeElementDataAsync();

            manager.IsElementsCollectionPopulated.Should().BeTrue();
            catalog.GetElement("ID_XFP_SPELL_SPARK").Should().NotBeNull();
            catalog.Should().Contain(element => element.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_ABILITY_4"));
            const string namedId = "ID_XFP_INTERNAL_ITEM_FALLBACKMAGE_SPELL_PROXY_SPELL_SPARK";
            const string unnamedId = "ID_XFP_INTERNAL_ITEM__SPELL_PROXY_SPELL_SPARK";
            var fixtureProxies = catalog.Where(element => element.Id.StartsWith("ID_XFP_INTERNAL_ITEM_"));
            if (lazy)
            {
                fixtureProxies.Should().BeEmpty("the XML fallback must defer the spell/list cross product");
                SpellProxyCatalog.Categories.Should().Contain(["Additional Spell", "Additional Fallbackmage Spell"]);

                SpellProxyCatalog.ResolveOrBuild(catalog, namedId)!.Name.Should().Be("Additional Fallbackmage Spell, Spark");

                fixtureProxies.Should().ContainSingle();
                catalog.GetElement(unnamedId).Should().BeNull("only the requested category is materialized");
            }
            else
            {
                catalog.GetElement(namedId).Should().NotBeNull("disabling lazy proxies retains eager generation");
                catalog.GetElement(unnamedId).Should().NotBeNull();
            }
        }
        finally
        {
            catalog.Clear();
            catalog.AddRange(previousElements);
            typeof(DataManager).GetProperty(nameof(DataManager.IsElementsCollectionPopulated))!
                .SetValue(manager, previousPopulated);
            typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!
                .SetValue(manager, previousCustomPath);
            additionalDirectories.Clear();
            additionalDirectories.AddRange(previousAdditionalDirectories);
            ApplicationContext.Current.IsInDeveloperMode = previousDeveloperMode;
            SpellProxyCatalog.Enabled = previousEnabled;
            for (int i = 0; i < stateFields.Length; i++)
                stateFields[i].SetValue(null, previousProxyState[i]);
            materialized.Clear();
            materialized.UnionWith(previousMaterialized);
            Directory.Delete(customPath, recursive: true);
        }
    }
}
