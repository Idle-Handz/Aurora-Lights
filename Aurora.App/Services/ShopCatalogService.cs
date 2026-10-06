using System.Collections.Specialized;
using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.App.Services;

/// <summary>
/// Builds what the shop sells (the loaded item catalog, organised onto shelves) and what it can buy
/// back (the character's inventory). The catalog is built once off the UI thread and kept until the
/// engine replaces its element collection; source restrictions are applied on every read because
/// they can change while the shop is open.
/// </summary>
public sealed class ShopCatalogService : IDisposable
{
    private sealed record CatalogEntry(ShopItemModel Model, Item Element);

    private readonly object _gate = new();
    private Task<IReadOnlyList<CatalogEntry>>? _build;
    private ElementBaseCollection? _observed;
    private volatile bool _stale = true;

    /// <summary>
    /// The items the shop lists for a character whose content is limited by
    /// <paramref name="restrictions"/>. The same items the inventory picker offers, less anything
    /// hidden from the inventory.
    /// </summary>
    public async Task<IReadOnlyList<ShopItemModel>> GetCatalogAsync(
        BuildSourceRestrictionSnapshot? restrictions = null,
        CancellationToken cancellationToken = default)
    {
        restrictions ??= BuildSourceRestrictionSnapshot.CaptureCurrent();
        IReadOnlyList<CatalogEntry> entries = await EnsureBuild().WaitAsync(cancellationToken).ConfigureAwait(false);

        return entries
            .Where(entry => restrictions.Allows(entry.Element))
            .Select(entry => entry.Model)
            .ToList();
    }

    /// <summary>Discards the built catalog; the next read rebuilds it.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _stale = true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_observed is not null)
                _observed.CollectionChanged -= OnElementsChanged;
            _observed = null;
        }
    }

    private Task<IReadOnlyList<CatalogEntry>> EnsureBuild()
    {
        lock (_gate)
        {
            // The engine swaps content in with ReplaceAll, which raises a Reset; follow whichever
            // collection is current so a reload always invalidates the cache.
            ElementBaseCollection current = DataManager.Current.ElementsCollection;
            if (!ReferenceEquals(_observed, current))
            {
                if (_observed is not null)
                    _observed.CollectionChanged -= OnElementsChanged;
                _observed = current;
                _observed.CollectionChanged += OnElementsChanged;
                _stale = true;
            }

            if (_build is null || _stale || _build.IsFaulted)
            {
                _stale = false;
                _build = Task.Run(BuildEntries);
            }

            return _build;
        }
    }

    private void OnElementsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _stale = true;

    private static IReadOnlyList<CatalogEntry> BuildEntries()
    {
        // Copy first: the collection is observable and a content load may still be appending to it.
        Builder.Data.ElementBase[] elements = DataManager.Current.ElementsCollection.ToArray();
        var entries = new List<CatalogEntry>(capacity: 2048);

        // Content can declare one id more than once (the generated spell scrolls do when a spell
        // exists under two ids that end the same way). An id is what a purchase refers to and
        // ElementsCollection.GetElement hands back the first, so list the first and only that.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int duplicates = 0;
        string? duplicateExample = null;
        foreach (Builder.Data.ElementBase element in elements)
        {
            if (element is not Item item || !IsShopItem(item))
                continue;

            if (!seen.Add(item.Id))
            {
                duplicates++;
                duplicateExample ??= item.Id;
                continue;
            }

            entries.Add(new CatalogEntry(ToModel(item), item));
        }

        if (duplicates > 0)
        {
            DebugLogService.Instance.Info(
                $"Shop: listed {entries.Count} items and skipped {duplicates} repeated element ids.",
                $"First repeated id: {duplicateExample}");
        }

        return entries;
    }

    /// <summary>
    /// Whether the shop lists an element: it must be inventory material and not one of the
    /// engine's hidden "Additional …" proxies, which stand in for feats and spells.
    /// </summary>
    public static bool IsShopItem(Item item) =>
        EquipmentService.IsInventoryItemType(item.Type)
        && !item.HideFromInventory
        && !string.IsNullOrWhiteSpace(item.Name)
        && !item.Name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase);

    public static ShopItemModel ToModel(Item item)
    {
        var (category, department) = ShopTaxonomy.Classify(item.Type, item.Category);
        string subtype = ShopTaxonomy.ClassifySubtype(item.Type, item.ItemType, item.Supports, item.ArmorGroups);
        string rarity = ShopTaxonomy.NormalizeRarity(item.Rarity);
        bool isTemplate = InventoryItemFactory.GetTemplateKind(item) is not null;
        bool overridesCost = item is MagicItemElement { OverrideCost: true };
        string source = item.Source ?? string.Empty;

        return new ShopItemModel(
            Id: item.Id,
            Name: item.Name,
            Type: item.Type,
            Source: source,
            Category: category,
            Department: department,
            Subtype: subtype,
            Rarity: rarity,
            UnitPriceCopper: ShopPricing.ToCopper(item.Cost, item.CurrencyAbbreviation),
            DisplayWeight: item.DisplayWeight ?? string.Empty,
            WeightPounds: item.CalculableWeight,
            Summary: BuildSummary(item),
            RequiresAttunement: item.RequiresAttunement,
            IsStackable: item.IsStackable,
            IsTemplate: isTemplate,
            PriceFollowsBase: isTemplate && !overridesCost,
            SearchKey: ShopCatalogFilter.BuildSearchKey(
                item.Name, category, department, subtype, rarity, item.Type, source));
    }

    /// <summary>
    /// Everything the detail pane needs beyond the list row: rules text, stats, and for a magic
    /// weapon or armor template the bases it can be built on, each with the price that choice makes.
    /// Template resolution evaluates the engine's support expressions, so call it inside the
    /// character's <see cref="CharacterContext"/> scope.
    /// </summary>
    public ShopItemDetailModel? GetDetail(
        string itemId,
        Character? character,
        BuildSourceRestrictionSnapshot? restrictions = null)
    {
        if (DataManager.Current.ElementsCollection.GetElement(itemId) is not Item item)
            return null;

        var stats = new List<ShopStatModel>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "—")
                stats.Add(new ShopStatModel(label, value.Trim()));
        }

        Add("Damage", EquipmentService.FormatItemDamage(item));
        Add("Properties", item.DisplayWeaponProperties);
        Add("Range", item.Range);
        Add("Armor class", item.DisplayArmorClass);
        Add("Strength", item.DisplayStrength);
        Add("Stealth", item.DisplayStealth);
        Add("Weight", item.DisplayWeight);
        Add("Attunement", item.RequiresAttunement ? "Required" : null);

        return new ShopItemDetailModel(
            item.Id,
            item.Name,
            EquipmentService.GetDescriptionHtml(item),
            EquipmentService.GetDescription(item),
            stats,
            GetBaseOptions(item, character, restrictions));
    }

    private static IReadOnlyList<ShopBaseOptionModel> GetBaseOptions(
        Item template,
        Character? character,
        BuildSourceRestrictionSnapshot? restrictions)
    {
        if (character is null || InventoryItemFactory.GetTemplateKind(template) is null)
            return [];

        restrictions ??= BuildSourceRestrictionSnapshot.CaptureCurrent();
        bool overridesCost = template is MagicItemElement { OverrideCost: true };
        long templateCopper = ShopPricing.ToCopper(template.Cost, template.CurrencyAbbreviation);

        return InventoryItemFactory.GetCompatibleBaseItems(character.Inventory, template)
            .Where(restrictions.Allows)
            .Select(baseItem => new ShopBaseOptionModel(
                baseItem.Id,
                baseItem.Name,
                ShopPricing.UnitPrice(
                    ShopPricing.ToCopper(baseItem.Cost, baseItem.CurrencyAbbreviation),
                    templateCopper,
                    overridesCost),
                baseItem.Source ?? string.Empty))
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── The character's side of the counter ───────────────────────────────────

    /// <summary>The character's coins as the shop's purse.</summary>
    public static CoinPurse GetPurse(Character character)
    {
        var coins = character.Inventory.Coins;
        return new CoinPurse(coins.Copper, coins.Silver, coins.Electrum, coins.Gold, coins.Platinum);
    }

    /// <summary>
    /// The inventory rows the shop can buy back, shelved the same way the catalog is. A magic item
    /// built on a base item (a +1 sword) is shelved and valued as the magic item.
    /// </summary>
    public static IReadOnlyList<ShopInventoryEntryModel> GetInventory(Character character)
    {
        var rows = character.Inventory.Items
            .Where(owned => owned.IncludeInEquipmentPageInventory && owned.Item is not null)
            .Select(owned => (
                Owned: owned,
                Name: InventoryItemAdder.StripAmountSuffix(
                    owned.DisplayName ?? owned.Name ?? (owned.AdornerItem ?? owned.Item).Name, owned.Amount)))
            .ToList();

        // Two daggers are two rows under one name; say which is in which hand ("Dagger (M)").
        var handLabels = HandLabels.For(rows.Select(row => (row.Owned.Identifier, row.Name, (string?)row.Owned.EquippedLocation)));

        var entries = new List<ShopInventoryEntryModel>();
        foreach (var (owned, plainName) in rows)
        {
            Item primary = owned.AdornerItem ?? owned.Item;
            var (category, department) = ShopTaxonomy.Classify(primary.Type, primary.Category);
            string subtype = ShopTaxonomy.ClassifySubtype(
                owned.Item.Type, owned.Item.ItemType, owned.Item.Supports, owned.Item.ArmorGroups);
            string rarity = ShopTaxonomy.NormalizeRarity(primary.Rarity);
            string name = handLabels.GetValueOrDefault(owned.Identifier, plainName);
            string source = primary.Source ?? string.Empty;

            entries.Add(new ShopInventoryEntryModel(
                Identifier: owned.Identifier,
                ElementId: primary.Id,
                Name: name,
                Source: source,
                Category: category,
                Department: department,
                Subtype: subtype,
                Rarity: rarity,
                Amount: owned.Amount,
                IsStackable: owned.IsStackable,
                IsEquipped: owned.IsEquipped,
                UnitPriceCopper: UnitValue(owned),
                DisplayWeight: owned.DisplayWeight ?? string.Empty,
                WeightPounds: owned.Item.CalculableWeight,
                SearchKey: ShopCatalogFilter.BuildSearchKey(name, category, department, subtype, rarity, source)));
        }

        return entries;
    }

    /// <summary>How many of each catalog item the character holds, keyed by the element they bought.</summary>
    public static IReadOnlyDictionary<string, int> GetOwnedCounts(Character character)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (RefactoredEquipmentItem owned in character.Inventory.Items)
        {
            if (owned.Item is null)
                continue;

            string id = (owned.AdornerItem ?? owned.Item).Id;
            counts[id] = counts.GetValueOrDefault(id) + Math.Max(1, owned.Amount);
        }

        return counts;
    }

    /// <summary>The price of one unit of an owned item, by the same rule that priced it at purchase.</summary>
    public static long UnitValue(RefactoredEquipmentItem owned)
    {
        long baseCopper = ShopPricing.ToCopper(owned.Item.Cost, owned.Item.CurrencyAbbreviation);
        if (owned.AdornerItem is not { } adorner)
            return baseCopper;

        return ShopPricing.UnitPrice(
            baseCopper,
            ShopPricing.ToCopper(adorner.Cost, adorner.CurrencyAbbreviation),
            adorner is MagicItemElement { OverrideCost: true });
    }

    private static string BuildSummary(Item item)
    {
        var parts = new List<string>(3);
        if (string.Equals(item.Type, "Weapon", StringComparison.OrdinalIgnoreCase))
        {
            AddPart(parts, EquipmentService.FormatItemDamage(item));
            AddPart(parts, item.DisplayWeaponProperties);
            if (!string.IsNullOrWhiteSpace(item.Range))
                parts.Add($"Range {item.Range.Trim()}");
        }
        else if (string.Equals(item.Type, "Armor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayArmorClass))
                parts.Add($"AC {item.DisplayArmorClass.Trim()}");
            if (!string.IsNullOrWhiteSpace(item.DisplayStrength))
                parts.Add($"Str {item.DisplayStrength.Trim()}");
            if (!string.IsNullOrWhiteSpace(item.DisplayStealth))
                parts.Add($"Stealth {item.DisplayStealth.Trim().ToLowerInvariant()}");
        }

        return string.Join(" · ", parts);
    }

    private static void AddPart(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != "—")
            parts.Add(value.Trim());
    }
}
