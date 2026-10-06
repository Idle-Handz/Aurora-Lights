using Builder.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.App.Services;

/// <summary>
/// An add that has been worked out but not yet made. Nothing in the inventory has changed; hand it to
/// <see cref="InventoryItemAdder.Apply"/> to make it. Preparing first lets a caller check every part of
/// a larger change (a pack's components, a purchase's price) before committing to any of it.
/// </summary>
public sealed class PreparedInventoryAdd
{
    internal PreparedInventoryAdd(
        string itemName,
        int quantity,
        RefactoredEquipmentItem? mergeInto,
        IReadOnlyList<RefactoredEquipmentItem> newRows)
    {
        ItemName = itemName;
        Quantity = quantity;
        MergedInto = mergeInto;
        NewRows = newRows;
    }

    /// <summary>The item's name as it will read in the inventory, a magic item composed onto its base included.</summary>
    public string ItemName { get; }

    public int Quantity { get; }

    /// <summary>The existing stack this add grows, or null when it adds rows instead.</summary>
    public RefactoredEquipmentItem? MergedInto { get; }

    /// <summary>The rows this add creates; empty when it only grows <see cref="MergedInto"/>.</summary>
    public IReadOnlyList<RefactoredEquipmentItem> NewRows { get; }

    /// <summary>Every row the add touches: the stack it grows, or the rows it creates.</summary>
    public IReadOnlyList<RefactoredEquipmentItem> Rows =>
        MergedInto is { } stack ? [stack] : NewRows;
}

/// <summary>
/// The one place that decides how an item joins an inventory, for the Add Item dialog, Starting
/// Equipment, Extract Pack and the Shop alike.
/// </summary>
/// <remarks>
/// <para>An item the content marks stackable (torches, potions, scrolls, ammunition) joins an existing
/// stack of the same item, or starts one holding the whole quantity. Anything else becomes one inventory
/// row per unit, as legacy's Add and Buy do.</para>
/// <para>The engine marks a two-handed grip by putting the same row in both hands, so two daggers can only
/// be wielded one in each hand if they are two rows. For that reason an item that can be equipped is never
/// merged into a stack, even if the content flags it stackable: it also gets one row per unit.</para>
/// </remarks>
public static class InventoryItemAdder
{
    /// <summary>
    /// Works out how <paramref name="quantity"/> of <paramref name="element"/> would join the inventory.
    /// Returns null, having changed nothing, when it cannot: for a magic weapon or armor template that
    /// needs a base the caller did not give or that does not fit, or an element that is not inventory material.
    /// </summary>
    /// <param name="alternativeName">
    /// A name the rows should carry (starting-equipment choices, pack contents). A stack is only joined
    /// when it has the same name, so renamed stacks stay as the user left them.
    /// </param>
    public static PreparedInventoryAdd? Prepare(
        Character character,
        ElementBase element,
        int quantity,
        string? baseElementId = null,
        string? alternativeName = null)
    {
        int count = Math.Max(1, quantity);
        RefactoredEquipmentItem? first = InventoryItemFactory.Create(character.Inventory, element, baseElementId);
        if (first is null)
            return null;

        // Read before any alternative name is applied, so messages say what the item is.
        string name = StripAmountSuffix(first.DisplayName ?? element.Name, first.Amount);
        ApplyName(first, alternativeName);

        if (first.IsStackable && !IsWieldable(first))
        {
            RefactoredEquipmentItem? stack = FindStack(character, first, alternativeName);
            if (stack is not null)
                return new PreparedInventoryAdd(name, count, stack, []);

            first.Amount = count;
            return new PreparedInventoryAdd(name, count, null, [first]);
        }

        first.Amount = 1;
        var rows = new List<RefactoredEquipmentItem>(count) { first };
        for (int i = 1; i < count; i++)
        {
            RefactoredEquipmentItem? extra = InventoryItemFactory.Create(character.Inventory, element, baseElementId);
            if (extra is null)
                return null;

            extra.Amount = 1;
            ApplyName(extra, alternativeName);
            rows.Add(extra);
        }

        return new PreparedInventoryAdd(name, count, null, rows);
    }

    /// <summary>Makes a prepared add and brings the carried weight and attunement count up to date.</summary>
    public static void Apply(Character character, PreparedInventoryAdd prepared)
    {
        if (prepared.MergedInto is { } stack)
            stack.Amount += prepared.Quantity;

        foreach (RefactoredEquipmentItem row in prepared.NewRows)
            character.Inventory.Items.Add(row);

        character.Inventory.CalculateWeight();
        character.Inventory.CalculateAttunedItemCount();
    }

    /// <summary>Prepares and makes an add in one step. Null, with nothing changed, when it cannot be done.</summary>
    public static PreparedInventoryAdd? Add(
        Character character,
        ElementBase element,
        int quantity,
        string? baseElementId = null,
        string? alternativeName = null)
    {
        PreparedInventoryAdd? prepared = Prepare(character, element, quantity, baseElementId, alternativeName);
        if (prepared is not null)
            Apply(character, prepared);
        return prepared;
    }

    /// <summary>True when the item can be put in a hand, on the body or in a slot.</summary>
    internal static bool IsWieldable(RefactoredEquipmentItem row) =>
        row.IsEquippable || row.AdornerItem?.IsEquippable == true;

    // A stack is the same base item under the same magic template carrying the same name.
    private static RefactoredEquipmentItem? FindStack(
        Character character,
        RefactoredEquipmentItem candidate,
        string? alternativeName)
    {
        string wanted = NormalizeName(alternativeName);
        return character.Inventory.Items.FirstOrDefault(existing =>
            existing.IsStackable
            && !IsWieldable(existing)
            && string.Equals(existing.Item?.Id, candidate.Item?.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.AdornerItem?.Id, candidate.AdornerItem?.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeName(existing.AlternativeName), wanted, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyName(RefactoredEquipmentItem row, string? alternativeName)
    {
        if (!string.IsNullOrWhiteSpace(alternativeName))
            row.AlternativeName = alternativeName;
    }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? "" : name.Trim();

    // The inventory's display name carries the stack size ("Rope (3)"); messages show it as a quantity.
    internal static string StripAmountSuffix(string name, int amount)
    {
        string suffix = $" ({amount})";
        return amount > 1 && name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }
}
