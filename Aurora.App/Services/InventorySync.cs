using Builder.Presentation;
using Builder.Presentation.Models;

namespace Aurora.App.Services;

/// <summary>
/// What every page does after it changes a character's inventory or coins in memory: let the engine
/// settle, then bring the snapshot the pages render from back in line with the live character.
/// </summary>
public static class InventorySync
{
    /// <summary>
    /// Refreshes <paramref name="snapshot"/> from <paramref name="character"/>. The engine is
    /// reprocessed first (armor, attacks and attunement all depend on what is carried) unless the
    /// change cannot affect it, such as a rename or a note. A tab without a snapshot yet is still
    /// reprocessed.
    /// </summary>
    public static void Refresh(CharacterSnapshot? snapshot, Character character, bool reprocess = true)
    {
        if (reprocess)
            CharacterManager.Current.ReprocessCharacter();

        snapshot?.RefreshEquipmentFromCharacter(character);
    }
}
