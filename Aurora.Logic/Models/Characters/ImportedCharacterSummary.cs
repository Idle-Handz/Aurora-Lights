using System.Xml.Linq;
using Builder.Presentation.Models;

namespace Builder.Presentation.Models.Characters;

public sealed record ImportedCharacterSummary(
    string RelativePath,
    string FileName,
    string DisplayName,
    string PlayerName,
    string Level,
    string Race,
    string CharacterClass,
    string Background,
    string GroupName,
    string Version,
    long SizeBytes)
{
    public string DisplayBuild => $"Level {Level} {Race} {CharacterClass}".Trim();

    public static ImportedCharacterSummary FromCharacterFile(
        string relativePath,
        FileInfo fileInfo,
        Character character,
        CharacterFile characterFile) =>
        new(
            relativePath,
            fileInfo.Name,
            string.IsNullOrWhiteSpace(character.Name) ? characterFile.DisplayName : character.Name,
            character.PlayerName ?? string.Empty,
            character.Level.ToString(),
            character.Race ?? characterFile.DisplayRace ?? string.Empty,
            character.Class ?? characterFile.DisplayClass ?? string.Empty,
            character.Background ?? characterFile.DisplayBackground ?? string.Empty,
            characterFile.CollectionGroupName ?? string.Empty,
            characterFile.DisplayVersion ?? string.Empty,
            fileInfo.Length);

    public static ImportedCharacterSummary FromSavedFile(
        string absolutePath,
        string relativePath,
        string fileName,
        long sizeBytes)
    {
        XDocument document = XDocument.Load(absolutePath, LoadOptions.None);
        XElement? root = document.Root;
        XElement? display = root?.Element("display-properties");
        XElement? build = root?.Element("build");
        XElement? input = build?.Element("input");
        XElement? info = root?.Element("information");

        string displayName = display?.Element("name")?.Value
                             ?? input?.Element("name")?.Value
                             ?? Path.GetFileNameWithoutExtension(fileName);

        return new ImportedCharacterSummary(
            relativePath,
            fileName,
            displayName,
            input?.Element("player-name")?.Value ?? string.Empty,
            display?.Element("level")?.Value ?? "1",
            display?.Element("race")?.Value ?? string.Empty,
            display?.Element("class")?.Value ?? string.Empty,
            display?.Element("background")?.Value ?? string.Empty,
            info?.Element("group")?.Value ?? string.Empty,
            root?.Attribute("version")?.Value ?? string.Empty,
            sizeBytes);
    }
}
