using System.Text.Json;

namespace Aurora.PdfImport;

/// <summary>
/// Parses a D&amp;D Beyond character export JSON file into a <see cref="ParsedCharacterSheet"/>.
/// Supports both the raw character JSON and the API response envelope
/// (<c>{ "data": { "character": { ... } } }</c> or <c>{ "character": { ... } }</c>).
/// </summary>
public static class DndBeyondJsonImporter
{
    // ── Public entry points ───────────────────────────────────────────────────

    public static ParsedCharacterSheet Parse(string jsonPath)
    {
        using var stream = File.OpenRead(jsonPath);
        using var doc    = JsonDocument.Parse(stream);
        return ParseElement(doc.RootElement);
    }

    public static ParsedCharacterSheet ParseJson(string jsonText)
    {
        using var doc = JsonDocument.Parse(jsonText);
        return ParseElement(doc.RootElement);
    }

    // ── Core parser ───────────────────────────────────────────────────────────

    private static ParsedCharacterSheet ParseElement(JsonElement root)
    {
        // The JSON may be the character object directly, or wrapped in "data.character"/"character".
        JsonElement ch = root;
        if (root.TryGetProperty("data", out var data))
            ch = data.TryGetProperty("character", out var inner) ? inner : data;
        else if (root.TryGetProperty("character", out var c))
            ch = c;

        var sheet = new ParsedCharacterSheet();

        sheet.CharacterName = Str(ch, "name");

        // ── Race ──────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("race", out var race))
        {
            sheet.Species = Str(race, "fullName")
                         ?? Str(race, "raceName")
                         ?? Str(race, "baseRaceName");
        }

        // ── Classes ───────────────────────────────────────────────────────────
        if (ch.TryGetProperty("classes", out var classes))
        {
            int    totalLevel      = 0;
            string? primaryName   = null;
            int     primaryLevel  = 0;

            foreach (var cls in classes.EnumerateArray())
            {
                int lvl = Int(cls, "level") ?? 0;
                totalLevel += lvl;

                string? name = null;
                if (cls.TryGetProperty("definition", out var clsDef))
                    name = Str(clsDef, "name");

                // Subclass — emit as a sub-feature so InferSubclass can resolve it.
                if (cls.TryGetProperty("subclassDefinition", out var subDef))
                {
                    string? subName = Str(subDef, "name");
                    if (!string.IsNullOrWhiteSpace(subName))
                        sheet.Features.Add(new ParsedFeature { Name = subName, IsSubFeature = true });
                }

                bool isStarting = Bool(cls, "isStartingClass") ?? false;
                if (isStarting || primaryName == null || lvl > primaryLevel)
                {
                    primaryName  = name;
                    primaryLevel = lvl;
                }
            }

            sheet.ClassName  = primaryName;
            sheet.ClassLevel = totalLevel;
        }

        // ── Background ────────────────────────────────────────────────────────
        if (ch.TryGetProperty("background", out var bg))
        {
            sheet.Background = bg.TryGetProperty("definition", out var bgDef)
                ? Str(bgDef, "name")
                : Str(bg, "name");
        }

        // ── Ability scores ────────────────────────────────────────────────────
        // stat IDs: 1=STR  2=DEX  3=CON  4=INT  5=WIS  6=CHA
        var baseStats     = ParseStatArray(ch, "stats");
        var bonusStats    = ParseStatArray(ch, "bonusStats");
        var overrideStats = ParseStatArray(ch, "overrideStats");

        sheet.Strength     = FinalStat(1, baseStats, bonusStats, overrideStats);
        sheet.Dexterity    = FinalStat(2, baseStats, bonusStats, overrideStats);
        sheet.Constitution = FinalStat(3, baseStats, bonusStats, overrideStats);
        sheet.Intelligence = FinalStat(4, baseStats, bonusStats, overrideStats);
        sheet.Wisdom       = FinalStat(5, baseStats, bonusStats, overrideStats);
        sheet.Charisma     = FinalStat(6, baseStats, bonusStats, overrideStats);

        // ── HP ────────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("hitPointInfo", out var hpInfo))
            sheet.MaxHitPoints = Int(hpInfo, "maxHitPoints");
        // Older export format stores max HP directly on the character.
        sheet.MaxHitPoints ??= Int(ch, "maxHitPoints");

        // ── Feats ─────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("feats", out var feats))
        {
            foreach (var feat in feats.EnumerateArray())
            {
                string? name = null;
                if (feat.TryGetProperty("definition", out var featDef))
                    name = Str(featDef, "name");
                name ??= Str(feat, "name");
                if (!string.IsNullOrWhiteSpace(name))
                    sheet.Feats.Add(new ParsedFeature { Name = name });
            }
        }

        // ── Spells ────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("spells", out var spellsEl))
        {
            // Spells from class sources.
            if (spellsEl.TryGetProperty("class", out var classSources))
                foreach (var src in classSources.EnumerateArray())
                    if (src.TryGetProperty("spells", out var list))
                        AddSpells(list, sheet);

            // Spells granted by race, feats, or items.
            foreach (string key in (string[])["race", "feat", "item"])
                if (spellsEl.TryGetProperty(key, out var src))
                    AddSpells(src, sheet);
        }

        // ── Inventory ─────────────────────────────────────────────────────────
        if (ch.TryGetProperty("inventory", out var inventory))
        {
            foreach (var item in inventory.EnumerateArray())
            {
                if (!item.TryGetProperty("definition", out var itemDef)) continue;
                string? name = Str(itemDef, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                int qty = Int(item, "quantity") ?? 1;
                sheet.Equipment.Add(new ParsedItem { Name = name, Quantity = qty });
            }
        }

        // ── Traits ────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("traits", out var traits))
        {
            sheet.PersonalityTraits = Str(traits, "personalityTraits");
            sheet.Ideals            = Str(traits, "ideals");
            sheet.Bonds             = Str(traits, "bonds");
            sheet.Flaws             = Str(traits, "flaws");
        }

        // ── Backstory ─────────────────────────────────────────────────────────
        if (ch.TryGetProperty("notes", out var notes))
            sheet.Backstory = Str(notes, "backstory");

        // ── Languages ────────────────────────────────────────────────────────
        if (ch.TryGetProperty("proficiencies", out var profs))
        {
            foreach (var prof in profs.EnumerateArray())
            {
                if (!prof.TryGetProperty("definition", out var profDef)) continue;
                string? name = Str(profDef, "name");
                string? type = Str(profDef, "type") ?? Str(profDef, "proficiencyGroup");
                if (!string.IsNullOrWhiteSpace(name) &&
                    string.Equals(type, "Language", StringComparison.OrdinalIgnoreCase))
                    sheet.Languages.Add(name);
            }
        }

        // ── Alignment (stored as an integer type ID in some exports) ──────────
        if (ch.TryGetProperty("alignmentId", out var alignEl) && alignEl.ValueKind == JsonValueKind.Number)
        {
            sheet.Alignment = alignEl.GetInt32() switch
            {
                1 => "Lawful Good",    2 => "Neutral Good",    3 => "Chaotic Good",
                4 => "Lawful Neutral", 5 => "True Neutral",    6 => "Chaotic Neutral",
                7 => "Lawful Evil",    8 => "Neutral Evil",    9 => "Chaotic Evil",
                _ => null,
            };
        }

        // ── Appearance (stored in characterValues array) ──────────────────────
        if (ch.TryGetProperty("characterValues", out var charVals))
        {
            foreach (var cv in charVals.EnumerateArray())
            {
                int? typeId = Int(cv, "typeId");
                string? val = Str(cv, "value");
                if (val == null) continue;
                switch (typeId)
                {
                    case 1:  sheet.Gender   = val; break;
                    case 2:  sheet.Age      = val; break;
                    case 3:  sheet.Height   = val; break;
                    case 4:  sheet.Weight   = val; break;
                    case 5:  sheet.SkinColor = val; break;
                    case 6:  sheet.EyeColor = val; break;
                    case 7:  sheet.HairColor = val; break;
                    case 8:  sheet.Faith    = val; break;
                }
            }
        }

        sheet.ParseDiagnostics =
            $"Imported from D&D Beyond JSON | Name={sheet.CharacterName} " +
            $"| Class={sheet.ClassName} | Level={sheet.ClassLevel} " +
            $"| Race={sheet.Species} | Background={sheet.Background}";

        return sheet;
    }

    // ── Spell helpers ─────────────────────────────────────────────────────────

    private static void AddSpells(JsonElement spellArray, ParsedCharacterSheet sheet)
    {
        foreach (var entry in spellArray.EnumerateArray())
        {
            string? name = null;
            int?    level = null;
            if (entry.TryGetProperty("definition", out var def))
            {
                name  = Str(def, "name");
                level = Int(def, "level");
            }
            if (!string.IsNullOrWhiteSpace(name))
                sheet.Spells.Add(new ParsedSpell { Name = name, Level = level });
        }
    }

    // ── Ability score helpers ─────────────────────────────────────────────────

    private static Dictionary<int, int> ParseStatArray(JsonElement ch, string key)
    {
        var result = new Dictionary<int, int>();
        if (!ch.TryGetProperty(key, out var arr)) return result;
        foreach (var stat in arr.EnumerateArray())
        {
            int? id  = Int(stat, "id");
            int? val = Int(stat, "value");
            if (id.HasValue && val.HasValue) result[id.Value] = val.Value;
        }
        return result;
    }

    private static int? FinalStat(
        int id,
        Dictionary<int, int> baseStats,
        Dictionary<int, int> bonusStats,
        Dictionary<int, int> overrideStats)
    {
        // Override takes precedence over everything.
        if (overrideStats.TryGetValue(id, out int ov)) return ov;

        int total = baseStats.GetValueOrDefault(id);
        if (bonusStats.TryGetValue(id, out int bonus)) total += bonus;
        return total > 0 ? total : null;
    }

    // ── JSON element helpers ──────────────────────────────────────────────────

    private static string? Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? Int(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        // D&D Beyond sometimes stores integers as strings in older exports.
        if (v.ValueKind == JsonValueKind.String &&
            int.TryParse(v.GetString(), out int parsed))
            return parsed;
        return null;
    }

    private static bool? Bool(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;
    }
}
