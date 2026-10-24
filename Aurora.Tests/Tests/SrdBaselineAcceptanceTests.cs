using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Models.CharacterSheet.Pages;
using Builder.Presentation.Models.CharacterSheet.Pages.Content;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace Aurora.Tests.Tests;

public sealed class SrdBaselineAcceptanceTests
{
    private static ElementBase[] ParseBaseline()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var fallback = new ElementParser();
        var parsers = ElementParserFactory.GetParsers().ToArray();
        return DataManager.Current.LoadElementDocumentsFromResource()
            .SelectMany(d => d.SelectNodes("/elements/element")!.Cast<XmlNode>())
            .Select(n => (parsers.FirstOrDefault(p => p.ParserType == n.Attributes!["type"]!.Value) ?? fallback).ParseElement(n))
            .ToArray();
    }

    [Fact]
    public void BaselineParsesWithSrdCatalogsAndNoExcludedPlayableDefinitions()
    {
        var elements = ParseBaseline();
        elements.Should().HaveCount(1854);
        elements.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        elements.Count(e => e.Type == "Class").Should().Be(12);
        elements.Count(e => e.Type == "Archetype").Should().Be(12);
        elements.Count(e => e.Type == "Race").Should().Be(9);
        elements.Count(e => e.Type == "Sub Race").Should().Be(4);
        elements.Count(e => e.Type == "Magic Item").Should().Be(343);
        elements.Count(e => e.Type == "Deity").Should().Be(67);
        elements.OfType<Spell>().Should().HaveCount(319);
        foreach (string id in new[] {
            "ID_PHB_SPELL_ARCANE_GATE", "ID_PHB_SPELL_HEX", "ID_RACE_VARIANT_HUMAN_VARIANT",
            "ID_RACIAL_TRAIT_DROW_MAGIC", "ID_WOTC_PHB_ARCHETYPE_FEATURE_CIRCLE_SPELLS_UNDERDARK",
            "ID_WOTC_PHB_ITEM_TOOL_DRAGONCHESS_SET", "ID_WOTC_PHB_ITEM_TOOL_THREE_DRAGON_ANTE_SET" })
            elements.Should().NotContain(e => e.Id == id);
        foreach (string name in new[] { "Restorative Ointment", "Efficient Quiver", "Instant Fortress", "Handy Haversack",
            "Iron Bands of Binding", "Feather Token", "Marvelous Pigments", "Apparatus of the Crab" })
            elements.Should().Contain(e => e.Type == "Magic Item" && e.Name == name);
        elements.Where(e => e.Type == "Deity").Should().OnlyContain(e => e.Id.StartsWith("ID_SRD51_DEITY_"));
        elements.OfType<Spell>().Single(s => s.Id == "ID_PHB_SPELL_WIND_WALK").CastingTime.Should().Be("1 minute");
        var grappler = elements.Single(e => e.Id == "ID_PHB_FEAT_GRAPPLER");
        Regex.Matches(grappler.Description, @"<li(?:\s|>)").Count.Should().Be(2);
        grappler.Description.Should().NotContain("one size larger");
        elements.Single(e => e.Id == "ID_RACIAL_TRAIT_INFERNAL_LEGACY").Description
            .Should().Contain("long rest").And.NotContain("per day");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverySrdSpellExportsWithItsCompleteEndingInBothCardModes(bool editable)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var spells = ParseBaseline().OfType<Spell>().OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Aurora.Tests", "srd-deck"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "all-srd-spells.pdf");
            using (var cards = new CardPageGenerator { Flatten = !editable })
            {
                foreach (var spell in spells)
                    cards.AddSpellCard(new SpellCardContent(spell.Name, spell.GetShortDescription())
                    {
                        CastingTime = spell.CastingTime, Range = spell.Range, Duration = spell.Duration,
                        Components = spell.GetComponentsString(), DescriptionHtml = spell.Description
                    });
                cards.Save(path);
            }
            if (Environment.GetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY") is { } qa)
            {
                Directory.CreateDirectory(qa);
                File.Copy(path, Path.Combine(qa, $"all-srd-spells-{editable}.pdf"), overwrite: true);
            }
            using var pdf = PdfDocument.Open(path);
            pdf.NumberOfPages.Should().Be(36);
            var pages = pdf.GetPages().Select(p => Normalize(p.Text)).ToArray();
            for (int index = 0; index < spells.Length; index++)
            {
                var spell = spells[index];
                string description = Normalize(WebUtility.HtmlDecode(Regex.Replace(spell.Description, "<[^>]+>", " ")));
                pages[index / 9].Should().Contain(Normalize(spell.Name), spell.Id);
                string ending = description[^Math.Min(65, description.Length)..];
                Assert.True(pages[index / 9].Contains(ending),
                    $"The ending of {spell.Name} is missing from card page {index / 9 + 1}: {ending}");
                if (spell.Id == "ID_PHB_SPELL_CLONE")
                {
                    pages[index / 9].Should().Contain("vsmdetailsbelow");
                    pages[index / 9].Should().Contain(Normalize(spell.GetComponentsString()),
                        "the full material requirements must remain available in the readable description");
                }
            }
        }
        finally
        {
            Path.GetDirectoryName(Path.GetFullPath(root)).Should().Be(parent);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MissingRemovedSpellIsReportedWithoutChangingSaveAndImportedDefinitionRestoresIt()
    {
        await ContentFixture.EnsureAvailableAsync();
        ContentFixture.SkipIfUnavailable();
        const string id = "ID_PHB_SPELL_HEX";
        DataManager.Current.ElementsCollection.HasElement(id).Should().BeFalse();
        var document = new XmlDocument();
        // Original synthetic content using an old identifier, not the excluded book text.
        document.LoadXml("""
            <element name="Imported acceptance spell" type="Spell" source="Synthetic acceptance" id="ID_PHB_SPELL_HEX">
              <supports>Wizard</supports><description><p>Original imported content for this isolated test.</p></description>
              <setters><set name="level">1</set><set name="school">Evocation</set><set name="time">1 action</set>
              <set name="range">30 feet</set><set name="duration">Instantaneous</set><set name="hasVerbalComponent">true</set></setters>
            </element>
            """);
        var imported = ElementParserFactory.GetParsers().Single(p => p.ParserType == "Spell")
            .ParseElement(document.DocumentElement!);
        DataManager.Current.ElementsCollection.Add(imported);
        var manager = CharacterManager.Current;
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Aurora.Tests", "srd-import"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await manager.New(true);
            manager.Character.Name = "Import preservation acceptance";
            manager.Character.Abilities.Intelligence.BaseScore = 17;
            var handler = SelectionRuleExpanderContext.Current!;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_WOTC_PHB_CLASS_WIZARD");
            manager.ReprocessCharacter();
            var slot = manager.SelectionRules.First(r => r.Attributes.Type == "Spell" && r.Attributes.Name.StartsWith("Spellbook"));
            handler.SetRegisteredElement(slot, id);
            manager.ReprocessCharacter();
            manager.GetElements().Should().Contain(e => e.Id == id);
            string path = Path.Combine(root, "existing-imported-character.dnd5e");
            new CharacterFile(path).Save(manager.Character).Should().BeTrue();
            byte[] saved = File.ReadAllBytes(path);
            DataManager.Current.ElementsCollection.Remove(imported);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var partial = await new CharacterFile(path).Load();
            partial.Success.Should().BeFalse();
            partial.Missing.Should().Contain(m => m.Id == id);
            File.ReadAllBytes(path).Should().Equal(saved, "loading must not rewrite or discard the original save");

            DataManager.Current.ElementsCollection.Add(imported);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var restored = await new CharacterFile(path).Load();
            restored.Success.Should().BeTrue(restored.Message);
            manager.GetElements().Should().Contain(e => e.Id == id && e.Name == "Imported acceptance spell");
            manager.Character.Name.Should().Be("Import preservation acceptance");
            manager.Character.Abilities.Intelligence.BaseScore.Should().Be(17);
            File.ReadAllBytes(path).Should().Equal(saved);
        }
        finally
        {
            await manager.New(false);
            DataManager.Current.ElementsCollection.Remove(imported);
            Path.GetDirectoryName(Path.GetFullPath(root)).Should().Be(parent);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task InventoryWithLegacyMagicItemNamesLoadsUsingThePreservedIdentifiers()
    {
        await ContentFixture.EnsureAvailableAsync();
        ContentFixture.SkipIfUnavailable();
        var manager = CharacterManager.Current;
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Aurora.Tests", "srd-inventory"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var items = new[] {
            (Id: "ID_WOTC_DMG_MAGIC_ITEM_KEOGHTOMS_OINTMENT", Current: "Restorative Ointment", Old: "Keoghtom's Ointment"),
            (Id: "ID_WOTC_DMG_MAGIC_ITEM_HEWARDS_HANDY_HAVERSACK", Current: "Handy Haversack", Old: "Heward's Handy Haversack")
        };
        try
        {
            await manager.New(true);
            manager.Character.Name = "Legacy inventory acceptance";
            var handler = SelectionRuleExpanderContext.Current!;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_WOTC_PHB_CLASS_WIZARD");
            manager.ReprocessCharacter();
            foreach (var item in items)
                manager.Character.Inventory.Items.Add(new RefactoredEquipmentItem(
                    DataManager.Current.ElementsCollection.GetElement(item.Id).Should().BeAssignableTo<Item>().Subject) { Amount = 2 });
            string path = Path.Combine(root, "legacy-inventory.dnd5e");
            new CharacterFile(path).Save(manager.Character).Should().BeTrue();
            var saved = new XmlDocument();
            saved.Load(path);
            foreach (XmlNode node in saved.SelectNodes("//@* | //text()")!)
                foreach (var item in items)
                    if (node.Value is { } value) node.Value = value.Replace(item.Current, item.Old, StringComparison.Ordinal);
            saved.Save(path);
            byte[] original = File.ReadAllBytes(path);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var result = await new CharacterFile(path).Load();
            result.Success.Should().BeTrue(result.Message);
            foreach (var item in items)
            {
                var restored = manager.Character.Inventory.Items.Single(row => row.Item.Id == item.Id);
                restored.Item.Name.Should().Be(item.Current);
                restored.Amount.Should().Be(2);
            }
            File.ReadAllBytes(path).Should().Equal(original);
        }
        finally
        {
            await manager.New(false);
            Path.GetDirectoryName(Path.GetFullPath(root)).Should().Be(parent);
            Directory.Delete(root, true);
        }
    }

    private static string Normalize(string text) => Regex.Replace(text, "[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    [Fact]
    public void FormattedCardListsUseOneMarkerPerItemAndPreserveNumbering()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        using var cards = new CardPageGenerator { Flatten = true };
        cards.AddGenericCard(new GenericCardContent("List acceptance", "Feature")
        {
            DescriptionHtml = "<ul><li>First <b>bold</b> item.</li><li>Second <i>italic</i> item.</li></ul>" +
                "<ol><li>Numbered item.</li><li>Another numbered item.</li></ol>"
        });
        using var pdf = PdfDocument.Open(cards.GetStream().ToArray());
        string text = pdf.GetPage(1).Text;
        text.Count(c => c == '\u2022').Should().Be(2);
        Normalize(text).Should().Contain("firstbolditem").And.Contain("seconditalicitem")
            .And.Contain("1numbereditem").And.Contain("2anothernumbereditem");
    }
}
