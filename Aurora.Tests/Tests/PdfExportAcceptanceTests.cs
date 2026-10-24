using System.Reflection;
using System.Text;
using Aurora.Documents.Sheets;
using Aurora.Documents.ExportContent.Equipment;
using Aurora.Tests.Helpers;
using Builder.Presentation.Models.CharacterSheet;
using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.CharacterSheet.Pages;
using Builder.Presentation.Models.CharacterSheet.Pages.Content;
using iTextSharp.text.pdf;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.ViewModels.Shell.Items;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace Aurora.Tests.Tests;

// Synthetic content only. Set AURORA_PDF_QA_DIRECTORY to retain exports for visual comparison.
public sealed class PdfExportAcceptanceTests
{
    public static IEnumerable<object[]> SheetModes()
    {
        for (int bits = 0; bits < 32; bits++)
            yield return new object[] { bits };
    }

    [Theory, MemberData(nameof(SheetModes))]
    public void FullSheetPreservesSelectedPagesValuesAndFlattening(int bits)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        bool editable = (bits & 1) != 0;
        var config = new CharacterSheetConfiguration
        {
#pragma warning disable CS0618
            IsEditable = editable,
#pragma warning restore CS0618
            IncludeFormatting = (bits & 2) != 0,
            UseLegacyDetailsPage = (bits & 4) != 0,
            UseLegacyBackgroundPage = (bits & 8) != 0,
            UseLegacySpellcastingPage = (bits & 16) != 0,
            IncludeEquipmentPage = true, IncludeNotesPage = true,
            IncludeCompanionPage = true, IncludeSpellcastingPage = true
        };
        var sheet = new CharacterSheetEx(config) { EquipmentSheetExportContent = new(), NotesExportContent = new() };
        InitializeStrings(sheet.ExportContent);
        InitializeStrings(sheet.EquipmentSheetExportContent);
        sheet.ExportContent.CharacterName = "Acceptance Hero";
        sheet.ExportContent.PlayerName = "Synthetic Player";
        sheet.ExportContent.Level = "5";
        for (int n = 0; n < 3; n++)
        {
            var attack = new CharacterSheetExportContent.AttackExportContent();
            InitializeStrings(attack);
            attack.Name = "Acceptance Attack " + n;
            attack.Bonus = "+6"; attack.Damage = "1d8 + 2";
            sheet.ExportContent.AttacksContent.Add(attack);
        }
        sheet.ExportContent.MainClass.Name = "Wizard";
        sheet.ExportContent.MainClass.Level = "5";
        sheet.ExportContent.AbilitiesContent.Strength = "14";
        sheet.ExportContent.AbilitiesContent.StrengthSaveProficient = true;
        sheet.ExportContent.ArmorClassContent.StealthDisadvantage = true;
        sheet.ExportContent.AbilitiesContent.StrengthModifier = "+2";
        sheet.ExportContent.CompanionName = "Acceptance Companion";
        sheet.ExportContent.Features = "<p><b>Feature marker.</b> A useful feature with several words.</p><ul><li>First benefit</li><li>Second benefit</li></ul>";
        sheet.ExportContent.AdditionalFeaturesAndTraits = "<p>Background marker with <i>italic words</i>.</p>";
        sheet.NotesExportContent.LeftNotesColumn = "Acceptance notes" + Environment.NewLine + "Second paragraph";
        sheet.NotesExportContent.RightNotesColumn = "Right column marker";
        sheet.EquipmentSheetExportContent.AdventuringGear.Add(new InventoryItemExportContent("Acceptance Rope") { Amount = "2", Weight = "10", IsEquipped = true });
        sheet.EquipmentSheetExportContent.Coinage.Gold = "37";
        sheet.EquipmentSheetExportContent.AttunedMagicItems = "<p>Acceptance Ring</p>";
        foreach (string caster in new[] { "Wizard", "Cleric" })
        {
            var casting = new CharacterSheetSpellcastingPageExportContent
            {
                SpellcastingClass = caster, Ability = "Intelligence", AttackBonus = "+6", Save = "14", PrepareCount = "5"
            };
            casting.Spells1.Level = 1;
            casting.Spells1.AvailableSlots = 4;
            casting.Spells1.Spells.Add(new() { Name = caster + " Spell", IsPrepared = true, Description = "Spell description marker", Level = "1", School = "Evocation", Components = "V, S", Duration = "Instantaneous", Range = "120 ft.", CastingTime = "1 action", Subtitle = "" });
            sheet.SpellcastingPageExportContentCollection.Add(casting);
        }
        WithOutput($"sheet-{bits:D2}.pdf", path =>
        {
            sheet.Save(path);
            using var reader = new PdfReader(path);
            reader.NumberOfPages.Should().Be(config.UseLegacySpellcastingPage ? 7 : 6);
            string values = string.Join("\n", reader.AcroFields.Fields.Keys.Select(reader.AcroFields.GetField));
            using var independent = PdfDocument.Open(path);
            string text = string.Join("\n", independent.GetPages().Select(p => p.Text));
            string all = text + values;
            foreach (string expected in new[] { "Acceptance Hero", "Acceptance Companion", "Acceptance Rope", "Acceptance notes", "Wizard Spell", "Cleric Spell", "37", "Acceptance Attack 0", "Acceptance Attack 1", "Acceptance Attack 2" })
                all.Should().Contain(expected);
            if (editable)
            {
                reader.AcroFields.GetField(config.UseLegacyDetailsPage ? "CharacterName" : "details_character_name").Should().Be("Acceptance Hero");
                reader.AcroFields.GetField(config.UseLegacyDetailsPage ? "STR" : "details_str_score").Should().Be("14");
                if (!config.UseLegacyDetailsPage)
                {
                    reader.AcroFields.GetField("details_str_save_proficiency").Should().Be("Yes");
                    reader.AcroFields.GetField("details_dex_save_proficiency").Should().Be("Off");
                    reader.AcroFields.GetField("details_armor_stealth_disadvantage").Should().Be("Yes");
                }
            }
            AssertFlattening(reader, editable);
        });
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CardKindsPreserveContentAndPagination(bool editable, bool separated)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        using var cards = new CardPageGenerator { Flatten = !editable };
        const string html = "<p><b>Action marker.</b> Accurate text with <i>emphasis</i>.</p><ul><li>List marker</li><li>Second benefit</li></ul><table><tr><th>Roll</th><th>Result</th></tr><tr><td>1</td><td>Table marker</td></tr></table><p>Ending marker.</p>";
        for (int kind = 0; kind < 4; kind++)
        {
            if (separated && kind > 0) cards.StartNewPage();
            for (int n = 0; n < 10; n++)
            {
                string title = $"Kind {kind} card {n}";
                switch (kind)
                {
                    case 0: cards.AddSpellCard(new SpellCardContent(title, "Level 1 evocation") { CastingTime = "1 action", Range = "120 ft.", Duration = "1 minute", Components = "V, S", DescriptionHtml = html }); break;
                    case 1: cards.AddAttackCard(new AttackCardContent(title, "Melee weapon") { Range = "5 ft.", Attack = "+6", Damage = "1d8 + 2", Description = html }); break;
                    default: cards.AddGenericCard(new GenericCardContent(title, kind == 2 ? "Item" : "Feature") { DescriptionHtml = html }); break;
                }
            }
        }
        WithOutput($"cards-{editable}-{separated}.pdf", path =>
        {
            cards.Save(path);
            using var reader = new PdfReader(path);
            reader.NumberOfPages.Should().Be(separated ? 8 : 5);
            using var independent = PdfDocument.Open(path);
            string all = string.Join("\n", independent.GetPages().Select(p => p.Text)) +
                string.Join("\n", reader.AcroFields.Fields.Keys.Select(reader.AcroFields.GetField));
            for (int kind = 0; kind < 4; kind++)
                for (int n = 0; n < 10; n++) all.Should().Contain($"Kind {kind} card {n}");
            all.Should().Contain("Ending marker.");
            all.Should().Contain("Table marker");
            AssertFlattening(reader, editable);
        });
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void SheetCardOptionsMergeEveryCategoryAndHonorPageBreaks(bool editable, bool separated)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        var config = new CharacterSheetConfiguration { IncludeCharacterPage = false, IncludeBackgroundPage = false,
            IncludeSpellcards = true, IncludeAttackCards = true, IncludeItemcards = true, IncludeFeatureCards = true,
            StartNewSpellCardsPage = separated, StartNewAttackCardsPage = separated, StartNewItemCardsPage = separated, StartNewFeatureCardsPage = separated };
#pragma warning disable CS0618
        config.IsEditable = editable;
#pragma warning restore CS0618
        var sheet = new CharacterSheetEx(config);
        InitializeStrings(sheet.ExportContent);
        sheet.Spells.Add(new Spell { ElementHeader = new("Integration Spell", "Spell", "Synthetic", "TEST_SPELL"), Level = 1, MagicSchool = "Evocation", CastingTime = "1 action", Duration = "1 minute", Range = "60 ft.", HasVerbalComponent = true, Description = "<p>Spell body marker.</p>" });
        sheet.ExportContent.AttacksContent.Add(new() { Name = "Integration Attack", Underline = "Melee", Range = "5 ft.", Bonus = "+6", Damage = "1d8 + 2", Description = "Attack body marker.", AsCard = true });
        sheet.Items.Add(new RefactoredEquipmentItem(new Item { ElementHeader = new("Integration Item", "Item", "Synthetic", "TEST_ITEM"), Description = "<p>Item body marker.</p>", Category = "Adventuring Gear", Weight = "1 lb." }));
        var feature = new ElementBase("Integration Feature", "Class Feature", "Synthetic", "TEST_FEATURE") { Description = "<p>Feature body marker.</p>" };
        feature.SheetDescription.DisplayOnSheet = true;
        sheet.Features.Add(feature);
        WithOutput($"integration-cards-{editable}-{separated}.pdf", path =>
        {
            sheet.Save(path);
            using var reader = new PdfReader(path);
            reader.NumberOfPages.Should().Be(separated ? 4 : 1);
            using var pdf = PdfDocument.Open(path);
            string all = string.Join("\n", pdf.GetPages().Select(p => p.Text)) + string.Join("\n", reader.AcroFields.Fields.Keys.Select(reader.AcroFields.GetField));
            foreach (string value in new[] { "Integration Spell", "Integration Attack", "Integration Item", "Integration Feature", "Spell body marker", "Attack body marker", "Item body marker", "Feature body marker" }) all.Should().Contain(value);
            AssertFlattening(reader, editable);
        });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void OptionalPagesCanBeExportedIndependently(int mode)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        var config = new CharacterSheetConfiguration { IncludeCharacterPage = mode == 0, IncludeBackgroundPage = mode == 1, IncludeEquipmentPage = mode == 2, IncludeNotesPage = mode == 3, IncludeCompanionPage = mode == 4 };
        var sheet = new CharacterSheetEx(config) { EquipmentSheetExportContent = new(), NotesExportContent = new() };
        InitializeStrings(sheet.ExportContent); InitializeStrings(sheet.EquipmentSheetExportContent);
        sheet.ExportContent.MainClass.Name = "Wizard"; sheet.ExportContent.MainClass.Level = "1";
        WithOutput($"single-page-{mode}.pdf", path => { sheet.Save(path); using var reader = new PdfReader(path); reader.NumberOfPages.Should().Be(1); AssertFlattening(reader, false); });
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void PortraitOrganizationAndCompanionImagesSurviveFinalization(bool editable, bool legacy)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        WithOutput($"images-{editable}-{legacy}.pdf", path =>
        {
            string image = path + ".png";
            File.WriteAllBytes(image, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEUlEQVR4nGP4z8AARAxg8j8AG/ID/fPnS7EAAAAASUVORK5CYII="));
            try
            {
                var config = new CharacterSheetConfiguration { IncludeCharacterPage = false, IncludeCompanionPage = true, UseLegacyBackgroundPage = legacy };
#pragma warning disable CS0618
                config.IsEditable = editable;
#pragma warning restore CS0618
                var sheet = new CharacterSheetEx(config); InitializeStrings(sheet.ExportContent);
                string without = path + ".empty.pdf";
                sheet.Save(without);
                using var before = PdfDocument.Open(without);
                int imagesBefore = before.GetPages().Sum(p => p.GetImages().Count());
                sheet.ExportContent.AppearanceContent.Portrait = image;
                sheet.ExportContent.OrganizationSymbol = image;
                sheet.ExportContent.CompanionPortrait = image;
                sheet.Save(path);
                using var after = PdfDocument.Open(path);
                // Editable button appearances are not page content until flattened.
                if (!editable) after.GetPages().Sum(p => p.GetImages().Count()).Should().Be(imagesBefore + 3);
                using var reader = new PdfReader(path);
                AssertFlattening(reader, editable);
                if (editable)
                {
                    reader.AcroFields.Fields.Keys.Count(n => n.Contains("replaced")).Should().BeGreaterThanOrEqualTo(3);
                    using var flattened = new MemoryStream();
                    using (var stamper = new PdfStamper(reader, flattened)) stamper.FormFlattening = true;
                    using var rendered = PdfDocument.Open(flattened.ToArray());
                    rendered.GetPages().Sum(p => p.GetImages().Count()).Should().Be(imagesBefore + 3);
                }
            }
            finally { File.Delete(image); }
        });
    }

    [Theory]
    [InlineData("<table><tr><td colspan='2'>Wide cell marker</td></tr><tr><td>Left cell</td><td>Right cell</td></tr></table>", "Wide cell marker")]
    [InlineData("<table><tr><td rowspan='2'>Tall cell marker</td><td>Top cell</td></tr><tr><td>Bottom cell</td></tr></table>", "Tall cell marker")]
    [InlineData("<p>Unicode marker: Łódź, café, naïve, ±2.</p><ol><li>First numbered item</li><li>Second numbered item</li></ol>", "Łódź")]
    public void RichCardDescriptionsKeepTablesListsUnicodeAndEndOfText(string html, string expected)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        using var cards = new CardPageGenerator { Flatten = true };
        cards.AddGenericCard(new GenericCardContent("Rich description", "Synthetic") { DescriptionHtml = html + "<p>Last content marker.</p>" });
        WithOutput($"rich-{expected.Split(' ')[0]}.pdf", path =>
        {
            cards.Save(path);
            using var pdf = PdfDocument.Open(path);
            string all = pdf.GetPage(1).Text;
            all.Should().Contain(expected);
            all.Should().Contain("Last content marker.");
            if (html.Contains("colspan")) { all.Should().Contain("Left cell"); all.Should().Contain("Right cell"); }
            if (html.Contains("rowspan")) { all.Should().Contain("Top cell"); all.Should().Contain("Bottom cell"); }
        });
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public void AbilityScoresAndModifiersRemainAccurateWhenFlipped(bool editable, bool flipped, bool legacy)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        var config = new CharacterSheetConfiguration { IncludeBackgroundPage = false, UseLegacyDetailsPage = legacy, IsAttributeDisplayFlipped = flipped };
#pragma warning disable CS0618
        config.IsEditable = editable;
#pragma warning restore CS0618
        var sheet = new CharacterSheetEx(config); InitializeStrings(sheet.ExportContent);
        sheet.ExportContent.CharacterName = "Élodie Łódź";
        var abilities = sheet.ExportContent.AbilitiesContent;
        string[] names = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
        string[] shortNames = { "str", "dex", "con", "int", "wis", "cha" };
        string[] scores = { "14", "16", "12", "18", "10", "8" };
        string[] modifiers = { "+2", "+3", "+1", "+4", "+0", "-1" };
        for (int n = 0; n < names.Length; n++)
        {
            abilities.GetType().GetProperty(names[n])!.SetValue(abilities, scores[n]);
            abilities.GetType().GetProperty(names[n] + "Modifier")!.SetValue(abilities, modifiers[n]);
        }
        WithOutput($"abilities-{editable}-{flipped}-{legacy}.pdf", path =>
        {
            sheet.Save(path);
            using var reader = new PdfReader(path);
            AssertFlattening(reader, editable);
            if (editable)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    string score = legacy ? shortNames[n].ToUpperInvariant() : $"details_{shortNames[n]}_score";
                    string modifier = legacy ? (n == 5 ? "CHamod" : n == 1 ? "DEXmod " : shortNames[n].ToUpperInvariant() + "mod") : $"details_{shortNames[n]}_modifier";
                    reader.AcroFields.GetField(flipped ? modifier : score).Should().Be(scores[n]);
                    reader.AcroFields.GetField(flipped ? score : modifier).Should().Be(modifiers[n]);
                }
            }
            else
            {
                using var pdf = PdfDocument.Open(path);
                pdf.GetPage(1).Text.Should().Contain("Élodie Łódź");
                foreach (string value in scores.Concat(modifiers)) pdf.GetPage(1).Text.Should().Contain(value);
            }
        });
    }

    [Fact]
    public void DenseNotesAndWrappedCardDescriptionsKeepFinalLines()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        var config = new CharacterSheetConfiguration { IncludeCharacterPage = false, IncludeBackgroundPage = false, IncludeNotesPage = true, IncludeFormatting = true };
        var sheet = new CharacterSheetEx(config) { NotesExportContent = new() };
        InitializeStrings(sheet.ExportContent);
        sheet.NotesExportContent.LeftNotesColumn = string.Join(Environment.NewLine, Enumerable.Range(1, 70).Select(n => $"Line {n}: a synthetic journal entry."));
        sheet.NotesExportContent.RightNotesColumn = "Right end marker.";
        WithOutput("dense-notes.pdf", path =>
        {
            sheet.Save(path);
            using var pdf = PdfDocument.Open(path);
            for (int n = 1; n <= 70; n++) pdf.GetPage(1).Text.Should().Contain($"Line {n}:");
            pdf.GetPage(1).Text.Should().Contain("Right end marker.");
        });
        using var cards = new CardPageGenerator { Flatten = true };
        cards.AddGenericCard(new GenericCardContent("Wrapped description", "Synthetic") { DescriptionHtml = "<p>" + string.Join(" ", Enumerable.Repeat("A precise effect with several conditions and meaningful details.", 14)) + " Final wrapped marker.</p>" });
        WithOutput("dense-card.pdf", path => { cards.Save(path); using var pdf = PdfDocument.Open(path); string.Concat(pdf.GetPage(1).Text.Where(c => !char.IsWhiteSpace(c))).Should().Contain("Finalwrappedmarker."); });
    }

    private static void AssertFlattening(PdfReader reader, bool editable)
    {
        int widgets = 0;
        for (int n = 1; n <= reader.NumberOfPages; n++)
        {
            var annotations = reader.GetPageN(n).GetAsArray(new PdfName("Annots"));
            if (annotations is null) continue;
            foreach (var reference in annotations.ArrayList)
                if (PdfReader.GetPdfObject(reference) is PdfDictionary annotation && annotation.GetAsName(new PdfName("Subtype"))?.Equals(new PdfName("Widget")) == true) widgets++;
        }
        if (editable) { reader.AcroFields.Fields.Should().NotBeEmpty(); widgets.Should().BeGreaterThan(0); }
        else { reader.AcroFields.Fields.Should().BeEmpty(); widgets.Should().Be(0); }
    }

    private static void WithOutput(string name, Action<string> action)
    {
        string? qa = Environment.GetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY");
        string directory = qa ?? Path.Combine(Path.GetTempPath(), "Aurora.Tests", "pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, name)); }
        finally { if (qa is null) Directory.Delete(directory, true); }
    }

    private static void InitializeStrings(object content)
    {
        foreach (var p in content.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (p.PropertyType == typeof(string) && p.CanWrite && p.GetValue(content) is null) p.SetValue(content, "");
            else if (p.PropertyType.Namespace == content.GetType().Namespace && p.GetValue(content) is { } nested) InitializeStrings(nested);
        }
    }
}
