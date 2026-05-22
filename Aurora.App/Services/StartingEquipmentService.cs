using Aurora.Components.Models;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

/// <summary>
/// Retrieves parsed starting equipment blocks for a character's class and background.
/// Checks <see cref="StartingEquipmentDataLoader"/> first (bundled + custom-directory files),
/// then falls back to an inline <c>&lt;starting-equipment&gt;</c> node on the element itself.
/// </summary>
public static class StartingEquipmentService
{
    /// <summary>
    /// Returns starting equipment for the character's primary (first) class,
    /// or <see cref="StartingEquipmentBlock.Empty"/> if none is defined.
    /// </summary>
    public static async Task<StartingEquipmentBlock> GetClassBlockAsync()
    {
        var cm = CharacterManager.Current;
        var classElement = cm.ClassProgressionManagers
            .Select(m => m.ClassElement)
            .FirstOrDefault(e => e != null);

        if (classElement == null)
            return StartingEquipmentBlock.Empty;

        var fromLoader = await StartingEquipmentDataLoader.GetBlockAsync(classElement.Id);
        if (fromLoader.HasContent) return fromLoader;

        // Fallback: inline <starting-equipment> node on the element (future authoritative content).
        var canonical = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id == classElement.Id);
        var inline = StartingEquipmentParser.Parse(canonical?.ElementNode);
        return inline.HasContent
            ? inline
            : XmlContentFallbackService.GetStartingEquipmentBlock(classElement.Id);
    }

    /// <summary>
    /// Returns starting equipment for the character's background,
    /// or <see cref="StartingEquipmentBlock.Empty"/> if none is defined.
    /// </summary>
    public static async Task<StartingEquipmentBlock> GetBackgroundBlockAsync()
    {
        var cm = CharacterManager.Current;

        var bgRegistered = cm.GetElements()
            .FirstOrDefault(e => e.Type.Equals("Background", StringComparison.OrdinalIgnoreCase));

        if (bgRegistered == null)
            return StartingEquipmentBlock.Empty;

        var fromLoader = await StartingEquipmentDataLoader.GetBlockAsync(bgRegistered.Id);
        if (fromLoader.HasContent) return fromLoader;

        var canonical = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id == bgRegistered.Id);
        var inline = StartingEquipmentParser.Parse(canonical?.ElementNode);
        return inline.HasContent
            ? inline
            : XmlContentFallbackService.GetStartingEquipmentBlock(bgRegistered.Id);
    }

    /// <summary>
    /// Returns both class and background starting equipment as a single aggregate.
    /// </summary>
    public static async Task<StartingEquipmentAggregate> GetAllAsync()
    {
        var classBlock = await GetClassBlockAsync();
        var bgBlock    = await GetBackgroundBlockAsync();
        return new StartingEquipmentAggregate(classBlock, bgBlock);
    }
}

/// <summary>Class and background starting equipment for one character.</summary>
public sealed record StartingEquipmentAggregate(
    StartingEquipmentBlock ClassBlock,
    StartingEquipmentBlock BackgroundBlock)
{
    public bool HasAnyContent => ClassBlock.HasContent || BackgroundBlock.HasContent;
}

/// <summary>The resolved choices from the starting equipment dialog — items to add and gold to grant.</summary>
public sealed record StartingEquipmentResult(
    IReadOnlyList<(string ElementId, int Count, string? Name)> Items,
    CoinGrant Coins,
    bool TookRolledGold = false)
{
    public int Copper => Coins.Copper;
    public int Silver => Coins.Silver;
    public int Electrum => Coins.Electrum;
    public int Gold => Coins.Gold;
    public int Platinum => Coins.Platinum;
}
