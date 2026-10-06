using System.Globalization;
using System.Text;

namespace Aurora.Components.Models;

/// <summary>
/// The coins a character holds, plus the arithmetic a shop needs: whether a price can be met, which
/// coins change hands when it is, and what a sale pays out. It is a plain value so a screen can
/// preview a transaction and a test can pin it down without touching the engine's live
/// <c>Coinage</c> object; the host writes the result back through the engine's own coin setter.
/// </summary>
/// <remarks>
/// Coins are handed over smallest denomination first, and only when no combination of whole coins
/// covers the price exactly is the smallest coin that covers the remainder broken, with change
/// returned in gold, silver and copper. Platinum is never handed out. Electrum is handed out only
/// when <see cref="ShopCoinOptions.UseElectrum"/> is set: the engine's own <c>Coinage.Withdraw</c>
/// converts value correctly but always breaks coins down through electrum, so buying a 1 cp torch
/// with a gold piece returns an electrum coin even at a table that never uses one.
/// </remarks>
public readonly record struct CoinPurse(long Copper, long Silver, long Electrum, long Gold, long Platinum)
{
    public const long CopperPerSilver = 10;
    public const long CopperPerElectrum = 50;
    public const long CopperPerGold = 100;
    public const long CopperPerPlatinum = 1000;

    private static readonly long[] CopperValues = [1, CopperPerSilver, CopperPerElectrum, CopperPerGold, CopperPerPlatinum];
    private static readonly string[] Abbreviations = ["cp", "sp", "ep", "gp", "pp"];

    /// <summary>Everything held, in copper pieces.</summary>
    public long TotalCopper =>
        Copper + (Silver * CopperPerSilver) + (Electrum * CopperPerElectrum)
        + (Gold * CopperPerGold) + (Platinum * CopperPerPlatinum);

    public bool CanAfford(long costCopper) => costCopper <= TotalCopper;

    /// <summary>
    /// Pays <paramref name="costCopper"/>. Returns false, leaving <paramref name="after"/> equal to
    /// this purse, when the price is negative or more than the purse holds.
    /// </summary>
    public bool TryPay(long costCopper, out CoinPurse after, ShopCoinOptions options = default)
    {
        after = this;
        if (costCopper < 0 || costCopper > TotalCopper)
            return false;
        if (costCopper == 0)
            return true;

        long[] held = ToArray();
        long[] spent = new long[held.Length];
        long remaining = costCopper;

        // Smallest coins first, never overshooting. Afterwards every denomination is either spent
        // out or worth more than what is still owed, so a coin that covers the rest must exist.
        for (int i = 0; i < held.Length; i++)
        {
            long take = Math.Min(held[i], remaining / CopperValues[i]);
            spent[i] = take;
            remaining -= take * CopperValues[i];
        }

        long change = 0;
        if (remaining > 0)
        {
            for (int i = 0; i < held.Length; i++)
            {
                if (held[i] - spent[i] > 0 && CopperValues[i] > remaining)
                {
                    spent[i]++;
                    change = CopperValues[i] - remaining;
                    break;
                }
            }
        }

        for (int i = 0; i < held.Length; i++)
            held[i] -= spent[i];

        after = FromArray(held).Receive(change, options);
        return true;
    }

    /// <summary>
    /// Adds a payout as the fewest gold, silver and copper pieces, with electrum between gold and
    /// silver when <see cref="ShopCoinOptions.UseElectrum"/> is set. Platinum is never handed out as
    /// change or sale proceeds.
    /// </summary>
    public CoinPurse Receive(long amountCopper, ShopCoinOptions options = default)
    {
        if (amountCopper <= 0)
            return this;

        long gold = amountCopper / CopperPerGold;
        long rest = amountCopper % CopperPerGold;

        long electrum = 0;
        if (options.UseElectrum)
        {
            electrum = rest / CopperPerElectrum;
            rest %= CopperPerElectrum;
        }

        long silver = rest / CopperPerSilver;
        long copper = rest % CopperPerSilver;
        return new CoinPurse(Copper + copper, Silver + silver, Electrum + electrum, Gold + gold, Platinum);
    }

    /// <summary>The purse as a short label, largest coin first: "3 gp 5 sp".</summary>
    public string Describe()
    {
        var parts = new List<string>(5);
        long[] held = ToArray();
        for (int i = held.Length - 1; i >= 0; i--)
        {
            if (held[i] != 0)
                parts.Add(Format(held[i], Abbreviations[i]));
        }

        return parts.Count == 0 ? "no coins" : string.Join(' ', parts);
    }

    /// <summary>The signed per-coin change between two purses: "-1 gp, +4 sp".</summary>
    public static string DescribeChange(CoinPurse before, CoinPurse after)
    {
        long[] was = before.ToArray();
        long[] now = after.ToArray();
        var parts = new List<string>(5);
        for (int i = was.Length - 1; i >= 0; i--)
        {
            long difference = now[i] - was[i];
            if (difference == 0)
                continue;

            string sign = difference > 0 ? "+" : "-";
            parts.Add($"{sign}{Math.Abs(difference).ToString("N0", CultureInfo.InvariantCulture)} {Abbreviations[i]}");
        }

        return parts.Count == 0 ? "no change" : string.Join(", ", parts);
    }

    /// <summary>A price in copper as gold, silver and copper: "12 gp 5 sp 3 cp"; "50,000 gp".</summary>
    public static string FormatCopper(long copper)
    {
        if (copper <= 0)
            return "0 cp";

        long gold = copper / CopperPerGold;
        long silver = (copper % CopperPerGold) / CopperPerSilver;
        long remainder = copper % CopperPerSilver;

        var text = new StringBuilder();
        void Append(long amount, string abbreviation)
        {
            if (amount == 0)
                return;
            if (text.Length > 0)
                text.Append(' ');
            text.Append(Format(amount, abbreviation));
        }

        Append(gold, "gp");
        Append(silver, "sp");
        Append(remainder, "cp");
        return text.ToString();
    }

    private static string Format(long amount, string abbreviation) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} {abbreviation}";

    private long[] ToArray() => [Copper, Silver, Electrum, Gold, Platinum];

    private static CoinPurse FromArray(long[] coins) =>
        new(coins[0], coins[1], coins[2], coins[3], coins[4]);
}
