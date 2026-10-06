using Aurora.Components.Models;

namespace Aurora.Tests.Helpers;

/// <summary>Builders for shop models, so a test states only what it is about.</summary>
public static class ShopTestData
{
    public static ShopItemModel Item(
        string name,
        string category = "Adventuring Gear",
        string type = "Item",
        long priceCopper = 0,
        string rarity = "",
        string source = "System Reference Document",
        string subtype = "",
        decimal weight = 0m,
        string summary = "",
        bool template = false,
        bool followsBase = false,
        bool attunement = false,
        string? id = null)
    {
        var (shelf, department) = ShopTaxonomy.Classify(type, category);
        return new ShopItemModel(
            Id: id ?? "ID_" + name.ToUpperInvariant().Replace(' ', '_').Replace(",", string.Empty),
            Name: name,
            Type: type,
            Source: source,
            Category: shelf,
            Department: department,
            Subtype: subtype,
            Rarity: ShopTaxonomy.NormalizeRarity(rarity),
            UnitPriceCopper: priceCopper,
            DisplayWeight: weight == 0 ? "—" : $"{weight} lb.",
            WeightPounds: weight,
            Summary: summary,
            RequiresAttunement: attunement,
            IsStackable: false,
            IsTemplate: template,
            PriceFollowsBase: followsBase,
            SearchKey: ShopCatalogFilter.BuildSearchKey(name, shelf, department, subtype, rarity, type));
    }

    public static ShopInventoryEntryModel Owned(
        string name,
        string identifier,
        long unitCopper,
        int amount = 1,
        string category = "Adventuring Gear",
        string type = "Item",
        bool equipped = false,
        string rarity = "")
    {
        var (shelf, department) = ShopTaxonomy.Classify(type, category);
        return new ShopInventoryEntryModel(
            Identifier: identifier,
            ElementId: "ID_" + name.ToUpperInvariant().Replace(' ', '_'),
            Name: name,
            Source: "System Reference Document",
            Category: shelf,
            Department: department,
            Subtype: string.Empty,
            Rarity: ShopTaxonomy.NormalizeRarity(rarity),
            Amount: amount,
            IsStackable: amount > 1,
            IsEquipped: equipped,
            UnitPriceCopper: unitCopper,
            DisplayWeight: "1 lb.",
            WeightPounds: 1m,
            SearchKey: ShopCatalogFilter.BuildSearchKey(name, shelf, department, rarity));
    }
}
