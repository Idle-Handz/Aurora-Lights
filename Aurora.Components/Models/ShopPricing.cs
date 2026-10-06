using System.Globalization;

namespace Aurora.Components.Models;

/// <summary>
/// The shop's price arithmetic. The screen quotes a price with these and the host charges it with
/// the same functions, so what a button promises is what the purse pays.
/// </summary>
public static class ShopPricing
{
    public const int DefaultSellRatePercent = 50;

    /// <summary>The most of one item a single purchase or sale can cover; matches the inventory picker.</summary>
    public const int MaxQuantity = 999;

    /// <summary>The sell rates offered; the first is the default.</summary>
    public static IReadOnlyList<int> SellRatePresets { get; } = [50, 25, 75, 100];

    /// <summary>
    /// An item's listed cost in copper. Zero when there is no cost or the currency is missing or
    /// unrecognised: legacy throws on a missing currency, and the shop treats the item as having
    /// no listed price instead of guessing a denomination.
    /// </summary>
    public static long ToCopper(int cost, string? currencyAbbreviation)
    {
        if (cost <= 0)
            return 0;

        long perUnit = currencyAbbreviation?.Trim().ToLowerInvariant() switch
        {
            "cp" or "copper" => 1,
            "sp" or "silver" => CoinPurse.CopperPerSilver,
            "ep" or "electrum" => CoinPurse.CopperPerElectrum,
            "gp" or "gold" => CoinPurse.CopperPerGold,
            "pp" or "platinum" => CoinPurse.CopperPerPlatinum,
            _ => 0,
        };

        return cost * perUnit;
    }

    /// <summary>
    /// The price of one unit. A plain item costs what it lists. A magic item composed onto a base
    /// item (a +1 sword, dragon scale mail) costs the base plus its own price, unless it declares a
    /// cost of its own, in which case that cost is the whole price. This is legacy's buy rule.
    /// </summary>
    public static long UnitPrice(long baseCopper, long? adornerCopper = null, bool adornerOverridesCost = false)
    {
        if (adornerCopper is null)
            return baseCopper;

        return adornerOverridesCost ? adornerCopper.Value : baseCopper + adornerCopper.Value;
    }

    /// <summary>The cost of buying <paramref name="quantity"/> units, or an override for the whole lot.</summary>
    public static long PurchaseTotal(long unitCopper, int quantity, long? overrideTotalCopper = null)
    {
        if (overrideTotalCopper is { } total)
            return Math.Max(0, total);

        return Math.Max(0, unitCopper) * Math.Max(1, quantity);
    }

    /// <summary>
    /// What selling pays: the unit value times the quantity at <paramref name="ratePercent"/>,
    /// rounded down to a whole copper piece. An override replaces the figure for the whole lot.
    /// </summary>
    public static long SaleProceeds(long unitCopper, int quantity, int ratePercent, long? overrideTotalCopper = null)
    {
        if (overrideTotalCopper is { } total)
            return Math.Max(0, total);

        long rate = Math.Clamp(ratePercent, 0, 100);
        return Math.Max(0, unitCopper) * Math.Max(1, quantity) * rate / 100;
    }

    /// <summary>Gold, as typed in a price field, to copper. Null for blank, negative or unparseable text.</summary>
    public static long? ParseGold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal gold)
            || gold < 0
            || gold > 1_000_000_000m)
        {
            return null;
        }

        return (long)Math.Round(gold * CoinPurse.CopperPerGold, MidpointRounding.AwayFromZero);
    }
}
