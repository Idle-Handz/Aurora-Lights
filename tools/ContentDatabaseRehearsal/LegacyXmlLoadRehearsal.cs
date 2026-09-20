using Aurora.App.Services;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Loads content the way Aurora Legacy does — DataManager XML with local corrections, no content
/// database — and then opens each character file through the shared reader. Shows whether characters
/// saved by Reflections still load in Legacy, and which element ids Legacy cannot resolve.
/// </summary>
internal static class LegacyXmlLoadRehearsal
{
    /// <param name="onlyFiles">Comma-separated file names; null runs every character.</param>
    public static async Task<object> Run(string root, string output, string? onlyFiles = null)
    {
        var wanted = onlyFiles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trace = new CharacterTrace(Path.Combine(output, "legacy-xml-trace.log"));
        Builder.Core.Logging.Logger.RegisterLogger(trace);

        await DataManager.Current.InitializeElementDataAsync();
        var catalog = DataManager.Current.ElementsCollection;
        var knownIds = catalog.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        var results = new List<object>();
        foreach (string path in Directory.GetFiles(Path.Combine(root, "characters"), "*.dnd5e")
            .Where(p => wanted == null || wanted.Contains(Path.GetFileName(p))).OrderBy(p => p, StringComparer.Ordinal))
        {
            trace.MissingGrants.Clear();
            trace.LoadWarnings.Clear();
            SelectionRuleExpanderContext.Current = new MauiSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new MauiSpellcastingSectionHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();

            // Ids the file references but this catalog does not define: what Legacy would drop.
            var document = XDocument.Load(path);
            var unknown = document.Descendants("element")
                .Select(e => (string?)e.Attribute("id")).Where(id => !string.IsNullOrEmpty(id))
                .Concat(document.Descendants("custom-features").Elements().Select(e => (string?)e.Value))
                .Where(id => !string.IsNullOrWhiteSpace(id) && !knownIds.Contains(id!))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            bool hasCustomFeatures = document.Descendants("custom-features").Elements().Any();

            try
            {
                var file = new CharacterFile(path);
                var load = await Task.Run(() => file.Load()).WaitAsync(TimeSpan.FromSeconds(180));
                var character = CharacterManager.Current.Character;
                results.Add(new
                {
                    file = Path.GetFileName(path),
                    load.Success,
                    load.Message,
                    character.Level,
                    character.Class,
                    character.Race,
                    elementCount = CharacterManager.Current.GetElements().Count(),
                    hasCustomFeatures,
                    unknownIds = unknown,
                    missingGrants = trace.MissingGrants.Order(StringComparer.Ordinal).ToArray(),
                    loadWarnings = trace.LoadWarnings.Order(StringComparer.Ordinal).ToArray(),
                });
            }
            catch (Exception error)
            {
                results.Add(new { file = Path.GetFileName(path), Success = false, error = error.ToString(), unknownIds = unknown, hasCustomFeatures });
            }
        }

        File.WriteAllText(Path.Combine(output, "legacy-xml-load.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return new { catalogElements = catalog.Count, characters = results.Count, results };
    }
}
