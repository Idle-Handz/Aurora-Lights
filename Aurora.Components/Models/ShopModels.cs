namespace Aurora.Components.Models;

public enum ShopMode
{
    Buy,
    Sell,
}

public enum ShopSort
{
    Name,
    PriceLowToHigh,
    PriceHighToLow,
    Weight,
    Rarity,
}

/// <summary>
/// What the shop's filters and sorts need to know about a row, so one set of filters narrows both
/// the catalog being bought from and the inventory being sold out of.
/// </summary>
public interface IShopListing
{
    string Name { get; }
    string Department { get; }
    string Category { get; }
    string Subtype { get; }
    string Rarity { get; }
    string Source { get; }

    /// <summary>The listed price of one unit in copper; zero when the item has no price.</summary>
    long UnitPriceCopper { get; }

    decimal WeightPounds { get; }

    /// <summary>Normalised text the search box matches against.</summary>
    string SearchKey { get; }
}

/// <summary>One thing the shop can sell the character. Immutable so a built catalog can be cached.</summary>
public sealed record ShopItemModel(
    string Id,
    string Name,
    string Type,
    string Source,
    string Category,
    string Department,
    string Subtype,
    string Rarity,
    long UnitPriceCopper,
    string DisplayWeight,
    decimal WeightPounds,
    string Summary,
    bool RequiresAttunement,
    bool IsStackable,
    bool IsTemplate,
    bool PriceFollowsBase,
    string SearchKey) : IShopListing
{
    /// <summary>
    /// True when the item lists a price of its own. A template whose price follows its base item has
    /// none until a base is chosen, so the list shows that instead of "no listed price".
    /// </summary>
    public bool HasPrice => UnitPriceCopper > 0;
}

/// <summary>A row of the character's inventory, as the sell side lists it.</summary>
public sealed record ShopInventoryEntryModel(
    string Identifier,
    string ElementId,
    string Name,
    string Source,
    string Category,
    string Department,
    string Subtype,
    string Rarity,
    int Amount,
    bool IsStackable,
    bool IsEquipped,
    long UnitPriceCopper,
    string DisplayWeight,
    decimal WeightPounds,
    string SearchKey) : IShopListing
{
    public bool HasValue => UnitPriceCopper > 0;
}

public sealed record ShopStatModel(string Label, string Value);

/// <summary>A base item a magic weapon or armor template can be built on, with the price that choice makes.</summary>
public sealed record ShopBaseOptionModel(string Id, string Name, long UnitPriceCopper);

/// <summary>The part of an item the list does not carry: its rules text, stats and any base choices.</summary>
public sealed record ShopItemDetailModel(
    string Id,
    string Name,
    string DescriptionHtml,
    string PlainDescription,
    IReadOnlyList<ShopStatModel> Stats,
    IReadOnlyList<ShopBaseOptionModel> BaseOptions);

/// <summary>
/// A request to take an item into the inventory. <see cref="PriceOverrideCopper"/> replaces the
/// whole listed price (haggling, or a price the DM set for an item with none); <see cref="Free"/>
/// adds the item without touching the purse.
/// </summary>
public sealed record ShopPurchaseRequest(
    string ItemId,
    string? BaseItemId,
    int Quantity,
    long? PriceOverrideCopper,
    bool Free);

/// <summary>A request to sell part or all of an inventory row.</summary>
public sealed record ShopSaleRequest(
    string Identifier,
    int Quantity,
    int RatePercent,
    long? PriceOverrideCopper);

/// <summary>A department of the shop with the shelves (item categories) in it.</summary>
public sealed record ShopDepartmentModel(string Name, int Count, IReadOnlyList<ShopShelfModel> Shelves);

public sealed record ShopShelfModel(string Name, int Count);
