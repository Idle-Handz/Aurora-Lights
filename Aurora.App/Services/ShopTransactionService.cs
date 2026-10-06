using Aurora.Components.Models;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.App.Services;

public enum ShopOutcome
{
    Completed,
    UnknownItem,
    NeedsBaseItem,
    IncompatibleBase,
    NoPrice,
    InsufficientFunds,
    NotOwned,
}

/// <summary>What a purchase or sale did. On any outcome but <see cref="ShopOutcome.Completed"/> nothing changed.</summary>
public sealed record ShopTransactionResult(
    ShopOutcome Outcome,
    string Message,
    CoinPurse Before,
    CoinPurse After,
    long AmountCopper,
    string ItemName,
    int Quantity)
{
    public bool Succeeded => Outcome == ShopOutcome.Completed;
}

/// <summary>
/// Buys items into, and sells items out of, a character's inventory. Every check runs before the
/// first change, so a refused transaction leaves the inventory and the purse exactly as they were.
/// Callers hold the character's <see cref="CharacterContext"/> scope, then refresh the snapshot and
/// persist, as the Equipment page does after any inventory change.
/// </summary>
public static class ShopTransactionService
{
    public const int MaxQuantity = ShopPricing.MaxQuantity;

    /// <summary>
    /// Takes an item into the inventory, paying for it from the purse unless the request is free.
    /// Items are built through the engine's inventory factory, so a magic weapon or armor template
    /// is composed onto the chosen base exactly as the inventory picker composes it.
    /// <paramref name="options"/> says whether change may be made in electrum.
    /// </summary>
    public static ShopTransactionResult Purchase(
        Character character,
        ShopPurchaseRequest request,
        ShopCoinOptions options = default)
    {
        CoinPurse before = ShopCatalogService.GetPurse(character);
        ShopTransactionResult Refuse(ShopOutcome outcome, string message, string name = "", int quantity = 0) =>
            new(outcome, message, before, before, 0, name, quantity);

        int quantity = Math.Clamp(request.Quantity, 1, MaxQuantity);
        if (DataManager.Current.ElementsCollection.GetElement(request.ItemId) is not Item item)
            return Refuse(ShopOutcome.UnknownItem, "That item is not in the loaded content any more.");

        Item? baseItem = null;
        bool isTemplate = InventoryItemFactory.GetTemplateKind(item) is not null;
        if (isTemplate)
        {
            if (string.IsNullOrWhiteSpace(request.BaseItemId))
                return Refuse(ShopOutcome.NeedsBaseItem, $"Choose a base item for {item.Name} first.", item.Name);

            baseItem = InventoryItemFactory.GetCompatibleBaseItems(character.Inventory, item)
                .FirstOrDefault(candidate => candidate.Id.Equals(request.BaseItemId, StringComparison.OrdinalIgnoreCase));
            if (baseItem is null)
                return Refuse(ShopOutcome.IncompatibleBase, $"That base item cannot be used for {item.Name}.", item.Name);
        }

        long unitCopper = isTemplate
            ? ShopPricing.UnitPrice(
                ShopPricing.ToCopper(baseItem!.Cost, baseItem.CurrencyAbbreviation),
                ShopPricing.ToCopper(item.Cost, item.CurrencyAbbreviation),
                item is MagicItemElement { OverrideCost: true })
            : ShopPricing.ToCopper(item.Cost, item.CurrencyAbbreviation);
        long totalCopper = request.Free
            ? 0
            : ShopPricing.PurchaseTotal(unitCopper, quantity, request.PriceOverrideCopper);

        if (!request.Free && totalCopper <= 0)
        {
            return Refuse(
                ShopOutcome.NoPrice,
                $"{item.Name} has no listed price. Enter a price to buy it, or add it for free.",
                item.Name,
                quantity);
        }

        CoinPurse after = before;
        if (!request.Free && !before.TryPay(totalCopper, out after, options))
        {
            return Refuse(
                ShopOutcome.InsufficientFunds,
                $"{item.Name} costs {CoinPurse.FormatCopper(totalCopper)}; you are "
                + $"{CoinPurse.FormatCopper(totalCopper - before.TotalCopper)} short.",
                item.Name,
                quantity);
        }

        // Work out the rows before touching the inventory or the purse, so a failure leaves no partial
        // purchase. The adder decides stacking: one row per unit unless the item is a stackable that
        // cannot be wielded.
        PreparedInventoryAdd? prepared = InventoryItemAdder.Prepare(character, item, quantity, baseItem?.Id);
        if (prepared is null)
            return Refuse(ShopOutcome.UnknownItem, $"{item.Name} could not be added to the inventory.", item.Name, quantity);

        string name = prepared.ItemName;

        // Coins first, so the carried weight the add recalculates includes their new weight.
        if (!request.Free)
            ApplyPurse(character, after);
        InventoryItemAdder.Apply(character, prepared);

        string quantityText = quantity > 1 ? $"{quantity}× " : string.Empty;
        string message = request.Free
            ? $"Added {quantityText}{name} to your inventory."
            : $"Bought {quantityText}{name} for {CoinPurse.FormatCopper(totalCopper)}. "
              + $"{CoinPurse.FormatCopper(after.TotalCopper)} left.";
        return new ShopTransactionResult(ShopOutcome.Completed, message, before, after, totalCopper, name, quantity);
    }

    /// <summary>Sells part or all of an inventory row, paying out in gold, silver and copper.</summary>
    public static ShopTransactionResult Sell(
        Character character,
        ShopSaleRequest request,
        ShopCoinOptions options = default)
    {
        CoinPurse before = ShopCatalogService.GetPurse(character);
        ShopTransactionResult Refuse(ShopOutcome outcome, string message, string name = "", int quantity = 0) =>
            new(outcome, message, before, before, 0, name, quantity);

        RefactoredEquipmentItem? owned = character.Inventory.Items
            .FirstOrDefault(item => item.Identifier == request.Identifier);
        if (owned?.Item is null)
            return Refuse(ShopOutcome.NotOwned, "That item is no longer in the inventory.");

        string name = InventoryItemAdder.StripAmountSuffix(owned.DisplayName ?? owned.Name, owned.Amount);
        int quantity = Math.Clamp(request.Quantity, 1, Math.Max(1, owned.Amount));
        long proceeds = ShopPricing.SaleProceeds(
            ShopCatalogService.UnitValue(owned), quantity, request.RatePercent, request.PriceOverrideCopper);
        if (proceeds <= 0)
        {
            return Refuse(
                ShopOutcome.NoPrice,
                $"{name} has no listed value. Enter a price to sell it, or remove it on the Equipment page.",
                name,
                quantity);
        }

        if (quantity >= owned.Amount)
            EquipmentService.RemoveItem(character, owned.Identifier);
        else
            owned.Amount -= quantity;

        CoinPurse after = before.Receive(proceeds, options);
        ApplyPurse(character, after);
        character.Inventory.CalculateWeight();
        character.Inventory.CalculateAttunedItemCount();

        string quantityText = quantity > 1 ? $"{quantity}× " : string.Empty;
        return new ShopTransactionResult(
            ShopOutcome.Completed,
            $"Sold {quantityText}{name} for {CoinPurse.FormatCopper(proceeds)}.",
            before,
            after,
            proceeds,
            name,
            quantity);
    }

    // The engine's own coin setter, the one the Equipment and Session pages write through.
    private static void ApplyPurse(Character character, CoinPurse purse) =>
        character.Inventory.Coins.Set(purse.Copper, purse.Silver, purse.Electrum, purse.Gold, purse.Platinum);
}
