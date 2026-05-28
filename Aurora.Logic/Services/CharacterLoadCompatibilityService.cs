using Builder.Core.Logging;
using Builder.Presentation;
using Builder.Presentation.Models;

namespace Builder.Presentation.Services;

/// <summary>
/// Compatibility helpers that keep shared character state consistent when multiple
/// clients read and write the same save file format.
/// </summary>
public static class CharacterLoadCompatibilityService
{
    /// <summary>
    /// Clears any load-scoped cached state before loading a different character file.
    /// </summary>
    public static void PrepareForCharacterLoad()
    {
        try
        {
            SpellcastingSectionContext.Current?.ResetPreparedState();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(PrepareForCharacterLoad));
        }

        // DataManager element instances are process-wide singletons; registration stamps acquisition
        // (WasSelected / WasGranted / SelectRule / GrantRule) directly onto them. Nothing else clears
        // that between loads, so without this reset a previously-loaded character's acquisition bleeds
        // onto shared elements the next character also touches (verified across both New() and a real
        // Load()). Reset to a clean slate here — the load then re-stamps the elements the new character
        // actually uses.
        try
        {
            foreach (var element in Builder.Presentation.Services.Data.DataManager.Current.ElementsCollection)
                element.Aquisition.Clear();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(PrepareForCharacterLoad));
        }
    }

    /// <summary>
    /// After CharacterManager loads a character from XML it sets IsEquipped /
    /// EquippedLocation on inventory items but may not rebuild the slot references
    /// that clients rely on for equipped armor and weapon state.
    /// </summary>
    public static void RestoreEquippedSlots(Character? character)
    {
        if (character == null)
            return;

        try
        {
            var inv = character.Inventory;
            foreach (var item in inv.Items)
            {
                if (!item.IsEquipped)
                    continue;

                var location = item.EquippedLocation ?? "";

                if (location == "Armor" && inv.EquippedArmor == null)
                {
                    inv.EquipArmor(item);
                }
                else if (location is "Primary Hand" or "Two-Handed" or "Two-Handed (Versatile)"
                         && inv.EquippedPrimary == null)
                {
                    inv.EquipPrimary(item, item.IsTwoHandTarget());
                }
                else if (location == "Secondary Hand" && inv.EquippedSecondary == null)
                {
                    inv.EquipSecondary(item);
                }
                else if (string.IsNullOrEmpty(location))
                {
                    try { item.Activate(equip: true, attune: item.IsAttuned); }
                    catch { }
                }
            }

            inv.CalculateAttunedItemCount();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(RestoreEquippedSlots));
        }
    }
}
