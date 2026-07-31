using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Builder.Presentation.Services;

/// <summary>
/// Creates inventory entries while preserving Aurora's base-item plus magic-item
/// composition model for magic weapons and armor.
/// </summary>
public static class InventoryItemFactory
{
    public enum TemplateKind
    {
        Armor,
        Weapon,
    }

    public static TemplateKind? GetTemplateKind(ElementBase element)
    {
        if (element is not Item item ||
            (!item.Type.Equals("Magic Item", StringComparison.OrdinalIgnoreCase) &&
             !item.Type.Equals("Item", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (item.ElementSetters.ContainsSetter("weapon"))
            return TemplateKind.Weapon;
        if (item.ElementSetters.ContainsSetter("armor"))
            return TemplateKind.Armor;
        return null;
    }

    public static IReadOnlyList<Item> GetCompatibleBaseItems(
        CharacterInventory inventory,
        ElementBase element)
    {
        if (element is not Item item)
            return [];

        try
        {
            return GetTemplateKind(item) switch
            {
                TemplateKind.Weapon => ResolveCompatibleWeapons(inventory, item)
                    .Cast<Item>()
                    .OrderBy(candidate => candidate.Name)
                    .ToList(),
                TemplateKind.Armor => ResolveCompatibleArmor(inventory, item)
                    .Cast<Item>()
                    .OrderBy(candidate => candidate.Name)
                    .ToList(),
                _ => [],
            };
        }
        catch
        {
            return [];
        }
    }

    public static RefactoredEquipmentItem? Create(
        CharacterInventory inventory,
        ElementBase element,
        string? baseElementId = null)
    {
        if (element is not Item item)
            return null;

        if (GetTemplateKind(item) is null)
            return new RefactoredEquipmentItem(item);

        if (string.IsNullOrWhiteSpace(baseElementId))
            return null;

        var baseItem = GetCompatibleBaseItems(inventory, item)
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(baseElementId, StringComparison.OrdinalIgnoreCase));

        return baseItem is null
            ? null
            : new RefactoredEquipmentItem(baseItem, item);
    }

    public static RefactoredEquipmentItem CreateForLoadCompatibility(
        CharacterInventory inventory,
        Item item)
    {
        if (GetTemplateKind(item) is null)
            return new RefactoredEquipmentItem(item);

        var candidates = GetCompatibleBaseItems(inventory, item);
        return candidates.Count == 1
            ? new RefactoredEquipmentItem(candidates[0], item)
            : new RefactoredEquipmentItem(item);
    }

    private static IEnumerable<WeaponElement> ResolveCompatibleWeapons(
        CharacterInventory inventory,
        Item magicItem)
    {
        var supportExpression = GetSetterValue(magicItem, "weapon");
        return string.IsNullOrWhiteSpace(supportExpression)
            ? []
            : inventory.GetSupportedWeaponElements(supportExpression);
    }

    private static IEnumerable<ArmorElement> ResolveCompatibleArmor(
        CharacterInventory inventory,
        Item magicItem)
    {
        var supportExpression = GetSetterValue(magicItem, "armor");
        return string.IsNullOrWhiteSpace(supportExpression)
            ? []
            : inventory.GetSupportedArmorElements(supportExpression);
    }

    private static string? GetSetterValue(Item item, string setterName) =>
        item.ElementSetters.ContainsSetter(setterName)
            ? item.ElementSetters.GetSetter(setterName)?.Value
            : null;
}
