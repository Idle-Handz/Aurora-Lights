namespace Aurora.Components.Models;

/// <summary>What the shopper has narrowed the shop to.</summary>
public sealed record ShopFilterState
{
    public string Query { get; init; } = string.Empty;
    public string? Department { get; init; }
    public string? Category { get; init; }
    public string? Subtype { get; init; }
    public string? Rarity { get; init; }
    public string? Source { get; init; }
    public bool AffordableOnly { get; init; }
    public bool PricedOnly { get; init; }
    public ShopSort Sort { get; init; } = ShopSort.Name;
}

/// <summary>
/// Narrowing, sorting and counting for the shop. Depends on nothing but the listings, so the
/// catalog being bought from and the inventory being sold out of are filtered by the same rules.
/// </summary>
public static class ShopCatalogFilter
{
    /// <summary>
    /// Rows passing everything except the department, shelf and subtype choice. The department
    /// rail counts these, so it shows where a search or filter has results before one is picked.
    /// </summary>
    public static IReadOnlyList<T> ApplyScopedFilters<T>(
        IEnumerable<T> listings,
        ShopFilterState state,
        long walletCopper)
        where T : IShopListing
    {
        IEnumerable<T> result = listings;

        string[] terms = SplitQuery(state.Query);
        if (terms.Length > 0)
            result = result.Where(item => terms.All(term => item.SearchKey.Contains(term, StringComparison.Ordinal)));

        if (!string.IsNullOrWhiteSpace(state.Rarity))
        {
            string wanted = state.Rarity!;
            result = string.Equals(wanted, MundaneRarity, StringComparison.OrdinalIgnoreCase)
                ? result.Where(item => item.Rarity.Length == 0)
                : result.Where(item => string.Equals(item.Rarity, wanted, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(state.Source))
        {
            string source = CompendiumFilter.NormalizeSourceFilterKey(state.Source);
            result = result.Where(item =>
                string.Equals(CompendiumFilter.NormalizeSourceFilterKey(item.Source), source, StringComparison.Ordinal));
        }

        if (state.PricedOnly)
            result = result.Where(item => item.UnitPriceCopper > 0);

        if (state.AffordableOnly)
            result = result.Where(item => item.UnitPriceCopper > 0 && item.UnitPriceCopper <= walletCopper);

        return result.ToList();
    }

    /// <summary>Narrows scoped rows to the chosen department, shelf and subtype, then sorts them.</summary>
    public static IReadOnlyList<T> Apply<T>(
        IEnumerable<T> scopedListings,
        ShopFilterState state)
        where T : IShopListing
    {
        IEnumerable<T> result = scopedListings;

        if (!string.IsNullOrWhiteSpace(state.Department))
            result = result.Where(item => string.Equals(item.Department, state.Department, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(state.Category))
            result = result.Where(item => string.Equals(item.Category, state.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(state.Subtype))
            result = result.Where(item => string.Equals(item.Subtype, state.Subtype, StringComparison.OrdinalIgnoreCase));

        return Sort(result, state.Sort);
    }

    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> listings, ShopSort sort)
        where T : IShopListing
    {
        // Name is always the final tie-break so the order is stable and predictable.
        IOrderedEnumerable<T> ordered = sort switch
        {
            ShopSort.PriceLowToHigh => listings
                .OrderBy(item => item.UnitPriceCopper == 0)
                .ThenBy(item => item.UnitPriceCopper),
            ShopSort.PriceHighToLow => listings
                .OrderBy(item => item.UnitPriceCopper == 0)
                .ThenByDescending(item => item.UnitPriceCopper),
            ShopSort.Weight => listings.OrderBy(item => item.WeightPounds),
            ShopSort.Rarity => listings.OrderBy(item => ShopTaxonomy.RarityRank(item.Rarity)),
            _ => listings.OrderBy(item => 0),
        };

        return ordered
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The departments present in <paramref name="scopedListings"/> with their shelves, in shop
    /// order and with counts. Empty departments and shelves are left out.
    /// </summary>
    public static IReadOnlyList<ShopDepartmentModel> BuildDepartments<T>(IEnumerable<T> scopedListings)
        where T : IShopListing
    {
        return scopedListings
            .GroupBy(item => item.Department, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => ShopTaxonomy.DepartmentRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ShopDepartmentModel(
                group.Key,
                group.Count(),
                group.GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(shelf => ShopTaxonomy.ShelfRank(shelf.Key))
                    .ThenBy(shelf => shelf.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(shelf => new ShopShelfModel(shelf.Key, shelf.Count()))
                    .ToList()))
            .ToList();
    }

    /// <summary>Subtypes present on a shelf, with counts, for the chip row under the shelf name.</summary>
    public static IReadOnlyList<ShopShelfModel> BuildSubtypes<T>(IEnumerable<T> shelfListings)
        where T : IShopListing
    {
        return shelfListings
            .Where(item => item.Subtype.Length > 0)
            .GroupBy(item => item.Subtype, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ShopShelfModel(group.Key, group.Count()))
            .ToList();
    }

    /// <summary>Rarities present, mundane included, in rank order, for the rarity filter.</summary>
    public static IReadOnlyList<string> BuildRarities<T>(IEnumerable<T> listings)
        where T : IShopListing
    {
        var present = listings.Select(item => item.Rarity).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<string>();
        if (present.Contains(string.Empty))
            result.Add(MundaneRarity);

        result.AddRange(present
            .Where(rarity => rarity.Length > 0)
            .OrderBy(ShopTaxonomy.RarityRank)
            .ThenBy(rarity => rarity, StringComparer.OrdinalIgnoreCase));
        return result;
    }

    public static IReadOnlyList<string> BuildSources<T>(IEnumerable<T> listings)
        where T : IShopListing
    {
        return listings
            .Select(item => item.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .GroupBy(CompendiumFilter.NormalizeSourceFilterKey, StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0)
            .Select(group => group.OrderByDescending(source => source.Contains('’')).First())
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The label of the rarity filter that selects items with no rarity.</summary>
    public const string MundaneRarity = "Mundane";

    /// <summary>Builds the text a listing is searched by, folded the way the query is.</summary>
    public static string BuildSearchKey(params string?[] parts) =>
        CompendiumFilter.NormalizeSearchKey(string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part))));

    private static string[] SplitQuery(string? query) =>
        CompendiumFilter.NormalizeSearchKey(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
