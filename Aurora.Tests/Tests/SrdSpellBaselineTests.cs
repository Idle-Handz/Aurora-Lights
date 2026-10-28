using System.Text;
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
using iTextSharp.text.pdf;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace Aurora.Tests.Tests;

public sealed class SrdSpellBaselineTests
{
    [Fact]
    public void EmbeddedSpellCatalogParsesAs319SpellsWithCompatibleRenamedIdentifiers()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var parser = ElementParserFactory.GetParsers().Single(p => p.ParserType == "Spell");
        var spells = DataManager.Current.LoadElementDocumentsFromResource()
            .SelectMany(d => d.SelectNodes("/elements/element[@type='Spell']")!.Cast<XmlNode>())
            .Select(n => (Spell)parser.ParseElement(n)).ToArray();
        spells.Should().HaveCount(319);
        spells.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        spells.Should().NotContain(s => s.Name == "Arcane Gate" || s.Name == "Hex" || s.Name == "Friends");
        foreach (var (id, name) in new Dictionary<string, string>
        {
            ["ID_PHB_SPELL_BIGBYS_HAND"] = "Arcane Hand",
            ["ID_PHB_SPELL_DRAWMIJS_INSTANT_SUMMONS"] = "Instant Summons",
            ["ID_PHB_SPELL_EVARDS_BLACK_TENTACLES"] = "Black Tentacles",
            ["ID_PHB_SPELL_LEOMUNDS_SECRET_CHEST"] = "Secret Chest",
            ["ID_PHB_SPELL_LEOMUNDS_TINY_HUT"] = "Tiny Hut",
            ["ID_PHB_SPELL_MELFS_ACID_ARROW"] = "Acid Arrow",
            ["ID_PHB_SPELL_MORDENKAINENS_FAITHFUL_HOUND"] = "Faithful Hound",
            ["ID_PHB_SPELL_MORDENKAINENS_MAGNIFICENT_MANSION"] = "Magnificent Mansion",
            ["ID_PHB_SPELL_MORDENKAINENS_PRIVATE_SANCTUM"] = "Private Sanctum",
            ["ID_PHB_SPELL_MORDENKAINENS_SWORD"] = "Arcane Sword",
            ["ID_PHB_SPELL_NYSTULS_MAGIC_AURA"] = "Arcanist’s Magic Aura",
            ["ID_PHB_SPELL_OTILUKES_FREEZING_SPHERE"] = "Freezing Sphere",
            ["ID_PHB_SPELL_OTILUKES_RESILIENT_SPHERE"] = "Resilient Sphere",
            ["ID_PHB_SPELL_OTTOS_IRRESISTIBLE_DANCE"] = "Irresistible Dance",
            ["ID_PHB_SPELL_RARYS_TELEPATHIC_BOND"] = "Telepathic Bond",
            ["ID_PHB_SPELL_TASHAS_HIDEOUS_LAUGHTER"] = "Hideous Laughter",
            ["ID_PHB_SPELL_TENSERS_FLOATING_DISK"] = "Floating Disk",
        })
        {
            var spell = spells.Single(s => s.Id == id);
            spell.Name.Should().Be(name);
            spell.Level.Should().BeInRange(0, 9);
            spell.MagicSchool.Should().NotBeNullOrWhiteSpace();
            spell.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("ID_PHB_SPELL_TASHAS_HIDEOUS_LAUGHTER", "Tasha’s Hideous Laughter", "Hideous Laughter", 1, 1, false)]
    [InlineData("ID_PHB_SPELL_TASHAS_HIDEOUS_LAUGHTER", "Tasha’s Hideous Laughter", "Hideous Laughter", 1, 1, true)]
    [InlineData("ID_PHB_SPELL_BIGBYS_HAND", "Bigby’s Hand", "Arcane Hand", 9, 5, false)]
    [InlineData("ID_PHB_SPELL_BIGBYS_HAND", "Bigby’s Hand", "Arcane Hand", 9, 5, true)]
    [InlineData("ID_PHB_SPELL_MORDENKAINENS_SWORD", "Mordenkainen’s Sword", "Arcane Sword", 13, 7, false)]
    [InlineData("ID_PHB_SPELL_MORDENKAINENS_SWORD", "Mordenkainen’s Sword", "Arcane Sword", 13, 7, true)]
    public async Task SavedLegacySpellIdentifiersLoadAndExportWithSrdNames(
        string id, string legacyName, string srdName, int characterLevel, int spellLevel, bool editable)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        await ContentFixture.EnsureAvailableAsync();
        ContentFixture.SkipIfUnavailable();
        var manager = CharacterManager.Current;
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Aurora.Tests", "srd-spells"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await manager.New(true);
            manager.Character.Name = "SRD spell acceptance";
            manager.Character.Abilities.Intelligence.BaseScore = 16;
            var handler = SelectionRuleExpanderContext.Current!;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"),
                "ID_WOTC_PHB_CLASS_WIZARD");
            for (int level = 1; level < characterLevel; level++) manager.LevelUpMain();
            manager.ReprocessCharacter();
            var selection = manager.SelectionRules
                .Where(r => r.Attributes.Type == "Spell" && r.Attributes.Name.StartsWith("Spellbook"))
                .OrderByDescending(r => r.Attributes.RequiredLevel).First();
            handler.SetRegisteredElement(selection, id);
            manager.ReprocessCharacter();
            manager.GetElements().OfType<Spell>().Should().Contain(s => s.Id == id);

            string path = Path.Combine(root, "legacy-named.dnd5e");
            var file = new CharacterFile(path);
            file.Save(manager.Character).Should().BeTrue();
            var saved = new XmlDocument();
            saved.Load(path);
            // Reproduce a save written before the display-name migration. The
            // actual persisted compatibility identifier stays unchanged.
            int replacements = 0;
            foreach (XmlNode node in saved.SelectNodes("//@* | //text()")!)
            {
                if (node.Value?.Contains(srdName, StringComparison.Ordinal) == true)
                {
                    node.Value = node.Value.Replace(srdName, legacyName, StringComparison.Ordinal);
                    replacements++;
                }
            }
            replacements.Should().BeGreaterThan(0);
            using (var writer = XmlWriter.Create(path, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                saved.Save(writer);

            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            (await new CharacterFile(path).Load()).Success.Should().BeTrue();
            var restored = manager.GetElements().OfType<Spell>().Single(s => s.Id == id);
            restored.Name.Should().Be(srdName);
            restored.Level.Should().Be(spellLevel);
            manager.Character.Abilities.Intelligence.BaseScore.Should().Be(16);

            string pdfPath = Path.Combine(root, "spell.pdf");
            using (var cards = new CardPageGenerator { Flatten = !editable })
            {
                cards.AddSpellCard(new SpellCardContent(restored.Name, restored.GetShortDescription())
                {
                    CastingTime = restored.CastingTime, Range = restored.Range,
                    Duration = restored.Duration, Components = restored.GetComponentsString(),
                    DescriptionHtml = restored.Description
                });
                cards.Save(pdfPath);
            }
            if (Environment.GetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY") is { } qa)
            {
                Directory.CreateDirectory(qa);
                File.Copy(pdfPath, Path.Combine(qa, $"srd-{id}-{editable}.pdf"), overwrite: true);
            }
            using var pdf = PdfDocument.Open(pdfPath);
            using var form = new PdfReader(pdfPath);
            string text = string.Join("\n", pdf.GetPages().Select(p => p.Text)) +
                string.Join("\n", form.AcroFields.Fields.Keys.Select(form.AcroFields.GetField));
            text.Should().Contain(srdName).And.NotContain(legacyName);
            // Spell cards paint their text in both document modes. Editable
            // sheet fields are covered by PdfExportAcceptanceTests.
            if (!editable) form.AcroFields.Fields.Should().BeEmpty();
        }
        finally
        {
            await manager.New(false);
            Path.GetDirectoryName(Path.GetFullPath(root)).Should().Be(parent);
            Directory.Delete(root, true);
        }
    }
}
