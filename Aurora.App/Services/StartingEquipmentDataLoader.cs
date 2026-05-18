using System.Reflection;
using System.Xml;
using Aurora.Components.Models;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using LogLevel = Aurora.App.Services.LogLevel;

namespace Aurora.App.Services;

/// <summary>
/// Loads per-element starting equipment data from two sources, in priority order:
///   1. Embedded resources from <c>Resources/Raw/StartingEquipment/</c> (shipped with the app).
///   2. <c>*.starting-equipment.xml</c> files found in the user's custom content directories.
/// Custom directory files win so authors can override bundled defaults.
/// Call <see cref="Invalidate"/> after a content reload.
/// </summary>
public static class StartingEquipmentDataLoader
{
    private static Dictionary<string, StartingEquipmentBlock>? _cache;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    private const string FileGlob             = "*.starting-equipment.xml";
    private const string EmbeddedResourceMark = ".StartingEquipment.";

    /// <summary>
    /// Clears the cache so the next call to <see cref="GetBlockAsync"/> re-scans disk.
    /// Called from <c>CharacterService.ReloadElementsAsync</c>.
    /// </summary>
    public static void Invalidate() => _cache = null;

    /// <summary>
    /// Returns the starting equipment block for <paramref name="elementId"/>,
    /// or <see cref="StartingEquipmentBlock.Empty"/> if no data is registered.
    /// </summary>
    public static async Task<StartingEquipmentBlock> GetBlockAsync(string? elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return StartingEquipmentBlock.Empty;
        var cache = await EnsureLoadedAsync();
        return cache.TryGetValue(elementId, out var block) ? block : StartingEquipmentBlock.Empty;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, StartingEquipmentBlock>> EnsureLoadedAsync()
    {
        if (_cache is not null) return _cache;
        await _gate.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;
            _cache = await LoadAllAsync();
            return _cache;
        }
        finally { _gate.Release(); }
    }

    private static Task<Dictionary<string, StartingEquipmentBlock>> LoadAllAsync()
    {
        // Bundled defaults first; custom directory files loaded after can override them.
        var result = new Dictionary<string, StartingEquipmentBlock>(StringComparer.OrdinalIgnoreCase);

        LoadBundled(result);
        LoadFromDirectory(result, DataManager.Current.UserDocumentsCustomElementsDirectory);
        foreach (string dir in ApplicationContext.Current.Settings.AdditionalCustomDirectories)
            LoadFromDirectory(result, dir);

        DebugLogService.Instance.Log(LogLevel.Info,
            $"[StartingEquipmentDataLoader] loaded {result.Count} entries");

        return Task.FromResult(result);
    }

    private static void LoadBundled(Dictionary<string, StartingEquipmentBlock> target)
    {
        var assembly = typeof(StartingEquipmentDataLoader).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(EmbeddedResourceMark) && n.EndsWith(".xml",
                StringComparison.OrdinalIgnoreCase));

        foreach (string resourceName in resources)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                LoadFromStream(stream, target, resourceName);
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex,
                    $"StartingEquipmentDataLoader: {resourceName}");
            }
        }
    }

    private static void LoadFromDirectory(
        Dictionary<string, StartingEquipmentBlock> target, string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

        foreach (string file in Directory.GetFiles(dir, FileGlob, SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(file);
                LoadFromStream(stream, target, file);
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex,
                    $"StartingEquipmentDataLoader: {file}");
            }
        }
    }

    /// <summary>
    /// Reads an <c>&lt;aurora-starting-equipment&gt;</c> document and merges entries into
    /// <paramref name="target"/>. Later entries for the same element-id overwrite earlier ones,
    /// so custom directory files loaded after bundled ones act as overrides.
    /// </summary>
    private static void LoadFromStream(
        Stream stream,
        Dictionary<string, StartingEquipmentBlock> target,
        string sourceName)
    {
        var doc = new XmlDocument();
        doc.Load(stream);

        var root = doc.DocumentElement;
        if (root?.Name != "aurora-starting-equipment")
        {
            DebugLogService.Instance.Log(LogLevel.Warning,
                $"[StartingEquipmentDataLoader] {sourceName}: " +
                $"root is '{root?.Name}', expected 'aurora-starting-equipment'");
            return;
        }

        int count = 0;
        foreach (XmlNode entry in root.ChildNodes)
        {
            if (entry.Name != "entry") continue;
            string? elementId = entry.Attributes?["element-id"]?.Value;
            if (string.IsNullOrEmpty(elementId)) continue;

            // StartingEquipmentParser.Parse expects a node whose direct child is
            // <starting-equipment> — the <entry> node satisfies that contract.
            var block = StartingEquipmentParser.Parse(entry);
            if (block.HasContent)
            {
                target[elementId] = block;
                count++;
            }
        }

        DebugLogService.Instance.Log(LogLevel.Info,
            $"[StartingEquipmentDataLoader] {Path.GetFileName(sourceName)}: {count} entries");
    }
}
