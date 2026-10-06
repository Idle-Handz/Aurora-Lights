namespace Aurora.Components.Models;

/// <summary>
/// How the shop organises what it sells: items sit on <em>shelves</em> (the content's own item
/// categories, in legacy's order) and shelves are grouped into <em>departments</em>. A category the
/// content defines that is not listed here still gets a shelf of its own, so homebrew and newer
/// sources are never hidden by this table.
/// </summary>
public static class ShopTaxonomy
{
    public const string WeaponsAndArmor = "Weapons & Armor";
    public const string AdventuringGear = "Adventuring Gear";
    public const string ToolsAndInstruments = "Tools & Instruments";
    public const string PotionsAndScrolls = "Potions, Poisons & Scrolls";
    public const string MagicItems = "Magic Items";
    public const string Treasure = "Treasure";
    public const string Other = "Other";

    /// <summary>Departments in display order.</summary>
    public static IReadOnlyList<string> Departments { get; } =
    [
        WeaponsAndArmor, AdventuringGear, ToolsAndInstruments, PotionsAndScrolls, MagicItems, Treasure, Other,
    ];

    // Shelf order within a department follows the legacy equipment picker's category order.
    private static readonly (string Shelf, string Department)[] KnownShelves =
    [
        ("Weapons", WeaponsAndArmor),
        ("Armor", WeaponsAndArmor),
        ("Ammunition", WeaponsAndArmor),
        ("Adventuring Gear", AdventuringGear),
        ("Equipment Packs", AdventuringGear),
        ("Spellcasting Focus", AdventuringGear),
        ("Tools", ToolsAndInstruments),
        ("Musical Instruments", ToolsAndInstruments),
        ("Potions", PotionsAndScrolls),
        ("Poison", PotionsAndScrolls),
        ("Scrolls", PotionsAndScrolls),
        ("Spell Scrolls", PotionsAndScrolls),
        ("Magic Weapons", MagicItems),
        ("Magic Armor", MagicItems),
        ("Wondrous Items", MagicItems),
        ("Rings", MagicItems),
        ("Rods", MagicItems),
        ("Staffs", MagicItems),
        ("Wands", MagicItems),
        ("Artificer Infusions", MagicItems),
        ("Supernatural Gifts", MagicItems),
        ("Treasure", Treasure),
        ("Valuables", Treasure),
    ];

    private static readonly Dictionary<string, (string Shelf, string Department, int Order)> KnownByName =
        KnownShelves
            .Select((entry, index) => (entry.Shelf, entry.Department, Order: index))
            .ToDictionary(entry => entry.Shelf, entry => entry, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] RarityOrder =
        ["Common", "Uncommon", "Rare", "Very Rare", "Legendary", "Artifact", "Unique"];

    /// <summary>
    /// The shelf and department an item belongs on. <paramref name="category"/> is the content's own
    /// category; weapons and armor fall back to their element type when it is blank.
    /// </summary>
    public static (string Shelf, string Department) Classify(string? type, string? category)
    {
        string name = category?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            name = type?.Trim() switch
            {
                "Weapon" => "Weapons",
                "Armor" => "Armor",
                "Ammunition" => "Ammunition",
                "Pack" => "Equipment Packs",
                "Tool" => "Tools",
                _ => string.Empty,
            };
        }

        if (name.Length == 0)
            return ("Miscellaneous", Other);

        if (KnownByName.TryGetValue(name, out var known))
            return (known.Shelf, known.Department);

        // An unfamiliar category keeps its own name; magic items stay with the magic items.
        string department = string.Equals(type, "Magic Item", StringComparison.OrdinalIgnoreCase)
            ? MagicItems
            : Other;
        return (name, department);
    }

    /// <summary>
    /// A finer split of a shelf where the data supports one: weapon category, armor group, gaming
    /// sets. Empty when there is nothing meaningful to split on.
    /// </summary>
    public static string ClassifySubtype(
        string? type,
        string? itemType,
        IEnumerable<string>? supports,
        IEnumerable<string>? armorGroups)
    {
        if (string.Equals(type, "Weapon", StringComparison.OrdinalIgnoreCase))
            return WeaponSubtype(supports);

        if (string.Equals(type, "Armor", StringComparison.OrdinalIgnoreCase))
            return ArmorSubtype(itemType, armorGroups);

        if (string.Equals(itemType, "Gaming Set", StringComparison.OrdinalIgnoreCase))
            return "Gaming Sets";

        return string.Empty;
    }

    private static string WeaponSubtype(IEnumerable<string>? supports)
    {
        if (supports is null)
            return string.Empty;

        bool simple = false, martial = false, melee = false, ranged = false;
        foreach (string support in supports)
        {
            if (string.IsNullOrWhiteSpace(support))
                continue;

            string value = support.Trim();
            // Both the id form (ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_RANGED) and the plain words
            // ("Martial", "Ranged") appear in content from different sources.
            if (value.Contains("SIMPLE", StringComparison.OrdinalIgnoreCase)) simple = true;
            if (value.Contains("MARTIAL", StringComparison.OrdinalIgnoreCase)) martial = true;
            if (value.Contains("MELEE", StringComparison.OrdinalIgnoreCase)) melee = true;
            if (value.Contains("RANGED", StringComparison.OrdinalIgnoreCase)) ranged = true;
        }

        if (simple == martial)
            return string.Empty;

        string proficiency = simple ? "Simple" : "Martial";
        if (melee == ranged)
            return string.Empty;

        return $"{proficiency} {(melee ? "Melee" : "Ranged")}";
    }

    private static string ArmorSubtype(string? itemType, IEnumerable<string>? armorGroups)
    {
        if (string.Equals(itemType, "Shield", StringComparison.OrdinalIgnoreCase))
            return "Shields";

        foreach (string group in armorGroups ?? [])
        {
            switch (group?.Trim().ToLowerInvariant())
            {
                case "light": return "Light Armor";
                case "medium": return "Medium Armor";
                case "heavy": return "Heavy Armor";
                case "shield": return "Shields";
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// A rarity in one canonical spelling ("Very rare" and "Very Rare" both occur in the content),
    /// or an empty string for mundane items.
    /// </summary>
    public static string NormalizeRarity(string? rarity)
    {
        string trimmed = rarity?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return string.Empty;

        foreach (string known in RarityOrder)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return trimmed;
    }

    /// <summary>Sort position of a rarity: mundane first, then common up to artifact.</summary>
    public static int RarityRank(string? rarity)
    {
        string normalized = NormalizeRarity(rarity);
        if (normalized.Length == 0)
            return -1;

        int index = Array.IndexOf(RarityOrder, normalized);
        return index >= 0 ? index : RarityOrder.Length;
    }

    /// <summary>Rarities worth offering as filters, in rank order.</summary>
    public static IReadOnlyList<string> KnownRarities => RarityOrder;

    /// <summary>Position of a department in the shop; unknown departments sort last.</summary>
    public static int DepartmentRank(string department)
    {
        for (int i = 0; i < Departments.Count; i++)
        {
            if (string.Equals(Departments[i], department, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return Departments.Count;
    }

    /// <summary>Position of a shelf within the shop: known shelves in legacy order, the rest after by name.</summary>
    public static int ShelfRank(string shelf) =>
        KnownByName.TryGetValue(shelf, out var known) ? known.Order : KnownShelves.Length;
}
