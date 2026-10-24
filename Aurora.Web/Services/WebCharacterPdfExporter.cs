using Builder.Presentation;
using Builder.Presentation.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Aurora.Web.Services;

internal static class WebCharacterPdfExporter
{
    public static byte[] CreateSummaryPdf()
    {
        Character character = CharacterManager.Current.Character;

        using MemoryStream stream = new();
        using Document document = new(PageSize.Letter, 36f, 36f, 36f, 36f);
        PdfWriter.GetInstance(document, stream);
        document.Open();

        Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20f);
        Font sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13f);
        Font bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f);

        document.Add(new Paragraph(character.Name ?? "Aurora Character", titleFont));
        document.Add(new Paragraph(
            $"Level {character.Level} {character.Race} {character.Class}".Trim(),
            bodyFont));
        document.Add(new Paragraph($"Player: {character.PlayerName}", bodyFont));
        document.Add(new Paragraph($"Background: {character.Background}", bodyFont));
        document.Add(new Paragraph($"Alignment: {character.Alignment}", bodyFont));
        document.Add(Chunk.Newline);

        document.Add(new Paragraph("Abilities", sectionFont));
        PdfPTable abilities = new(6) { WidthPercentage = 100f };
        AddAbilityCell(abilities, "STR", character.Abilities.Strength.FinalScore, character.Abilities.Strength.ModifierString, bodyFont);
        AddAbilityCell(abilities, "DEX", character.Abilities.Dexterity.FinalScore, character.Abilities.Dexterity.ModifierString, bodyFont);
        AddAbilityCell(abilities, "CON", character.Abilities.Constitution.FinalScore, character.Abilities.Constitution.ModifierString, bodyFont);
        AddAbilityCell(abilities, "INT", character.Abilities.Intelligence.FinalScore, character.Abilities.Intelligence.ModifierString, bodyFont);
        AddAbilityCell(abilities, "WIS", character.Abilities.Wisdom.FinalScore, character.Abilities.Wisdom.ModifierString, bodyFont);
        AddAbilityCell(abilities, "CHA", character.Abilities.Charisma.FinalScore, character.Abilities.Charisma.ModifierString, bodyFont);
        document.Add(abilities);
        document.Add(Chunk.Newline);

        document.Add(new Paragraph("Combat", sectionFont));
        PdfPTable combat = new(4) { WidthPercentage = 100f };
        AddLabeledValue(combat, "Armor Class", character.ArmorClass.ToString(), bodyFont);
        AddLabeledValue(combat, "Initiative", FormatSigned(character.Initiative), bodyFont);
        AddLabeledValue(combat, "Speed", character.Speed.ToString(), bodyFont);
        AddLabeledValue(combat, "Max HP", character.MaxHp.ToString(), bodyFont);
        document.Add(combat);
        document.Add(Chunk.Newline);

        document.Add(new Paragraph("Skills", sectionFont));
        PdfPTable skills = new(2) { WidthPercentage = 100f };
        AddLabeledValue(skills, "Perception", FormatSigned(character.Skills.Perception.FinalBonus), bodyFont);
        AddLabeledValue(skills, "Stealth", FormatSigned(character.Skills.Stealth.FinalBonus), bodyFont);
        AddLabeledValue(skills, "Athletics", FormatSigned(character.Skills.Athletics.FinalBonus), bodyFont);
        AddLabeledValue(skills, "Arcana", FormatSigned(character.Skills.Arcana.FinalBonus), bodyFont);
        AddLabeledValue(skills, "Insight", FormatSigned(character.Skills.Insight.FinalBonus), bodyFont);
        AddLabeledValue(skills, "Persuasion", FormatSigned(character.Skills.Persuasion.FinalBonus), bodyFont);
        document.Add(skills);

        if (character.AttacksSection.Items.Any(attack => attack.IsDisplayed))
        {
            document.Add(Chunk.Newline);
            document.Add(new Paragraph("Attacks", sectionFont));
            PdfPTable attacks = new(4) { WidthPercentage = 100f };
            AddHeader(attacks, "Name", bodyFont);
            AddHeader(attacks, "Attack", bodyFont);
            AddHeader(attacks, "Damage", bodyFont);
            AddHeader(attacks, "Range", bodyFont);

            foreach (var attack in character.AttacksSection.Items.Where(attack => attack.IsDisplayed).Take(8))
            {
                AddBody(attacks, attack.Name.Content, bodyFont);
                AddBody(attacks, attack.Attack.Content, bodyFont);
                AddBody(attacks, attack.Damage.Content, bodyFont);
                AddBody(attacks, attack.Range.Content, bodyFont);
            }

            document.Add(attacks);
        }

        document.Close();
        return stream.ToArray();
    }

    private static void AddAbilityCell(PdfPTable table, string label, int score, string modifier, Font font)
    {
        PdfPCell cell = new(new Phrase($"{label}\n{score} ({modifier})", font))
        {
            HorizontalAlignment = Element.ALIGN_CENTER,
            Padding = 8f
        };
        table.AddCell(cell);
    }

    private static void AddLabeledValue(PdfPTable table, string label, string? value, Font font)
    {
        AddHeader(table, label, font);
        AddBody(table, value, font);
    }

    private static void AddHeader(PdfPTable table, string? text, Font font)
    {
        PdfPCell cell = new(new Phrase(text ?? string.Empty, font))
        {
            BackgroundColor = new BaseColor(235, 240, 248),
            Padding = 6f
        };
        table.AddCell(cell);
    }

    private static void AddBody(PdfPTable table, string? text, Font font)
    {
        PdfPCell cell = new(new Phrase(text ?? string.Empty, font))
        {
            Padding = 6f
        };
        table.AddCell(cell);
    }

    private static string FormatSigned(int value) =>
        value >= 0 ? $"+{value}" : value.ToString();
}
