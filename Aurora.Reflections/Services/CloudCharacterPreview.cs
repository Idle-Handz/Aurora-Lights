using System.Xml;
using System.Xml.Linq;
using Aurora.Components.Models;
using Builder.Presentation.Services.Storage;

namespace Aurora.Reflections.Services;

/// <summary>Reads card details without loading content or opening a character workspace.</summary>
public static class CloudCharacterPreview
{
    public static CharacterBrowserEntry Placeholder(CharacterDocumentMetadata metadata) => new(
        metadata.Reference.DocumentId, Path.GetFileNameWithoutExtension(metadata.FileName),
        "Google Drive", CaptionText: "Loading portrait...", GroupName: "Cloud Saves", IsCloud: true);

    public static CharacterBrowserEntry Read(CharacterDocument document)
    {
        using var stream = new MemoryStream(document.Content);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "character") throw new InvalidDataException("This file is not an Aurora character.");
        var display = root.Element("display-properties");
        string name = display?.Element("name")?.Value
            ?? root.Element("build")?.Element("input")?.Element("name")?.Value
            ?? Path.GetFileNameWithoutExtension(document.Metadata.FileName);
        string? portrait = display?.Element("portrait")?.Element("base64")?.Value;
        string? portraitUrl = null;
        if (!string.IsNullOrWhiteSpace(portrait))
        {
            // Never read a local path supplied by a remote character. Portraits travel embedded.
            try { portraitUrl = "data:image/png;base64," + Convert.ToBase64String(Convert.FromBase64String(portrait)); }
            catch (FormatException) { }
        }
        return new(document.Metadata.Reference.DocumentId, name,
            $"Level {display?.Element("level")?.Value ?? "1"} - {display?.Element("race")?.Value}",
            display?.Element("class")?.Value,
            CaptionText: document.Metadata.ModifiedTime is { } modified ? $"Saved {modified.ToLocalTime():g}" : "Google Drive",
            GroupName: "Cloud Saves", IsFavorite: bool.TryParse(display?.Attribute("favorite")?.Value, out bool favorite) && favorite,
            PortraitDataUrl: portraitUrl, IsCloud: true);
    }
}
