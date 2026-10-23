using System.Reflection;
using System.Text;
using Aurora.Documents.Sheets;
using Aurora.Tests.Helpers;
using Builder.Presentation.Models.CharacterSheet;
using Builder.Presentation.Models.CharacterSheet.Content;
using iTextSharp.text.pdf;
using PdfTextExtractor = iTextSharp.text.pdf.parser.PdfTextExtractor;

namespace Aurora.Tests.Tests;

public sealed class CharacterSheetFlatteningTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void FillablePreferenceControlsAllFieldsAndWidgetsWithEitherSpellLayout(bool fillable, bool spells, bool legacy)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TestApplicationContextInstaller.EnsureInstalled();
        var config = new CharacterSheetConfiguration
        {
#pragma warning disable CS0618
            IsEditable = fillable,
#pragma warning restore CS0618
            IncludeBackgroundPage = true,
            IncludeSpellcastingPage = spells,
            UseLegacySpellcastingPage = legacy
        };
        var sheet = new CharacterSheetEx(config);
        FillEmptyStrings(sheet.ExportContent);
        sheet.ExportContent.CharacterName = "Flattening Test Hero";
        sheet.ExportContent.Level = "3";
        sheet.ExportContent.MainClass.Name = "Wizard";
        sheet.ExportContent.MainClass.Level = "3";
        if (spells)
        {
            var casting = new CharacterSheetSpellcastingPageExportContent
            {
                SpellcastingClass = "Wizard", Ability = "Intelligence", AttackBonus = "+4", Save = "12", PrepareCount = "4"
            };
            casting.Spells1.Level = 1;
            casting.Spells1.AvailableSlots = 4;
            casting.Spells1.Spells.Add(new() { Name = "Magic Missile", IsPrepared = true, Description = "Test spell", Level = "1", School = "Evocation", Components = "V, S", Duration = "Instantaneous", Range = "120 ft.", CastingTime = "1 action", Subtitle = "" });
            sheet.SpellcastingPageExportContentCollection.Add(casting);
        }
        string? qaDirectory = Environment.GetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY");
        string directory = qaDirectory ?? Path.Combine(Path.GetTempPath(), "Aurora.Tests", "sheet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string portrait = Path.Combine(directory, "portrait.png");
        File.WriteAllBytes(portrait, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=="));
        sheet.ExportContent.AppearanceContent.Portrait = portrait;
        string output = Path.Combine(directory, $"fillable-{fillable}-spells-{spells}-legacy-{legacy}.pdf");
        try
        {
            sheet.Save(output);
            using var reader = new PdfReader(output);
            int widgets = 0;
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                var annotations = reader.GetPageN(page).GetAsArray(PdfName.ANNOTS);
                if (annotations is null) continue;
                foreach (var reference in annotations.ArrayList)
                    if (PdfReader.GetPdfObject(reference) is PdfDictionary annotation && annotation.GetAsName(PdfName.SUBTYPE)?.Equals(PdfName.WIDGET) == true)
                        widgets++;
            }
            if (fillable)
            {
                reader.AcroFields.Fields.Should().ContainKey("background_portrait_image:replaced");
                reader.AcroFields.GetField("details_character_name").Should().Be("Flattening Test Hero");
                reader.AcroFields.Fields.Should().NotBeEmpty();
                widgets.Should().BeGreaterThan(0);
            }
            else
            {
                reader.AcroFields.Fields.Should().BeEmpty();
                widgets.Should().Be(0);
                PdfTextExtractor.GetTextFromPage(reader, 1).Should().Contain("Flattening Test Hero");
            }
            if (spells)
            {
                string spellContent = PdfTextExtractor.GetTextFromPage(reader, reader.NumberOfPages) +
                    string.Join("\n", reader.AcroFields.Fields.Keys.Select(reader.AcroFields.GetField));
                spellContent.Should().Contain("Magic Missile");
            }
        }
        finally { if (qaDirectory is null) Directory.Delete(directory, true); }
    }

    private static void FillEmptyStrings(object content)
    {
        foreach (var property in content.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.PropertyType == typeof(string) && property.CanWrite) property.SetValue(content, "");
            else if (property.PropertyType.Namespace == typeof(CharacterSheetExportContent).Namespace && property.GetValue(content) is { } nested)
                FillEmptyStrings(nested);
        }
    }
}
