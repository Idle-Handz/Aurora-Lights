using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Aurora.Components.Models;

/// <summary>
/// Narrowing a compendium catalog, and the facet lists a reader chooses from. Separate from the
/// service that assembles the catalog because it depends on nothing but the entries themselves,
/// so the desktop and web apps can describe the same content and narrow it the same way.
/// </summary>
public static class CompendiumFilter
{
    private static readonly string[] PreferredTypeOrder =
    [
        "Spell",
        "Feat",
        "Race",
        "Class",
        "Archetype",
        "Background",
        "Companion",
        "Companion Trait",
        "Companion Action",
        "Companion Reaction",
        "Weapon",
        "Armor",
        "Item",
        "Magic Item",
        "Language",
        "Proficiency",
        "Condition"
    ];

    public static IReadOnlyList<string> GetTypes(IEnumerable<CompendiumEntryModel> entries)
    {
        var types = entries.Select(e => e.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(TypeOrder)
            .ThenBy(t => t)
            .ToList();

        types.Insert(0, "All");
        return types;
    }

    public static IReadOnlyList<string> GetSources(IEnumerable<CompendiumEntryModel> entries)
    {
        var sources = entries.Select(e => e.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(NormalizeSourceFilterKey, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(ChooseSourceDisplayName)
            .OrderBy(s => s)
            .ToList();

        sources.Insert(0, "All");
        return sources;
    }

    public static string NormalizeSourceFilterKey(string? source)
    {
        return NormalizeSearchKey(source);
    }

    public static string NormalizeSearchKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Trim()
            .Normalize(NormalizationForm.FormKC)
            .Replace("\u00E2\u20AC\u2122", "'", StringComparison.Ordinal)
            .Replace("\u2019", "'", StringComparison.Ordinal)
            .Replace("\u2018", "'", StringComparison.Ordinal)
            .Replace("\u02BC", "'", StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static string ChooseSourceDisplayName(IGrouping<string, string> group) =>
        group.GroupBy(source => source, StringComparer.Ordinal)
            .OrderByDescending(sourceGroup => sourceGroup.Count())
            .ThenByDescending(sourceGroup => SourceDisplayPreference(sourceGroup.Key))
            .ThenBy(sourceGroup => sourceGroup.Key, StringComparer.OrdinalIgnoreCase)
            .Select(sourceGroup => sourceGroup.Key)
            .First();

    private static int SourceDisplayPreference(string source)
    {
        if (source.Contains("\u00E2\u20AC\u2122", StringComparison.Ordinal))
            return 0;

        return source.Contains('\u2019') || source.Contains('\u2018') || source.Contains('\u02BC')
            ? 2
            : 1;
    }

    public static IReadOnlyList<string> GetSpellLevels(IEnumerable<CompendiumEntryModel> entries)
    {
        var levels = entries
            .Where(e => string.Equals(e.Type, "Spell", StringComparison.OrdinalIgnoreCase) && e.SpellLevel is not null)
            .Select(e => e.SpellLevel == 0 ? "Cantrip" : e.SpellLevel!.Value.ToString(CultureInfo.InvariantCulture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(LevelOrder)
            .ToList();

        levels.Insert(0, "All");
        return levels;
    }

    public static IReadOnlyList<string> GetSpellSchools(IEnumerable<CompendiumEntryModel> entries)
    {
        var schools = entries
            .Where(e => string.Equals(e.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.SpellSchool)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        schools.Insert(0, "All");
        return schools;
    }

    public static IReadOnlyList<string> GetSpellClasses(IEnumerable<CompendiumEntryModel> entries)
    {
        var classes = entries
            .Where(e => string.Equals(e.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => e.SpellClasses)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        classes.Insert(0, "All");
        return classes;
    }

    public static IReadOnlyList<string> GetItemRarities(IEnumerable<CompendiumEntryModel> entries)
    {
        var rarities = entries
            .Where(e => e.IsItemLike)
            .Select(e => e.ItemRarity)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(RarityOrder)
            .ThenBy(s => s)
            .ToList();

        rarities.Insert(0, "All");
        return rarities;
    }

    public static IReadOnlyList<string> GetCreatureTypes(IEnumerable<CompendiumEntryModel> entries)
    {
        var creatureTypes = entries
            .Where(e => e.IsCompanionLike)
            .Select(e => e.CreatureType)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        creatureTypes.Insert(0, "All");
        return creatureTypes;
    }

    public static IReadOnlyList<string> GetCreatureSizes(IEnumerable<CompendiumEntryModel> entries)
    {
        var sizes = entries
            .Where(e => e.IsCompanionLike)
            .Select(e => e.CreatureSize)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SizeOrder)
            .ThenBy(s => s)
            .ToList();

        sizes.Insert(0, "All");
        return sizes;
    }

    public static IReadOnlyList<string> GetCreatureChallenges(IEnumerable<CompendiumEntryModel> entries)
    {
        var challenges = entries
            .Where(e => e.IsCompanionLike)
            .Select(e => e.ChallengeText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(CrOrder)
            .ThenBy(s => s)
            .ToList();

        challenges.Insert(0, "All");
        return challenges;
    }

    public static IReadOnlyList<CompendiumEntryModel> Filter(
        IEnumerable<CompendiumEntryModel> entries,
        string? query,
        string? type,
        string? source,
        string? spellLevel,
        string? spellSchool,
        string? spellClass,
        string? spellCastingTime,
        string? itemRarity,
        string? itemAttunement,
        string? creatureType,
        string? creatureSize,
        string? creatureChallenge,
        ISet<string>? restrictedSources)
    {
        IEnumerable<CompendiumEntryModel> filtered = entries;

        if (restrictedSources is { Count: > 0 })
        {
            var restrictedSourceKeys = restrictedSources
                .Select(NormalizeSourceFilterKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.Ordinal);

            filtered = filtered.Where(entry => !restrictedSourceKeys.Contains(NormalizeSourceFilterKey(entry.Source)));
        }

        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(entry => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(source) && !string.Equals(source, "All", StringComparison.OrdinalIgnoreCase))
        {
            string sourceKey = NormalizeSourceFilterKey(source);
            filtered = filtered.Where(entry => string.Equals(
                NormalizeSourceFilterKey(entry.Source),
                sourceKey,
                StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(spellLevel) && !string.Equals(spellLevel, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Type, "Spell", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.SpellLevelLabel, spellLevel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(spellSchool) && !string.Equals(spellSchool, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Type, "Spell", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.SpellSchool, spellSchool, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(spellClass) && !string.Equals(spellClass, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Type, "Spell", StringComparison.OrdinalIgnoreCase) &&
                entry.SpellClasses.Contains(spellClass, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(spellCastingTime)
            && !string.Equals(spellCastingTime, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Type, "Spell", StringComparison.OrdinalIgnoreCase) &&
                MagicCastingTimeClassifier.Matches(entry.SpellCastingTime, spellCastingTime));
        }

        if (!string.IsNullOrWhiteSpace(itemRarity) && !string.Equals(itemRarity, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                entry.IsItemLike &&
                string.Equals(entry.ItemRarity, itemRarity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(itemAttunement) && !string.Equals(itemAttunement, "All", StringComparison.OrdinalIgnoreCase))
        {
            bool requiresAttunement = string.Equals(itemAttunement, "Requires Attunement", StringComparison.OrdinalIgnoreCase);
            filtered = filtered.Where(entry =>
                entry.IsItemLike &&
                entry.RequiresAttunement == requiresAttunement);
        }

        if (!string.IsNullOrWhiteSpace(creatureType) && !string.Equals(creatureType, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                entry.IsCompanionLike &&
                string.Equals(entry.CreatureType, creatureType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(creatureSize) && !string.Equals(creatureSize, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                entry.IsCompanionLike &&
                string.Equals(entry.CreatureSize, creatureSize, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(creatureChallenge) && !string.Equals(creatureChallenge, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(entry =>
                entry.IsCompanionLike &&
                string.Equals(entry.ChallengeText, creatureChallenge, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalizedQuery = NormalizeSearchKey(query);
            filtered = filtered.Where(entry => entry.SearchKey.Contains(normalizedQuery, StringComparison.Ordinal));
        }

        return filtered.ToList();
    }

    private static int LevelOrder(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return int.MaxValue;
        if (string.Equals(label, "Cantrip", StringComparison.OrdinalIgnoreCase)) return 0;
        return int.TryParse(label, out int numeric) ? numeric + 1 : int.MaxValue;
    }

    private static int RarityOrder(string? rarity)
    {
        return rarity?.Trim().ToLowerInvariant() switch
        {
            "common" => 0,
            "uncommon" => 1,
            "rare" => 2,
            "very rare" => 3,
            "legendary" => 4,
            "artifact" => 5,
            "unique" => 6,
            _ => int.MaxValue
        };
    }

    private static int SizeOrder(string? size) =>
        size?.Trim().ToLowerInvariant() switch
        {
            "tiny" => 0,
            "small" => 1,
            "medium" => 2,
            "large" => 3,
            "huge" => 4,
            "gargantuan" => 5,
            _ => int.MaxValue
        };

    private static decimal CrOrder(string? challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge))
            return decimal.MaxValue;

        string trimmed = challenge.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal direct))
            return direct;

        if (trimmed.Contains('/'))
        {
            string[] parts = trimmed.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal numerator) &&
                decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        return decimal.MaxValue;
    }

    public static int TypeOrder(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return int.MaxValue;
        int index = Array.FindIndex(PreferredTypeOrder, candidate => candidate.Equals(type, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : PreferredTypeOrder.Length + 1;
    }

    public static bool IsItemLike(string type) =>
        type is "Weapon" or "Armor" or "Item" or "Magic Item" or "Ammunition" or "Tool" or "Mount" or "Vehicle" or "Pack" or "Gear" or "Adventuring Gear";
}
