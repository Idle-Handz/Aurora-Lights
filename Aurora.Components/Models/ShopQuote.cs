namespace Aurora.Components.Models;

/// <summary>
/// What a purchase would cost and whether it can go ahead, worked out the way the transaction
/// service will charge it. <see cref="Problem"/> is the one thing standing in the way, phrased for
/// the shopper, or null when nothing is.
/// </summary>
public sealed record ShopPurchaseQuote(
    long UnitCopper,
    long TotalCopper,
    bool NeedsBase,
    bool HasPrice,
    bool CanAfford,
    long ShortCopper,
    CoinPurse After,
    string? Problem)
{
    /// <summary>The purchase can be paid for now.</summary>
    public bool CanBuy => !NeedsBase && HasPrice && CanAfford;

    /// <summary>The item can be added without paying; only a missing base choice stops that.</summary>
    public bool CanAddFree => !NeedsBase;
}

/// <summary>What selling would pay and what the purse would hold afterwards.</summary>
public sealed record ShopSaleQuote(
    long UnitCopper,
    long ProceedsCopper,
    bool HasValue,
    CoinPurse After,
    string? Problem)
{
    public bool CanSell => HasValue;
}

public static class ShopQuotes
{
    /// <summary>
    /// Quotes buying <paramref name="quantity"/> of <paramref name="item"/>. For a magic template
    /// pass the chosen base, or null while none is chosen. <paramref name="overrideTotalCopper"/> is
    /// a price the shopper typed in for the whole lot.
    /// </summary>
    public static ShopPurchaseQuote ForPurchase(
        ShopItemModel item,
        ShopBaseOptionModel? chosenBase,
        int quantity,
        long? overrideTotalCopper,
        CoinPurse wallet,
        ShopCoinOptions options = default)
    {
        bool needsBase = item.IsTemplate && chosenBase is null;
        long unit = item.IsTemplate ? chosenBase?.UnitPriceCopper ?? 0 : item.UnitPriceCopper;
        long total = ShopPricing.PurchaseTotal(unit, quantity, overrideTotalCopper);
        bool hasPrice = total > 0;
        bool canAfford = wallet.CanAfford(total);
        long shortBy = canAfford ? 0 : total - wallet.TotalCopper;

        CoinPurse after = wallet;
        if (hasPrice && canAfford)
            wallet.TryPay(total, out after, options);

        string? problem = null;
        if (needsBase)
            problem = "Choose a base item first.";
        else if (!hasPrice)
            problem = "Set a price to buy it, or add it for free.";
        else if (!canAfford)
            problem = $"You need {CoinPurse.FormatCopper(shortBy)} more.";

        return new ShopPurchaseQuote(unit, total, needsBase, hasPrice, canAfford, shortBy, after, problem);
    }

    /// <summary>Quotes selling <paramref name="quantity"/> of an inventory row at the given rate.</summary>
    public static ShopSaleQuote ForSale(
        ShopInventoryEntryModel entry,
        int quantity,
        int ratePercent,
        long? overrideTotalCopper,
        CoinPurse wallet,
        ShopCoinOptions options = default)
    {
        int sellable = Math.Clamp(quantity, 1, Math.Max(1, entry.Amount));
        long proceeds = ShopPricing.SaleProceeds(entry.UnitPriceCopper, sellable, ratePercent, overrideTotalCopper);
        bool hasValue = proceeds > 0;

        return new ShopSaleQuote(
            entry.UnitPriceCopper,
            proceeds,
            hasValue,
            hasValue ? wallet.Receive(proceeds, options) : wallet,
            hasValue ? null : "Set a price to sell it.");
    }
}
