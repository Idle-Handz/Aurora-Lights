using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Aurora.Importer;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.App.Services;

public sealed class CompendiumService
{
    private static readonly ConcurrentDictionary<(Type Type, string Property), PropertyInfo?> PropertyCache = new();
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Source",
        "Support",
        "Internal",
        "Core",
        "Ability Score Improvement",
        "Level",
        "Multiclass",
        "Skill",
        "Ignore"
    };

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

    private readonly object _catalogLock = new();
    private readonly ContentDatabaseService _contentDb;
    private readonly CharacterService _characterService;
    private IReadOnlyList<CompendiumEntryModel>? _catalogCache;
    private readonly ConcurrentDictionary<string, CompendiumEntryModel> _detailCache = new(StringComparer.Ordinal);

    public CompendiumService(ContentDatabaseService contentDb, CharacterService characterService)
    {
        _contentDb = contentDb;
        _characterService = characterService;
    }

    public async Task<IReadOnlyList<CompendiumEntryModel>> BuildCatalogAsync()
    {
        // Ensure all Aurora content elements are loaded before building the catalog.
        // PreloadAsync is idempotent — fast no-op after the first call.
        await _characterService.PreloadAsync();

        lock (_catalogLock)
        {
            if (_catalogCache is not null)
                return _catalogCache;
        }

        IReadOnlyList<CompendiumEntryModel> built = await Task.Run(BuildCatalogCore);
        lock (_catalogLock)
        {
            _catalogCache ??= built;
            return _catalogCache;
        }
    }

    public async Task<CompendiumEntryModel> EnrichEntryAsync(CompendiumEntryModel entry)
    {
        if (entry.HasComputedDetail)
            return entry;

        if (_detailCache.TryGetValue(entry.Id, out CompendiumEntryModel? cached))
            return cached;

        CompendiumEntryModel enriched = await Task.Run(() => EnrichEntryCore(entry));
        _detailCache[entry.Id] = enriched;
        return enriched;
    }

    public IReadOnlyList<string> GetTypes(IEnumerable<CompendiumEntryModel> entries)
    {
        var types = entries.Select(e => e.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(TypeOrder)
            .ThenBy(t => t)
            .ToList();

        types.Insert(0, "All");
        return types;
    }

    public IReadOnlyList<string> GetSources(IEnumerable<CompendiumEntryModel> entries)
    {
        var sources = entries.Select(e => e.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        sources.Insert(0, "All");
        return sources;
    }

    public IReadOnlyList<string> GetSpellLevels(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetSpellSchools(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetSpellClasses(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetItemRarities(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetCreatureTypes(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetCreatureSizes(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<string> GetCreatureChallenges(IEnumerable<CompendiumEntryModel> entries)
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

    public IReadOnlyList<CompendiumEntryModel> Filter(
        IEnumerable<CompendiumEntryModel> entries,
        string? query,
        string? type,
        string? source,
        string? spellLevel,
        string? spellSchool,
        string? spellClass,
        string? itemRarity,
        string? itemAttunement,
        string? creatureType,
        string? creatureSize,
        string? creatureChallenge,
        ISet<string>? restrictedSources)
    {
        IEnumerable<CompendiumEntryModel> filtered = entries;

        if (restrictedSources is { Count: > 0 })
            filtered = filtered.Where(entry => !restrictedSources.Contains(entry.Source));

        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(entry => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(source) && !string.Equals(source, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(entry => string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase));

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
            string normalizedQuery = query.Trim().ToUpperInvariant();
            filtered = filtered.Where(entry => entry.SearchKey.Contains(normalizedQuery, StringComparison.Ordinal));
        }

        return filtered.ToList();
    }

    private IReadOnlyList<CompendiumEntryModel> BuildCatalogCore()
    {
        // DB is the primary source — structured, fast, richer metadata.
        var merged = new Dictionary<string, CompendiumEntryModel>(StringComparer.Ordinal);
        if (_contentDb.DatabasePath is { } dbPath && File.Exists(dbPath))
        {
            try
            {
                foreach (var entry in BuildCatalogFromDatabase(dbPath))
                    merged[entry.Id] = entry;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.Warn("CompendiumService: SQLite catalog query failed; falling back to loaded elements only.", ex.Message);
            }
        }

        // Loaded elements fill in anything the DB doesn't cover yet (e.g. spells not yet imported).
        foreach (var entry in BuildCatalogFromLoadedElements())
            merged.TryAdd(entry.Id, entry);

        return merged.Values
            .OrderBy(entry => TypeOrder(entry.Type))
            .ThenBy(entry => entry.Name)
            .ThenBy(entry => entry.Source)
            .ToList();
    }

    private CompendiumEntryModel EnrichEntryCore(CompendiumEntryModel entry)
    {
        if (_contentDb.DatabasePath is { } dbPath && File.Exists(dbPath))
        {
            try
            {
                CompendiumEntryModel? fromDb = TryLoadEntryDetailFromDatabase(dbPath, entry.Id, entry);
                if (fromDb is not null)
                    return fromDb;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.Warn("CompendiumService: SQLite detail query failed; falling back to loaded elements.", ex.Message);
            }
        }

        return EnrichEntryFromLoadedElements(entry);
    }

    private static IReadOnlyList<CompendiumEntryModel> BuildCatalogFromDatabase(string dbPath)
    {
        using var conn = OpenReadOnlyConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT
    e.aurora_id,
    e.name,
    et.type_name,
    COALESCE(sb.name, ''),
    COALESCE(summary.body, description.body, ''),
    COALESCE(sp.spell_level, NULL),
    COALESCE(sp.school_name, ''),
    COALESCE(classes.access_summary, ''),
    COALESCE(item_meta.rarity, ''),
    COALESCE(item_meta.requires_attunement, 0),
    COALESCE(item_meta.weight_text, ''),
    COALESCE(comp.creature_type, ''),
    COALESCE(comp.size_text, ''),
    COALESCE(comp.challenge_text, ''),
    COALESCE(sp.casting_time_text, ''),
    COALESCE(sp.range_text, ''),
    COALESCE(sp.duration_text, ''),
    COALESCE(sp.has_verbal, 0),
    COALESCE(sp.has_somatic, 0),
    COALESCE(sp.has_material, 0),
    COALESCE(sp.material_text, ''),
    COALESCE(sp.is_concentration, 0),
    COALESCE(sp.is_ritual, 0)
FROM resolved_elements_cache AS rec
JOIN elements AS e
    ON e.element_id = rec.winning_element_id
JOIN element_types AS et
    ON et.element_type_id = e.element_type_id
LEFT JOIN source_books AS sb
    ON sb.source_book_id = e.source_book_id
LEFT JOIN element_texts AS summary
    ON summary.element_id = e.element_id
   AND summary.text_kind = 'summary'
   AND summary.ordinal = 1
LEFT JOIN element_texts AS description
    ON description.element_id = e.element_id
   AND description.text_kind = 'description'
   AND description.ordinal = 1
LEFT JOIN spells AS sp
    ON sp.element_id = e.element_id
LEFT JOIN (
    SELECT
        sa.spell_element_id,
        GROUP_CONCAT(sa.access_text, ' | ') AS access_summary
    FROM spell_access AS sa
    GROUP BY sa.spell_element_id
) AS classes
    ON classes.spell_element_id = e.element_id
LEFT JOIN (
    SELECT
        i.element_id,
        i.weight_text,
        COALESCE(MAX(CASE WHEN LOWER(se.setter_name) = 'rarity' THEN se.setter_value END), '') AS rarity,
        COALESCE(MAX(CASE
            WHEN LOWER(se.setter_name) = 'attunement'
             AND LOWER(COALESCE(se.setter_value, '')) IN ('true', '1', 'yes')
            THEN 1 ELSE 0 END), 0) AS requires_attunement
    FROM items AS i
    LEFT JOIN setter_scopes AS ss
        ON ss.owner_element_id = i.element_id
       AND ss.owner_kind = 'element'
    LEFT JOIN setter_entries AS se
        ON se.setter_scope_id = ss.setter_scope_id
    GROUP BY i.element_id, i.weight_text
) AS item_meta
    ON item_meta.element_id = e.element_id
LEFT JOIN companions AS comp
    ON comp.element_id = e.element_id
WHERE (e.compendium_display = 1 OR et.type_name = 'Spell')
  AND et.type_name NOT IN ('Source', 'Support', 'Internal', 'Core', 'Ability Score Improvement', 'Level', 'Multiclass', 'Skill', 'Ignore')
ORDER BY e.name COLLATE NOCASE;
""";

        var rows = new List<CompendiumEntryModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            string name = reader.GetString(1);
            string type = reader.GetString(2);
            string source = reader.GetString(3);
            string preview = CreatePreviewText(reader.IsDBNull(4) ? string.Empty : reader.GetString(4));
            int? spellLevel = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            string spellSchool = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            IReadOnlyList<string> spellClasses = SplitPipeList(reader.IsDBNull(7) ? string.Empty : reader.GetString(7));
            string itemRarity = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            bool requiresAttunement = !reader.IsDBNull(9) && reader.GetInt64(9) != 0;
            string displayWeight = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
            string creatureType = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            string creatureSize = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
            string challenge = reader.IsDBNull(13) ? string.Empty : reader.GetString(13);
            string spellCastingTime = reader.IsDBNull(14) ? string.Empty : reader.GetString(14);
            string spellRange = reader.IsDBNull(15) ? string.Empty : reader.GetString(15);
            string spellDuration = reader.IsDBNull(16) ? string.Empty : reader.GetString(16);
            string spellComponents = FormatSpellComponents(
                !reader.IsDBNull(17) && reader.GetInt64(17) != 0,
                !reader.IsDBNull(18) && reader.GetInt64(18) != 0,
                !reader.IsDBNull(19) && reader.GetInt64(19) != 0,
                reader.IsDBNull(20) ? string.Empty : reader.GetString(20));
            bool spellConcentration = !reader.IsDBNull(21) && reader.GetInt64(21) != 0;
            bool spellRitual = !reader.IsDBNull(22) && reader.GetInt64(22) != 0;
            string searchText = string.Join(" ", name, type, source, spellSchool, spellCastingTime, spellRange, spellDuration, spellComponents, itemRarity, displayWeight, creatureType, creatureSize, challenge, string.Join(" ", spellClasses), preview);

            rows.Add(new CompendiumEntryModel(
                id,
                name,
                type,
                source,
                preview,
                string.Empty,
                searchText,
                spellLevel,
                spellSchool,
                spellClasses,
                itemRarity,
                requiresAttunement,
                displayWeight,
                creatureType,
                creatureSize,
                challenge,
                spellCastingTime,
                spellRange,
                spellComponents,
                spellDuration,
                spellConcentration,
                spellRitual,
                false));
        }

        return rows
            .OrderBy(entry => TypeOrder(entry.Type))
            .ThenBy(entry => entry.Name)
            .ThenBy(entry => entry.Source)
            .ToList();
    }

    private CompendiumEntryModel? TryLoadEntryDetailFromDatabase(string dbPath, string auroraId, CompendiumEntryModel fallback)
    {
        using var conn = OpenReadOnlyConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT
    COALESCE(markup.raw_xml, ''),
    COALESCE(description.body, ''),
    COALESCE(sp.casting_time_text, ''),
    COALESCE(sp.range_text, ''),
    COALESCE(sp.duration_text, ''),
    COALESCE(sp.has_verbal, 0),
    COALESCE(sp.has_somatic, 0),
    COALESCE(sp.has_material, 0),
    COALESCE(sp.material_text, ''),
    COALESCE(sp.is_concentration, 0),
    COALESCE(sp.is_ritual, 0)
FROM resolved_elements_cache AS rec
JOIN elements AS e
    ON e.element_id = rec.winning_element_id
LEFT JOIN element_texts AS description
    ON description.element_id = e.element_id
   AND description.text_kind = 'description'
   AND description.ordinal = 1
LEFT JOIN element_text_markup AS markup
    ON markup.element_text_id = description.element_text_id
LEFT JOIN spells AS sp
    ON sp.element_id = e.element_id
WHERE e.aurora_id = $auroraId
LIMIT 1;
""";
        cmd.Parameters.AddWithValue("$auroraId", auroraId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        string descriptionHtml = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        if (string.IsNullOrWhiteSpace(descriptionHtml))
        {
            string body = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            descriptionHtml = string.IsNullOrWhiteSpace(body) ? string.Empty : $"<p>{WebUtility.HtmlEncode(body)}</p>";
        }

        string plain = CreatePlainText(descriptionHtml);
        string summary = plain.Length > 220
            ? plain[..217].TrimEnd() + "..."
            : plain;

        string searchText = string.IsNullOrWhiteSpace(plain)
            ? fallback.SearchText
            : string.Join(" ", fallback.SearchText, plain);

        string spellCastingTime = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        string spellRange = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
        string spellDuration = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        string spellComponents = FormatSpellComponents(
            !reader.IsDBNull(5) && reader.GetInt64(5) != 0,
            !reader.IsDBNull(6) && reader.GetInt64(6) != 0,
            !reader.IsDBNull(7) && reader.GetInt64(7) != 0,
            reader.IsDBNull(8) ? string.Empty : reader.GetString(8));
        bool spellConcentration = !reader.IsDBNull(9) && reader.GetInt64(9) != 0;
        bool spellRitual = !reader.IsDBNull(10) && reader.GetInt64(10) != 0;

        return fallback with
        {
            Summary = string.IsNullOrWhiteSpace(summary) ? fallback.Summary : summary,
            DescriptionHtml = descriptionHtml,
            SpellCastingTime = string.IsNullOrWhiteSpace(spellCastingTime) ? fallback.SpellCastingTime : spellCastingTime,
            SpellRange = string.IsNullOrWhiteSpace(spellRange) ? fallback.SpellRange : spellRange,
            SpellComponents = string.IsNullOrWhiteSpace(spellComponents) ? fallback.SpellComponents : spellComponents,
            SpellDuration = string.IsNullOrWhiteSpace(spellDuration) ? fallback.SpellDuration : spellDuration,
            SpellIsConcentration = spellConcentration || fallback.SpellIsConcentration,
            SpellIsRitual = spellRitual || fallback.SpellIsRitual,
            SearchText = string.Join(" ", searchText, spellCastingTime, spellRange, spellDuration, spellComponents),
            SearchKey = string.Join(" ", searchText, spellCastingTime, spellRange, spellDuration, spellComponents).ToUpperInvariant(),
            HasComputedDetail = true
        };
    }

    private static IReadOnlyList<CompendiumEntryModel> BuildCatalogFromLoadedElements()
    {
        if (!DataManager.Current.IsElementsCollectionPopulated)
            return [];

        return DataManager.Current.ElementsCollection
            .Where(ShouldInclude)
            .Select(ToEntry)
            .OrderBy(entry => TypeOrder(entry.Type))
            .ThenBy(entry => entry.Name)
            .ThenBy(entry => entry.Source)
            .ToList();
    }

    private static CompendiumEntryModel EnrichEntryFromLoadedElements(CompendiumEntryModel entry)
    {
        if (!DataManager.Current.IsElementsCollectionPopulated)
            return entry with { HasComputedDetail = true };

        object? element = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => string.Equals(GetString(e, "Id"), entry.Id, StringComparison.Ordinal));
        if (element is null)
            return entry with { HasComputedDetail = true };

        string descriptionHtml = GetString(element, "Description");
        string plain = CreatePlainText(descriptionHtml);
        string summary = plain.Length > 220
            ? plain[..217].TrimEnd() + "..."
            : plain;

        string searchText = string.IsNullOrWhiteSpace(plain)
            ? entry.SearchText
            : string.Join(" ", entry.SearchText, plain);

        return entry with
        {
            Summary = string.IsNullOrWhiteSpace(summary) ? entry.Summary : summary,
            DescriptionHtml = descriptionHtml,
            SearchText = searchText,
            SearchKey = searchText.ToUpperInvariant(),
            HasComputedDetail = true
        };
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

    private static int TypeOrder(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return int.MaxValue;
        int index = Array.FindIndex(PreferredTypeOrder, candidate => candidate.Equals(type, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : PreferredTypeOrder.Length + 1;
    }

    private static bool ShouldInclude(object element)
    {
        if (element is null) return false;
        string name = GetString(element, "Name");
        string type = GetString(element, "Type");
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (ExcludedTypes.Contains(type)) return false;

        if (GetBool(element, "IncludeInCompendium") is false)
            return false;

        return true;
    }

    private static CompendiumEntryModel ToEntry(object element)
    {
        string id = GetString(element, "Id");
        string name = GetString(element, "Name");
        string type = GetString(element, "Type");
        string source = GetString(element, "Source");
        string descriptionHtml = GetString(element, "Description");
        bool isItemLike = IsItemLike(type);
        int? spellLevel = string.Equals(type, "Spell", StringComparison.OrdinalIgnoreCase)
            ? GetInt(element, "Level")
            : null;
        string spellSchool = string.Equals(type, "Spell", StringComparison.OrdinalIgnoreCase)
            ? GetString(element, "MagicSchool")
            : string.Empty;
        var spellClasses = string.Equals(type, "Spell", StringComparison.OrdinalIgnoreCase)
            ? GetStringList(element, "Supports")
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("ID_", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList()
            : [];
        bool isSpell = string.Equals(type, "Spell", StringComparison.OrdinalIgnoreCase);
        string spellCastingTime = isSpell ? GetString(element, "CastingTime") : string.Empty;
        string spellRange = isSpell ? GetString(element, "Range") : string.Empty;
        string spellDuration = isSpell ? GetString(element, "Duration") : string.Empty;
        string spellComponents = isSpell ? InvokeStringMethod(element, "GetComponentsString") : string.Empty;
        bool spellConcentration = isSpell && GetBool(element, "IsConcentration") == true;
        bool spellRitual = isSpell && GetBool(element, "IsRitual") == true;
        string itemRarity = isItemLike ? GetSetterValue(element, "rarity") : string.Empty;
        bool requiresAttunement = isItemLike && string.Equals(GetSetterValue(element, "attunement"), "true", StringComparison.OrdinalIgnoreCase);
        string displayWeight = isItemLike ? GetString(element, "DisplayWeight") : string.Empty;
        string creatureType = string.Equals(type, "Companion", StringComparison.OrdinalIgnoreCase) ? GetString(element, "CreatureType") : string.Empty;
        string creatureSize = string.Equals(type, "Companion", StringComparison.OrdinalIgnoreCase) ? GetString(element, "Size") : string.Empty;
        string challenge = string.Equals(type, "Companion", StringComparison.OrdinalIgnoreCase) ? GetString(element, "Challenge") : string.Empty;
        string preview = CreatePreviewText(descriptionHtml);

        return new CompendiumEntryModel(
            id,
            name,
            type,
            source,
            preview,
            string.Empty,
            string.Join(" ", name, type, source, spellSchool, spellCastingTime, spellRange, spellDuration, spellComponents, itemRarity, displayWeight, creatureType, creatureSize, challenge, string.Join(" ", spellClasses), preview),
            spellLevel,
            spellSchool,
            spellClasses,
            itemRarity,
            requiresAttunement,
            displayWeight,
            creatureType,
            creatureSize,
            challenge,
            spellCastingTime,
            spellRange,
            spellComponents,
            spellDuration,
            spellConcentration,
            spellRitual,
            false);
    }

    internal static bool IsItemLike(string type) =>
        type is "Weapon" or "Armor" or "Item" or "Magic Item" or "Ammunition" or "Tool" or "Mount" or "Vehicle" or "Pack" or "Gear" or "Adventuring Gear";

    private static string CreatePreviewText(string content)
    {
        string plain = CreatePlainText(content);
        return plain.Length > 220
            ? plain[..217].TrimEnd() + "..."
            : plain;
    }

    private static string CreatePlainText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        return WebUtility.HtmlDecode(HtmlTagRegex.Replace(content, " "))
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();
    }

    private static IReadOnlyList<string> SplitPipeList(string raw) =>
        raw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToList();

    private static string FormatSpellComponents(bool verbal, bool somatic, bool material, string materialText)
    {
        List<string> components = [];
        if (verbal) components.Add("V");
        if (somatic) components.Add("S");
        if (material)
        {
            string materialComponent = "M";
            if (!string.IsNullOrWhiteSpace(materialText))
                materialComponent += $" ({materialText.Trim()})";
            components.Add(materialComponent);
        }

        return string.Join(", ", components);
    }

    private static SqliteConnection OpenReadOnlyConnection(string dbPath)
        => AuroraContentImporter.OpenReadableConnection(dbPath);

    private static string GetString(object target, string propertyName)
    {
        PropertyInfo? property = GetProperty(target, propertyName);
        return property?.GetValue(target)?.ToString() ?? string.Empty;
    }

    private static bool? GetBool(object target, string propertyName)
    {
        PropertyInfo? property = GetProperty(target, propertyName);
        if (property?.GetValue(target) is bool value)
            return value;
        return null;
    }

    private static int? GetInt(object target, string propertyName)
    {
        PropertyInfo? property = GetProperty(target, propertyName);
        object? value = property?.GetValue(target);
        return value switch
        {
            int intValue => intValue,
            null => null,
            _ => int.TryParse(value.ToString(), out int parsed) ? parsed : null
        };
    }

    private static IReadOnlyList<string> GetStringList(object target, string propertyName)
    {
        PropertyInfo? property = GetProperty(target, propertyName);
        if (property?.GetValue(target) is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        return [];
    }

    private static string InvokeStringMethod(object target, string methodName)
    {
        try
        {
            return target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes)
                ?.Invoke(target, null)
                ?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetSetterValue(object target, string setterName)
    {
        PropertyInfo? property = GetProperty(target, "ElementSetters");
        if (property?.GetValue(target) is not System.Collections.IEnumerable setters)
            return string.Empty;

        foreach (object? setter in setters)
        {
            if (setter is null) continue;
            string name = GetString(setter, "Name");
            if (!string.Equals(name, setterName, StringComparison.OrdinalIgnoreCase))
                continue;

            return GetString(setter, "Value");
        }

        return string.Empty;
    }

    private static PropertyInfo? GetProperty(object target, string propertyName)
    {
        var key = (target.GetType(), propertyName);
        return PropertyCache.GetOrAdd(key, static tuple =>
            tuple.Type.GetProperty(tuple.Property, BindingFlags.Instance | BindingFlags.Public));
    }
}

public sealed record CompendiumEntryModel(
    string Id,
    string Name,
    string Type,
    string Source,
    string Summary,
    string DescriptionHtml,
    string SearchText,
    int? SpellLevel,
    string SpellSchool,
    IReadOnlyList<string> SpellClasses,
    string ItemRarity,
    bool RequiresAttunement,
    string DisplayWeight,
    string CreatureType,
    string CreatureSize,
    string ChallengeText,
    string SpellCastingTime,
    string SpellRange,
    string SpellComponents,
    string SpellDuration,
    bool SpellIsConcentration,
    bool SpellIsRitual,
    bool HasComputedDetail)
{
    public string SpellLevelLabel => SpellLevel switch
    {
        null => string.Empty,
        0 => "Cantrip",
        int level => level.ToString(CultureInfo.InvariantCulture)
    };

    public bool IsItemLike => CompendiumService.IsItemLike(Type);
    public bool IsCompanionLike => Type.StartsWith("Companion", StringComparison.OrdinalIgnoreCase);
    public bool HasSpellPropertyDetails =>
        !string.IsNullOrWhiteSpace(SpellCastingTime) ||
        !string.IsNullOrWhiteSpace(SpellRange) ||
        !string.IsNullOrWhiteSpace(SpellComponents) ||
        !string.IsNullOrWhiteSpace(SpellDuration);
    public bool HasSpellDetails => HasSpellPropertyDetails || SpellIsConcentration || SpellIsRitual;
    public string SearchKey { get; init; } = SearchText.ToUpperInvariant();
}
