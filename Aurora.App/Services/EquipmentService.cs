using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.App.Services;

public enum GearSlot { Armor, MainHand, OffHand }

/// <summary>
/// Service for adding, removing, and equipping inventory items on the active character.
/// </summary>
public static class EquipmentService
{
    private static readonly HashSet<string> ItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Item", "Weapon", "Armor", "Magic Item", "Ammunition",
        "Tool", "Mount", "Vehicle", "Pack", "Gear", "Adventuring Gear"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<ExtractionRecipeEntry>> SupplementalExtractionRecipes =
        new Dictionary<string, IReadOnlyList<ExtractionRecipeEntry>>(StringComparer.OrdinalIgnoreCase)
        {
            // The PHB describes these contents in prose but Aurora XML does not include an <extract>.
            // Reuse the ink pen element as a persisted inventory backing item for quills.
            ["ID_WOTC_PHB_ITEM_TOOL_CALLIGRAPHERS_SUPPLIES"] =
            [
                new("ID_WOTC_PHB_ITEM_INK_1OUNCEBOTTLE", 1),
                new("ID_WOTC_PHB_ITEM_PARCHMENT_ONESHEET", 12),
                new("ID_WOTC_PHB_ITEM_INKPEN", 3, "Quill"),
            ],
        };

    // ── General inventory ───────────────────────────────────────────────────────

    /// <summary>
    /// Searches all loaded elements for those that can be added to inventory.
    /// Returns at most 200 results ordered by name.
    /// </summary>
    public static IReadOnlyList<ItemSearchResult> SearchItems(string query)
    {
        IEnumerable<Builder.Data.ElementBase> source =
            DataManager.Current.ElementsCollection.Where(e =>
                ItemTypes.Contains(e.Type)
                // Exclude Aurora's internal ability-enabling pseudo-items (e.g. "Additional Arcane
                // Dilettante Spell, Fire Shield"). These are named "Additional …" and are designed
                // to stay hidden from the character sheet — they are not real inventory items.
                && !e.Name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
            source = source.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        return source
            .OrderBy(e => e.Name)
            .Take(200)
            .Select(e => new ItemSearchResult(e.Id, e.Name, e.Type, GetDescription(e), e.Source ?? ""))
            .ToList();
    }

    /// <summary>
    /// Searches items filtered to a category string from a starting-equipment XML file
    /// (e.g. "Martial Weapon", "Simple Weapon", "Arcane Focus").
    /// Falls back to a supports-substring match for unrecognised categories.
    /// </summary>
    public static IReadOnlyList<ItemSearchResult> SearchItemsByCategory(string category, string query)
    {
        // Supports is a string collection; Contains checks for exact membership.
        // Weapon categories use human-readable entries ("Martial", "Simple", "Melee", "Ranged")
        // as well as the ID-based internal entries ("ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_MELEE", etc.).
        // Checking both forms handles content from different sources.
        IEnumerable<Builder.Data.ElementBase> source = category.Trim().ToLowerInvariant() switch
        {
            "martial weapon" => DataManager.Current.ElementsCollection.Where(e =>
                e.Type == "Weapon" &&
                (e.Supports.Contains("ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_MELEE",  StringComparer.OrdinalIgnoreCase) ||
                 e.Supports.Contains("ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_RANGED", StringComparer.OrdinalIgnoreCase) ||
                 e.Supports.Contains("Martial", StringComparer.OrdinalIgnoreCase))),

            "simple weapon" => DataManager.Current.ElementsCollection.Where(e =>
                e.Type == "Weapon" &&
                (e.Supports.Contains("ID_INTERNAL_WEAPON_CATEGORY_SIMPLE_MELEE",   StringComparer.OrdinalIgnoreCase) ||
                 e.Supports.Contains("ID_INTERNAL_WEAPON_CATEGORY_SIMPLE_RANGED",  StringComparer.OrdinalIgnoreCase) ||
                 e.Supports.Contains("Simple", StringComparer.OrdinalIgnoreCase))),

            "melee weapon" => DataManager.Current.ElementsCollection.Where(e =>
                e.Type == "Weapon" && e.Supports.Contains("Melee", StringComparer.OrdinalIgnoreCase)),

            "ranged weapon" => DataManager.Current.ElementsCollection.Where(e =>
                e.Type == "Weapon" && e.Supports.Contains("Ranged", StringComparer.OrdinalIgnoreCase)),

            // Arcane focus items have no <supports> — matched by known element IDs.
            "arcane focus" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_PHB_ITEM_CRYSTAL", "ID_WOTC_PHB_ITEM_ORB", "ID_WOTC_PHB_ITEM_ROD",
                    "ID_WOTC_PHB_ITEM_STAFF", "ID_WOTC_PHB_ITEM_WAND",
                    "ID_WOTC_PHB24_ITEM_ARCANE_FOCUS_CRYSTAL", "ID_WOTC_PHB24_ITEM_ARCANE_FOCUS_ORB",
                    "ID_WOTC_PHB24_ITEM_ARCANE_FOCUS_ROD", "ID_WOTC_PHB24_ITEM_ARCANE_FOCUS_WAND",
                }.Contains(e.Id) || HasSetterValue(e, "container", "Arcane Focus")),

            // Druidic focus items have no <supports> — matched by known element IDs.
            "druidic focus" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_PHB_ITEM_SPRIGOFMISTLETOE", "ID_WOTC_PHB_ITEM_TOTEM",
                    "ID_WOTC_PHB_ITEM_WOODENSTAFF",      "ID_WOTC_PHB_ITEM_YEWWAND",
                    "ID_WOTC_PHB24_ITEM_DRUIDIC_FOCUS_SPRIG_OF_MISTLETOE",
                    "ID_WOTC_PHB24_ITEM_DRUIDIC_FOCUS_YEW_WAND",
                }.Contains(e.Id) || HasSetterValue(e, "container", "Druidic Focus")),

            "holy symbol" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_PHB_ITEM_AMULET", "ID_WOTC_PHB_ITEM_EMBLEM", "ID_WOTC_PHB_ITEM_RELIQUARY",
                    "ID_WOTC_PHB24_ITEM_HOLY_SYMBOL_AMULET", "ID_WOTC_PHB24_ITEM_HOLY_SYMBOL_EMBLEM",
                    "ID_WOTC_PHB24_ITEM_HOLY_SYMBOL_RELIQUARY",
                }.Contains(e.Id) || HasSetterValue(e, "container", "Holy Symbol")),

            "spellcasting focus" => DataManager.Current.ElementsCollection.Where(e =>
                HasSetterValue(e, "category", "Spellcasting Focus")),

            // Musical instruments have no <supports> — matched by known element IDs.
            "musical instrument" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_SRD_INSTRUMENT_BAGPIPES", "ID_WOTC_SRD_INSTRUMENT_DRUM",
                    "ID_WOTC_SRD_INSTRUMENT_DULCIMER", "ID_WOTC_SRD_INSTRUMENT_FLUTE",
                    "ID_WOTC_SRD_INSTRUMENT_HORN",     "ID_WOTC_SRD_INSTRUMENT_LUTE",
                    "ID_WOTC_SRD_INSTRUMENT_LYRE",     "ID_WOTC_SRD_INSTRUMENT_PANFLUTE",
                    "ID_WOTC_SRD_INSTRUMENT_SHAWM",    "ID_WOTC_SRD_INSTRUMENT_VIOL",
                    "ID_WOTC_PHB24_INSTRUMENT_BAGPIPES", "ID_WOTC_PHB24_INSTRUMENT_DRUM",
                    "ID_WOTC_PHB24_INSTRUMENT_DULCIMER", "ID_WOTC_PHB24_INSTRUMENT_FLUTE",
                    "ID_WOTC_PHB24_INSTRUMENT_HORN",     "ID_WOTC_PHB24_INSTRUMENT_LUTE",
                    "ID_WOTC_PHB24_INSTRUMENT_LYRE",     "ID_WOTC_PHB24_INSTRUMENT_PANFLUTE",
                    "ID_WOTC_PHB24_INSTRUMENT_SHAWM",    "ID_WOTC_PHB24_INSTRUMENT_VIOL",
                }.Contains(e.Id) || HasSetterValue(e, "category", "Musical Instruments")),

            // Artisan's tools have no <supports> — matched by known element IDs.
            "artisan's tools" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_PHB_ITEM_TOOL_ALCHEMISTS_SUPPLIES",
                    "ID_WOTC_PHB_ITEM_TOOL_BREWERS_SUPPLIES",
                    "ID_WOTC_PHB_ITEM_TOOL_CALLIGRAPHERS_SUPPLIES",
                    "ID_WOTC_PHB_ITEM_TOOL_CARPENTERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_CARTOGRAPHERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_COBBLERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_COOKS_UTENSILS",
                    "ID_WOTC_PHB_ITEM_TOOL_GLASSBLOWERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_JEWELERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_LEATHERWORKERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_MASONS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_PAINTERS_SUPPLIES",
                    "ID_WOTC_PHB_ITEM_TOOL_POTTERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_SMITHS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_TINKERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_WEAVERS_TOOLS",
                    "ID_WOTC_PHB_ITEM_TOOL_WOODCARVERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_ALCHEMISTS_SUPPLIES",
                    "ID_WOTC_PHB24_ITEM_TOOL_BREWERS_SUPPLIES",
                    "ID_WOTC_PHB24_ITEM_TOOL_CALLIGRAPHERS_SUPPLIES",
                    "ID_WOTC_PHB24_ITEM_TOOL_CARPENTERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_COBBLERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_COOKS_UTENSILS",
                    "ID_WOTC_PHB24_ITEM_TOOL_GLASSBLOWERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_JEWELERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_LEATHERWORKERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_MASONS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_PAINTERS_SUPPLIES",
                    "ID_WOTC_PHB24_ITEM_TOOL_POTTERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_SMITHS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_TINKERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_WEAVERS_TOOLS",
                    "ID_WOTC_PHB24_ITEM_TOOL_WOODCARVERS_TOOLS",
                }.Contains(e.Id)),

            "gaming set" => DataManager.Current.ElementsCollection.Where(e =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ID_WOTC_PHB24_ITEM_TOOL_DICE_SET",
                    "ID_WOTC_PHB24_ITEM_TOOL_DRAGONCHESS_SET",
                    "ID_WOTC_PHB24_ITEM_TOOL_PLAYING_CARDS_SET",
                    "ID_WOTC_PHB24_ITEM_TOOL_THREE_DRAGON_ANTE_SET",
                }.Contains(e.Id)),

            _ => DataManager.Current.ElementsCollection.Where(e =>
                ItemTypes.Contains(e.Type) &&
                e.Supports.Contains(category, StringComparer.OrdinalIgnoreCase)),
        };

        source = source.Where(e => !e.Name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
            source = source.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        return source
            .OrderBy(e => e.Name)
            .Take(200)
            .Select(e => new ItemSearchResult(e.Id, e.Name, e.Type, GetDescription(e), e.Source ?? ""))
            .ToList();
    }

    private static bool HasSetterValue(Builder.Data.ElementBase element, string setterName, string expectedValue)
    {
        if (!element.ElementSetters.ContainsSetter(setterName))
            return false;

        var value = element.ElementSetters.GetSetter(setterName)?.Value;
        return string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searches elements compatible with the given gear slot.
    /// </summary>
    public static IReadOnlyList<ItemSearchResult> SearchItemsForSlot(GearSlot slot, string query)
    {
        IEnumerable<Builder.Data.ElementBase> source =
            DataManager.Current.ElementsCollection.Where(e => IsElementCompatibleWithSlot(e, slot));

        if (!string.IsNullOrWhiteSpace(query))
            source = source.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        return source
            .OrderBy(e => e.Name)
            .Take(200)
            .Select(e => new ItemSearchResult(e.Id, e.Name, e.Type, GetDescription(e), e.Source ?? ""))
            .ToList();
    }

    /// <summary>
    /// Returns inventory items compatible with the given gear slot, ordered by name.
    /// </summary>
    public static IReadOnlyList<InventoryItemOption> GetInventoryItemsForSlot(Character character, GearSlot slot) =>
        character.Inventory.Items
            .Where(i => IsItemCompatibleWithSlot(i, slot))
            .OrderBy(i => i.DisplayName ?? i.Name ?? "")
            .Select(i => new InventoryItemOption(i.Identifier, i.DisplayName ?? i.Name ?? ""))
            .ToList();

    // ── Add / remove ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an item to inventory by element ID. Uses reflection because
    /// Builder.Data.Elements.Item cannot be named from Aurora.App.
    /// </summary>
    public static bool AddItem(Character character, string elementId, int amount = 1)
    {
        var element = DataManager.Current.ElementsCollection.GetElement(elementId);
        if (element == null) return false;

        try
        {
            var item = CreateInventoryItem(element, amount);
            if (item == null) return false;

            character.Inventory.Items.Add(item);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Returns true when an inventory item can be expanded into component items.</summary>
    public static bool CanExtractPack(Character character, string identifier) =>
        FindInventoryItem(character, identifier) is { } item && IsExtractable(item);

    /// <summary>Returns the component items that would be added by extracting a pack.</summary>
    public static IReadOnlyList<EquipmentPackComponent> GetPackComponents(Character character, string identifier)
    {
        var item = FindInventoryItem(character, identifier);
        if (item == null || !IsExtractable(item)) return [];

        return GetExtractionRecipe(item)
            .Select(entry =>
            {
                var element = DataManager.Current.ElementsCollection.GetElement(entry.ElementId);
                var name = entry.AlternativeName ?? element?.Name ?? entry.ElementId;
                return new EquipmentPackComponent(entry.ElementId, entry.Amount, name);
            })
            .ToList();
    }

    /// <summary>
    /// Extracts an equipment pack into its component items and consumes one pack.
    /// Uses the parsed legacy XML model, with supplemental recipes for prose-defined contents.
    /// </summary>
    public static EquipmentPackExtractionResult ExtractPack(Character character, string identifier)
    {
        var pack = FindInventoryItem(character, identifier);
        if (pack == null || !IsExtractable(pack))
            return new EquipmentPackExtractionResult(false, "", [], []);

        var packName = pack.DisplayName ?? pack.Name ?? "pack";
        var added = new List<EquipmentPackComponent>();
        var missing = new List<string>();
        var pending = new List<PendingExtractedItem>();

        foreach (var entry in GetExtractionRecipe(pack))
        {
            var element = DataManager.Current.ElementsCollection.GetElement(entry.ElementId);
            if (element == null)
            {
                missing.Add(entry.ElementId);
                continue;
            }

            var component = new EquipmentPackComponent(
                entry.ElementId,
                entry.Amount,
                entry.AlternativeName ?? element.Name);
            RefactoredEquipmentItem? existingStack = null;
            RefactoredEquipmentItem? newItem = null;

            if (IsStackableElement(element))
            {
                existingStack = character.Inventory.Items.FirstOrDefault(item =>
                    string.Equals(item.Item?.Id, element.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        NormalizeAlternativeName(item.AlternativeName),
                        NormalizeAlternativeName(entry.AlternativeName),
                        StringComparison.OrdinalIgnoreCase));
            }

            if (existingStack == null)
            {
                newItem = CreateInventoryItem(element, entry.Amount, entry.AlternativeName);
            }

            if (existingStack == null && newItem == null)
            {
                missing.Add(entry.ElementId);
                continue;
            }

            pending.Add(new PendingExtractedItem(component, existingStack, newItem));
        }

        if (missing.Count > 0)
            return new EquipmentPackExtractionResult(false, packName, [], missing);

        foreach (var item in pending)
        {
            if (item.ExistingStack != null)
                item.ExistingStack.Amount += item.Component.Amount;
            else if (item.NewItem != null)
                character.Inventory.Items.Add(item.NewItem);

            added.Add(item.Component);
        }

        ConsumeOneInventoryItem(character, pack);
        character.Inventory.CalculateWeight();
        character.Inventory.CalculateAttunedItemCount();

        return new EquipmentPackExtractionResult(true, packName, added, missing);
    }

    /// <summary>
    /// Removes the item with the given identifier from inventory (deactivates first).
    /// </summary>
    public static bool RemoveItem(Character character, string identifier)
    {
        var item = character.Inventory.Items.FirstOrDefault(i => i.Identifier == identifier);
        if (item == null) return false;

        // If this item is in a gear slot, unequip it cleanly via the inventory methods.
        if (item.IsEquipped)
        {
            var loc = item.EquippedLocation ?? "";
            if (loc == "Armor")
                character.Inventory.UnequipArmor();
            else if (loc is "Primary Hand" or "Two-Handed" or "Two-Handed (Versatile)")
                character.Inventory.UnequipPrimary();
            else if (loc == "Secondary Hand")
                character.Inventory.UnequipSecondary();
            else
                item.Deactivate();
        }
        else
        {
            item.Deactivate();
        }

        character.Inventory.Items.Remove(item);
        character.Inventory.CalculateAttunedItemCount();
        return true;
    }

    /// <summary>Updates the stack amount for a specific inventory item.</summary>
    public static void SetAmount(Character character, string identifier, int amount)
    {
        var item = character.Inventory.Items.FirstOrDefault(i => i.Identifier == identifier);
        if (item != null)
            item.Amount = Math.Max(1, amount);
    }

    private static RefactoredEquipmentItem? FindInventoryItem(Character character, string identifier) =>
        character.Inventory.Items.FirstOrDefault(i => i.Identifier == identifier);

    private static bool IsExtractable(RefactoredEquipmentItem item) =>
        GetExtractionRecipe(item).Count > 0;

    private static RefactoredEquipmentItem? CreateInventoryItem(
        Builder.Data.ElementBase element,
        int amount,
        string? alternativeName = null)
    {
        var elementType = element.GetType();

        // Find the constructor whose first parameter accepts this element type,
        // with all remaining parameters optional (has default values).
        var ctor = typeof(RefactoredEquipmentItem)
            .GetConstructors()
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length >= 1
                    && ps[0].ParameterType.IsAssignableFrom(elementType)
                    && ps.Skip(1).All(p => p.HasDefaultValue);
            });

        if (ctor == null) return null;

        // Build the argument list: first arg is the element, rest use their defaults.
        var ps = ctor.GetParameters();
        var args = ps.Select((p, i) => i == 0 ? (object?)element : p.DefaultValue).ToArray();

        var item = (RefactoredEquipmentItem)ctor.Invoke(args);
        item.Amount = Math.Max(1, amount);
        if (!string.IsNullOrWhiteSpace(alternativeName))
            item.AlternativeName = alternativeName;
        return item;
    }

    private static IReadOnlyList<ExtractionRecipeEntry> GetExtractionRecipe(RefactoredEquipmentItem item)
    {
        if (item.Item?.IsExtractable == true && item.Item.Extractables.Count > 0)
        {
            return item.Item.Extractables
                .Select(entry => new ExtractionRecipeEntry(entry.Key, entry.Value))
                .ToList();
        }

        return item.Item != null &&
               SupplementalExtractionRecipes.TryGetValue(item.Item.Id, out var recipe)
            ? recipe
            : [];
    }

    private static string NormalizeAlternativeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "" : name.Trim();

    private static bool IsStackableElement(Builder.Data.ElementBase element)
    {
        try
        {
            dynamic item = element;
            return item.IsStackable == true;
        }
        catch
        {
            return false;
        }
    }

    private static void ConsumeOneInventoryItem(Character character, RefactoredEquipmentItem item)
    {
        if (item.Amount > 1)
        {
            item.Amount--;
            return;
        }

        RemoveItem(character, item.Identifier);
    }

    private sealed record PendingExtractedItem(
        EquipmentPackComponent Component,
        RefactoredEquipmentItem? ExistingStack,
        RefactoredEquipmentItem? NewItem);

    private sealed record ExtractionRecipeEntry(string ElementId, int RawAmount, string? AlternativeName = null)
    {
        public int Amount => Math.Max(1, RawAmount);
    }

    // ── Custom features (Additional-X proxies + Supernatural Gifts) ─────────────────

    /// <summary>
    /// Categories surfaced by the "Add Custom Feature" picker: Aurora's engine-generated
    /// "Additional …" proxies (one hidden Item per addable feat / spell / language /
    /// proficiency / feature, etc.) plus Supernatural Gifts. Ordinary equipment is excluded —
    /// that belongs in the inventory picker.
    /// </summary>
    public static IReadOnlyList<string> GetCustomFeatureCategories()
    {
        var cats = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in DataManager.Current.ElementsCollection)
        {
            if (!ItemTypes.Contains(e.Type)) continue;
            var cat = GetCustomFeatureCategory(e);
            if (cat != null) cats.Add(cat);
        }
        return cats.ToList();
    }

    /// <summary>Returns the custom-feature category for an element, or null if it isn't one.</summary>
    private static string? GetCustomFeatureCategory(Builder.Data.ElementBase e)
    {
        var name = e.Name ?? "";
        if (name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase))
        {
            // "Additional Feat, Alert" -> category "Additional Feat".
            int comma = name.IndexOf(',');
            return (comma > 0 ? name[..comma] : name).Trim();
        }
        if (HasSetterValue(e, "category", "Supernatural Gifts"))
            return "Supernatural Gifts";
        return null;
    }

    /// <summary>
    /// Searches custom-feature entries within a category from <see cref="GetCustomFeatureCategories"/>.
    /// Result names are stripped of the "Additional &lt;Type&gt;, " prefix for display, and the
    /// description is taken from the underlying granted element (the proxy's own description is just
    /// engine boilerplate about gaining a benefit).
    /// </summary>
    public static IReadOnlyList<ItemSearchResult> SearchCustomFeatures(string category, string query)
    {
        IEnumerable<Builder.Data.ElementBase> source = DataManager.Current.ElementsCollection.Where(e =>
            ItemTypes.Contains(e.Type) &&
            string.Equals(GetCustomFeatureCategory(e), category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
            source = source.Where(e => (e.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase));

        return source
            .OrderBy(e => e.Name)
            .Take(500)
            .Select(e =>
            {
                // Resolve the real feat/spell/feature the proxy grants so the detail pane shows its
                // actual rules text instead of the proxy's "adding this grants a benefit" boilerplate.
                var underlying = ResolveCustomFeatureTarget(e);
                return new ItemSearchResult(
                    e.Id,
                    CleanCustomFeatureName(e.Name ?? "", category),
                    e.Type,
                    GetDescription(underlying),
                    e.Source ?? "");
            })
            .ToList();
    }

    /// <summary>
    /// Resolves the real element an "Additional …" proxy grants. The engine's proxy generator stores
    /// the underlying element id in its <see cref="GrantRule"/> (in the now-obsolete Name field, which
    /// still holds the value). Non-proxy entries (e.g. Supernatural Gifts) resolve to themselves.
    /// </summary>
    public static Builder.Data.ElementBase ResolveCustomFeatureTarget(Builder.Data.ElementBase proxy)
    {
        if (proxy.Name?.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase) != true)
            return proxy;
#pragma warning disable CS0618 // Type or member is obsolete
        var grantedId = proxy.Rules?
            .OfType<GrantRule>()
            .Select(g => g.Attributes?.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
#pragma warning restore CS0618
        if (string.IsNullOrWhiteSpace(grantedId)) return proxy;
        return DataManager.Current.ElementsCollection.GetElement(grantedId) ?? proxy;
    }

    public static bool IsRepeatableCustomFeature(Builder.Data.ElementBase element)
    {
        if (element.AllowMultipleElements)
            return true;

        if (!element.ElementSetters.ContainsSetter("stackable"))
            return false;

        string? value = element.ElementSetters.GetSetter("stackable")?.Value;
        return string.IsNullOrWhiteSpace(value)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanCustomFeatureName(string name, string category)
    {
        var prefix = category + ", ";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
    }

    // ── Gear slots ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Equips an existing inventory item to the specified gear slot.
    /// Unequips whatever is currently in the slot first.
    /// </summary>
    public static bool EquipToSlot(Character character, GearSlot slot, string identifier)
    {
        var item = character.Inventory.Items.FirstOrDefault(i => i.Identifier == identifier);
        if (item == null) return false;

        switch (slot)
        {
            case GearSlot.Armor:
                if (character.Inventory.EquippedArmor != null)
                    character.Inventory.UnequipArmor();
                character.Inventory.EquipArmor(item);
                break;

            case GearSlot.MainHand:
                if (character.Inventory.EquippedPrimary != null)
                    character.Inventory.UnequipPrimary();
                character.Inventory.EquipPrimary(item, item.IsTwoHandTarget());
                break;

            case GearSlot.OffHand:
                if (character.Inventory.EquippedSecondary != null)
                    character.Inventory.UnequipSecondary();
                character.Inventory.EquipSecondary(item);
                break;
        }

        character.Inventory.CalculateAttunedItemCount();
        return true;
    }

    public static bool SetVersatileWield(Character character, string identifier, bool twoHanded)
    {
        var item = character.Inventory.Items.FirstOrDefault(i => i.Identifier == identifier);
        if (item == null || !item.Item.HasVersatile || !item.IsEquipped)
            return false;

        if (twoHanded)
        {
            if (!ReferenceEquals(character.Inventory.EquippedPrimary, item))
            {
                if (character.Inventory.EquippedPrimary != null)
                    character.Inventory.UnequipPrimary();
            }

            if (character.Inventory.EquippedSecondary != null &&
                !ReferenceEquals(character.Inventory.EquippedSecondary, item))
            {
                character.Inventory.UnequipSecondary();
            }

            character.Inventory.EquipPrimary(item, twohanded: true);
        }
        else
        {
            if (!character.Inventory.IsEquippedVersatile() ||
                !ReferenceEquals(character.Inventory.EquippedPrimary, item))
                return false;

            character.Inventory.UnequipPrimary();
            character.Inventory.EquipPrimary(item, twohanded: false);
        }

        character.Inventory.CalculateAttunedItemCount();
        return true;
    }

    /// <summary>
    /// Adds a new item by element ID, then equips it to the given slot.
    /// </summary>
    public static bool AddAndEquipToSlot(Character character, GearSlot slot, string elementId)
    {
        if (!AddItem(character, elementId)) return false;
        var item = character.Inventory.Items.Last();
        return EquipToSlot(character, slot, item.Identifier);
    }

    /// <summary>Unequips the item in the given gear slot (item stays in inventory).</summary>
    public static void UnequipSlot(Character character, GearSlot slot)
    {
        switch (slot)
        {
            case GearSlot.Armor:    character.Inventory.UnequipArmor();     break;
            case GearSlot.MainHand: character.Inventory.UnequipPrimary();   break;
            case GearSlot.OffHand:  character.Inventory.UnequipSecondary(); break;
        }
        character.Inventory.CalculateAttunedItemCount();
    }

    // ── Slot compatibility ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the natural gear slot for an inventory item, or null if the item
    /// is equippable but doesn't belong to a named slot (rings, cloaks, etc.).
    /// Priority: Armor → MainHand → OffHand.
    /// </summary>
    public static GearSlot? GetNaturalSlot(RefactoredEquipmentItem item)
    {
        if (IsItemCompatibleWithSlot(item, GearSlot.Armor))    return GearSlot.Armor;
        if (IsItemCompatibleWithSlot(item, GearSlot.MainHand)) return GearSlot.MainHand;
        if (IsItemCompatibleWithSlot(item, GearSlot.OffHand))  return GearSlot.OffHand;
        return null;
    }

    private static bool IsItemCompatibleWithSlot(RefactoredEquipmentItem item, GearSlot slot) => slot switch
    {
        GearSlot.Armor    => item.IsArmorTarget(),
        GearSlot.MainHand => item.IsOneHandTarget() || item.IsTwoHandTarget() || item.IsPrimaryTarget(),
        GearSlot.OffHand  => item.IsSecondaryTarget() || item.IsOneHandTarget(),
        _                 => false,
    };

    private static bool IsElementCompatibleWithSlot(Builder.Data.ElementBase e, GearSlot slot)
    {
        try
        {
            dynamic d = e;
            string slotVal  = ((string?)d.Slot  ?? "").ToLower();
            var slotsRaw    = (IEnumerable<string>?)d.Slots ?? [];
            var slots       = slotsRaw.Select(s => s.ToLower()).ToHashSet();

            return slot switch
            {
                GearSlot.Armor    => e.Type == "Armor"
                                     && (slots.Contains("armor") || slots.Contains("body")
                                         || slotVal == "armor"  || slotVal == "body"),

                GearSlot.MainHand => e.Type == "Weapon"
                                     && (slots.Contains("onehand") || slots.Contains("twohand")
                                         || slots.Contains("primary")
                                         || slotVal is "onehand" or "twohand"),

                GearSlot.OffHand  => (e.Type == "Weapon"
                                         && (slots.Contains("onehand") || slots.Contains("secondary")))
                                     || (e.Type == "Armor"
                                         && (slots.Contains("secondary")
                                             || slotVal.Contains("secondary"))),
                _ => false,
            };
        }
        catch { return false; }
    }

    // ── Description helper ────────────────────────────────────────────────────────

    public static string GetDescription(Builder.Data.ElementBase e)
    {
        try
        {
            if (e.SheetDescription?.Count > 0)
            {
                var sd = e.SheetDescription[0].Description?.Trim();
                if (!string.IsNullOrEmpty(sd)) return sd;
            }
        }
        catch { }
        try
        {
            if (!string.IsNullOrWhiteSpace(e.Description))
                return Builder.Presentation.Utilities.ElementDescriptionGenerator
                    .GeneratePlainDescription(e.Description).Trim();
        }
        catch { }
        return "";
    }
}

public sealed record ItemSearchResult(string Id, string Name, string Type, string Description, string Source);
public sealed record ItemPickerResult(string ElementId, int Amount);
public sealed record InventoryItemOption(string Identifier, string Name);
public sealed record GearPickerResult(string? Identifier, string? ElementId, bool IsNew);
public sealed record EquipmentPackComponent(string ElementId, int Amount, string Name);
public sealed record EquipmentPackExtractionResult(
    bool Success,
    string PackName,
    IReadOnlyList<EquipmentPackComponent> Added,
    IReadOnlyList<string> MissingElementIds);
