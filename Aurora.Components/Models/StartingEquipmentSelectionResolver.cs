namespace Aurora.Components.Models;

/// <summary>Resolves selected starting equipment choices into inventory items and gold.</summary>
public static class StartingEquipmentSelectionResolver
{
    public static ResolvedStartingEquipment Resolve(
        StartingEquipmentBlock classBlock,
        StartingEquipmentBlock backgroundBlock,
        IReadOnlyList<int> classOptionIndexes,
        IReadOnlyList<int> backgroundOptionIndexes,
        IReadOnlyDictionary<string, string> categoryPicks,
        bool takeClassGold,
        bool takeBackgroundGold = false)
    {
        var items = new List<(string ElementId, int Count, string? Name)>();
        var coins = CoinGrant.Empty;
        bool tookRolledGold = false;

        if (takeClassGold && classBlock.GoldAlternative != null)
        {
            if (classBlock.GoldAlternative.IsRolled)
                tookRolledGold = true;
            else
                coins = coins.AddGold(classBlock.GoldAlternative.Amount ?? 0);
        }
        else
        {
            coins = coins.Add(AddChoiceItems(items, classBlock.Choices, classOptionIndexes, categoryPicks, "c"));
            AddFixedItems(items, classBlock.FixedItems, categoryPicks, static index => $"cf{index}");
            coins = coins.Add(EffectiveFixedCoins(classBlock));
        }

        if (takeBackgroundGold && backgroundBlock.GoldAlternative != null)
        {
            if (backgroundBlock.GoldAlternative.IsRolled)
                tookRolledGold = true;
            else
                coins = coins.AddGold(backgroundBlock.GoldAlternative.Amount ?? 0);
        }
        else
        {
            coins = coins.Add(AddChoiceItems(items, backgroundBlock.Choices, backgroundOptionIndexes, categoryPicks, "bc"));
            AddFixedItems(items, backgroundBlock.FixedItems, categoryPicks, static index => $"bg_i{index}");
            coins = coins.Add(EffectiveFixedCoins(backgroundBlock));
        }

        return new ResolvedStartingEquipment(items, coins, tookRolledGold);
    }

    private static CoinGrant AddChoiceItems(
        List<(string ElementId, int Count, string? Name)> items,
        IReadOnlyList<EquipmentChoice> choices,
        IReadOnlyList<int> optionIndexes,
        IReadOnlyDictionary<string, string> categoryPicks,
        string keyPrefix)
    {
        var coins = CoinGrant.Empty;
        for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
        {
            int optionIndex = GetOptionIndex(optionIndexes, choiceIndex);
            var options = choices[choiceIndex].Options;
            if (optionIndex >= options.Count) continue;
            var selectedOption = options[optionIndex];

            foreach (var (item, itemIndex) in selectedOption.Items.Select((x, i) => (x, i)))
            {
                string key = $"{keyPrefix}{choiceIndex}_o{optionIndex}_i{itemIndex}";
                AddResolvedItem(items, item, key, categoryPicks);
            }

            coins = coins.Add(selectedOption.Coins);
        }

        return coins;
    }

    private static void AddFixedItems(
        List<(string ElementId, int Count, string? Name)> items,
        IReadOnlyList<EquipmentItem> fixedItems,
        IReadOnlyDictionary<string, string> categoryPicks,
        Func<int, string> keyForIndex)
    {
        foreach (var (item, itemIndex) in fixedItems.Select((x, i) => (x, i)))
            AddResolvedItem(items, item, keyForIndex(itemIndex), categoryPicks);
    }

    private static void AddResolvedItem(
        List<(string ElementId, int Count, string? Name)> items,
        EquipmentItem item,
        string categoryPickKey,
        IReadOnlyDictionary<string, string> categoryPicks)
    {
        string? id = item.IsCategory
            ? (categoryPicks.TryGetValue(categoryPickKey, out string? pickedId) ? pickedId : null)
            : item.Id;

        if (!string.IsNullOrEmpty(id))
            items.Add((id, item.Count, item.Name));
    }

    private static int GetOptionIndex(IReadOnlyList<int> optionIndexes, int choiceIndex) =>
        choiceIndex < optionIndexes.Count ? optionIndexes[choiceIndex] : 0;

    private static CoinGrant EffectiveFixedCoins(StartingEquipmentBlock block) =>
        block.FixedCoins.HasAny ? block.FixedCoins : new CoinGrant(Gold: block.FixedGold);
}

public sealed record ResolvedStartingEquipment(
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
