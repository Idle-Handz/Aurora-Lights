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
        if (ch.TryGetProperty("classes", out var classes) &&
            classes.ValueKind == JsonValueKind.Array)
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
            string? bgName = null;

            // v5 API: definition may be present but JSON null (custom backgrounds).
            if (bg.TryGetProperty("definition", out var bgDef) &&
                bgDef.ValueKind == JsonValueKind.Object)
            {
                bgName = Str(bgDef, "name");
            }

            // Custom background: name lives under customBackground.featuresBackground.name.
            if (bgName == null &&
                bg.TryGetProperty("customBackground", out var customBg) &&
                customBg.ValueKind == JsonValueKind.Object &&
                customBg.TryGetProperty("featuresBackground", out var featBg) &&
                featBg.ValueKind == JsonValueKind.Object)
            {
                bgName = Str(featBg, "name");
            }

            sheet.Background = bgName ?? Str(bg, "name");
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
        // v5 API uses baseHitPoints.
        sheet.MaxHitPoints ??= Int(ch, "baseHitPoints");

        // ── Feats ─────────────────────────────────────────────────────────────
        if (ch.TryGetProperty("feats", out var feats) &&
            feats.ValueKind == JsonValueKind.Array)
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
            // Spells from class sources (older export format).
            if (spellsEl.TryGetProperty("class", out var classSources))
                foreach (var src in classSources.EnumerateArray())
                    if (src.TryGetProperty("spells", out var list))
                        AddSpells(list, sheet);

            // Spells granted by race, feats, or items.
            foreach (string key in (string[])["race", "feat", "item"])
                if (spellsEl.TryGetProperty(key, out var src))
                    AddSpells(src, sheet);
        }

        // v5 API: class spells live in a top-level classSpells array.
        if (ch.TryGetProperty("classSpells", out var classSpellsEl))
            foreach (var entry in classSpellsEl.EnumerateArray())
                if (entry.TryGetProperty("spells", out var list))
                    AddSpells(list, sheet);

        // ── Inventory ─────────────────────────────────────────────────────────
        if (ch.TryGetProperty("inventory", out var inventory) &&
            inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in inventory.EnumerateArray())
            {
                if (!item.TryGetProperty("definition", out var itemDef) ||
                    itemDef.ValueKind != JsonValueKind.Object) continue;
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
        // v5 API: definition.type is an integer (5 = Language).
        // Older exports may use the string "Language" or a "proficiencyGroup" field.
        if (ch.TryGetProperty("proficiencies", out var profs) &&
            profs.ValueKind == JsonValueKind.Array)
        {
            foreach (var prof in profs.EnumerateArray())
            {
                if (!prof.TryGetProperty("definition", out var profDef)) continue;
                string? name = Str(profDef, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                bool isLanguage = false;
                if (profDef.TryGetProperty("type", out var typeEl))
                {
                    isLanguage = typeEl.ValueKind == JsonValueKind.Number
                        ? typeEl.GetInt32() == 5
                        : string.Equals(typeEl.GetString(), "Language", StringComparison.OrdinalIgnoreCase);
                }
                isLanguage |= string.Equals(
                    Str(profDef, "proficiencyGroup"), "Language", StringComparison.OrdinalIgnoreCase);

                if (isLanguage)
                    sheet.Languages.Add(name);
            }
        }

        // v5 API: languages, saving-throw proficiencies, and skill proficiencies
        // all live in modifiers.{race,class,background,feat,item}[].
        // One pass handles all three categories.
        if (ch.TryGetProperty("modifiers", out var modifiers) &&
            modifiers.ValueKind == JsonValueKind.Object)
        {
            foreach (string modKey in (string[])["race", "class", "background", "feat", "item"])
            {
                if (!modifiers.TryGetProperty(modKey, out var modList) ||
                    modList.ValueKind != JsonValueKind.Array) continue;
                foreach (var mod in modList.EnumerateArray())
                {
                    string? type    = Str(mod, "type");
                    string? subType = Str(mod, "subType");
                    if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(subType)) continue;

                    if (string.Equals(type, "language", StringComparison.OrdinalIgnoreCase))
                    {
                        string? langName = Str(mod, "friendlySubtypeName") ?? subType;
                        if (!string.IsNullOrWhiteSpace(langName) && !sheet.Languages.Contains(langName))
                            sheet.Languages.Add(langName);
                    }
                    else if (string.Equals(type, "saving-throw", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (subType.ToLowerInvariant())
                        {
                            case "strength":     sheet.SaveStrength     = true; break;
                            case "dexterity":    sheet.SaveDexterity    = true; break;
                            case "constitution": sheet.SaveConstitution = true; break;
                            case "intelligence": sheet.SaveIntelligence = true; break;
                            case "wisdom":       sheet.SaveWisdom       = true; break;
                            case "charisma":     sheet.SaveCharisma     = true; break;
                        }
                    }
                    else if (string.Equals(type, "proficiency", StringComparison.OrdinalIgnoreCase) &&
                             _skillSubTypes.Contains(subType))
                    {
                        // Use the friendly display name so Aurora can match "Sleight of Hand"
                        // rather than "sleight-of-hand".
                        string? displayName = Str(mod, "friendlySubtypeName")
                            ?? ToTitleCase(subType.Replace('-', ' '));
                        if (!string.IsNullOrWhiteSpace(displayName))
                            sheet.ProficientSkills.Add(displayName);
                    }
                }
            }
        }

        // ── Choices (v5 API) ──────────────────────────────────────────────────
        // choices.choiceDefinitions is a flat id→label lookup for every option.
        // Walk choices.{class,race,background,feat} to find what was selected,
        // then route each resolved name to the appropriate sheet field.
        var optionLabels = BuildChoiceDefinitionsLookup(ch);
        if (optionLabels.Count > 0 &&
            ch.TryGetProperty("choices", out var choicesEl) &&
            choicesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (string choiceKey in (string[])["class", "race", "background", "feat", "item"])
            {
                if (!choicesEl.TryGetProperty(choiceKey, out var choiceList) ||
                    choiceList.ValueKind != JsonValueKind.Array) continue;

                foreach (var choice in choiceList.EnumerateArray())
                {
                    if (choice.ValueKind != JsonValueKind.Object) continue;
                    int? optionValue = Int(choice, "optionValue");
                    if (optionValue == null) continue;
                    if (!optionLabels.TryGetValue(optionValue.Value, out string? resolved) ||
                        string.IsNullOrWhiteSpace(resolved)) continue;

                    string prompt = Str(choice, "label") ?? "";

                    // Skip ASI selections (e.g. "Intelligence Score" from Custom Lineage) —
                    // these are ability score picks, not elements.
                    if (resolved.EndsWith(" Score", StringComparison.OrdinalIgnoreCase) &&
                        _abilityNames.Any(a => resolved.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Skip spells already captured from classSpells / spells arrays.
                    if (sheet.Spells.Any(s => string.Equals(s.Name, resolved, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Route by name match first (most reliable), then by prompt hint.
                    if (_skillDisplayNames.Contains(resolved))
                        sheet.ProficientSkills.Add(resolved);
                    else if (prompt.Contains("tool", StringComparison.OrdinalIgnoreCase))
                        sheet.ToolProficiencies.Add(resolved);
                    else if (prompt.Contains("language", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!sheet.Languages.Contains(resolved))
                            sheet.Languages.Add(resolved);
                    }
                    else if (!sheet.ImportChoices.Contains(resolved))
                        sheet.ImportChoices.Add(resolved);
                }
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
        if (ch.TryGetProperty("characterValues", out var charVals) &&
            charVals.ValueKind == JsonValueKind.Array)
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

        // ── Portrait ─────────────────────────────────────────────────────────
        if (ch.TryGetProperty("decorations", out var decs) &&
            decs.ValueKind == JsonValueKind.Object)
        {
            string? avatarUrl = Str(decs, "avatarUrl");
            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                // Strip DDB's resize query params to get the original-resolution image.
                int q = avatarUrl.IndexOf('?', StringComparison.Ordinal);
                sheet.PortraitUrl = q >= 0 ? avatarUrl[..q] : avatarUrl;
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
        if (spellArray.ValueKind != JsonValueKind.Array) return;
        foreach (var entry in spellArray.EnumerateArray())
        {
            string? name  = null;
            int?    level = null;
            if (entry.TryGetProperty("definition", out var def) &&
                def.ValueKind == JsonValueKind.Object)
            {
                name  = Str(def, "name");
                level = Int(def, "level");
            }
            if (string.IsNullOrWhiteSpace(name)) continue;

            bool prepared = (Bool(entry, "prepared") ?? false)
                         || (Bool(entry, "alwaysPrepared") ?? false);

            sheet.Spells.Add(new ParsedSpell { Name = name, Level = level, IsPrepared = prepared });
        }
    }

    // ── Ability score helpers ─────────────────────────────────────────────────

    private static Dictionary<int, int> ParseStatArray(JsonElement ch, string key)
    {
        var result = new Dictionary<int, int>();
        if (!ch.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;
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

    private static string? Str(JsonElement el, string key)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        return el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    private static int? Int(JsonElement el, string key)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
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
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(key, out var v)) return null;
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;
    }

    // ── Choice definitions lookup ─────────────────────────────────────────────

    private static Dictionary<int, string> BuildChoiceDefinitionsLookup(JsonElement ch)
    {
        var lookup = new Dictionary<int, string>();
        if (!ch.TryGetProperty("choices", out var choicesEl) ||
            choicesEl.ValueKind != JsonValueKind.Object) return lookup;
        if (!choicesEl.TryGetProperty("choiceDefinitions", out var defs) ||
            defs.ValueKind != JsonValueKind.Array) return lookup;

        foreach (var def in defs.EnumerateArray())
        {
            if (!def.TryGetProperty("options", out var opts) ||
                opts.ValueKind != JsonValueKind.Array) continue;
            foreach (var opt in opts.EnumerateArray())
            {
                int? id = Int(opt, "id");
                string? label = Str(opt, "label");
                if (id.HasValue && !string.IsNullOrWhiteSpace(label))
                    lookup.TryAdd(id.Value, label);
            }
        }
        return lookup;
    }

    // ── Skill sub-type lookup ─────────────────────────────────────────────────

    private static readonly HashSet<string> _skillSubTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "acrobatics", "animal-handling", "arcana", "athletics", "deception",
        "history", "insight", "intimidation", "investigation", "medicine",
        "nature", "perception", "performance", "persuasion", "religion",
        "sleight-of-hand", "stealth", "survival",
    };

    // Ability score names — used to detect and skip ASI selections like "Intelligence Score".
    private static readonly string[] _abilityNames =
        ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

    // Skill display names for categorising resolved choice labels.
    private static readonly HashSet<string> _skillDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acrobatics", "Animal Handling", "Arcana", "Athletics", "Deception",
        "History", "Insight", "Intimidation", "Investigation", "Medicine",
        "Nature", "Perception", "Performance", "Persuasion", "Religion",
        "Sleight of Hand", "Stealth", "Survival",
    };

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool capitalise = true;
        foreach (char c in s)
        {
            sb.Append(capitalise ? char.ToUpperInvariant(c) : c);
            capitalise = c == ' ';
        }
        return sb.ToString();
    }
}
