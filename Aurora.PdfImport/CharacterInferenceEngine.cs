using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Aurora.PdfImport;

/// <summary>
/// Resolves a <see cref="ParsedCharacterSheet"/> against the Aurora SQLite DB,
/// producing an <see cref="ImportResult"/> with element aurora IDs and warnings.
/// </summary>
public sealed class CharacterInferenceEngine
{
    private readonly string _dbPath;

    // Primary stat used for ASI inference when unclear.
    // Keyed by normalised class name (lowercase, no "(archived)", no spaces).
    private static readonly Dictionary<string, string[]> _classPrimaryStats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["barbarian"]    = ["Strength"],
        ["bard"]         = ["Charisma"],
        ["bloodhunter"]  = ["Wisdom", "Dexterity"],
        ["cleric"]       = ["Wisdom"],
        ["druid"]        = ["Wisdom"],
        ["fighter"]      = ["Strength", "Dexterity"],
        ["monk"]         = ["Dexterity", "Wisdom"],
        ["paladin"]      = ["Strength", "Charisma"],
        ["ranger"]       = ["Dexterity", "Wisdom"],
        ["rogue"]        = ["Dexterity"],
        ["sorcerer"]     = ["Charisma"],
        ["warlock"]      = ["Charisma"],
        ["wizard"]       = ["Intelligence"],
        ["artificer"]    = ["Intelligence"],
    };

    // ASI levels per class (standard PHB; multi-class not handled here).
    private static readonly Dictionary<string, int[]> _classAsiLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fighter"]  = [4, 6, 8, 12, 14, 16, 19],
        ["rogue"]    = [4, 8, 10, 12, 16, 19],
        ["default"]  = [4, 8, 12, 16, 19],
    };

    public CharacterInferenceEngine(string sqliteDbPath) => _dbPath = sqliteDbPath;

    // ── Main entry point ─────────────────────────────────────────────────────

    public ImportResult Infer(ParsedCharacterSheet sheet)
    {
        var result = new ImportResult();

        // Flat character data.
        result.CharacterName     = sheet.CharacterName  ?? "";
        result.PlayerName        = sheet.PlayerName     ?? "";
        result.Gender            = sheet.Gender         ?? "";
        result.Age               = sheet.Age            ?? "";
        result.Height            = sheet.Height         ?? "";
        result.Weight            = sheet.Weight         ?? "";
        result.Eyes              = sheet.EyeColor       ?? "";
        result.Skin              = sheet.SkinColor      ?? "";
        result.Hair              = sheet.HairColor      ?? "";
        result.Alignment         = sheet.Alignment      ?? "";
        result.Backstory         = sheet.Backstory      ?? "";
        result.PersonalityTraits = sheet.PersonalityTraits ?? "";
        result.Ideals            = sheet.Ideals         ?? "";
        result.Bonds             = sheet.Bonds          ?? "";
        result.Flaws             = sheet.Flaws          ?? "";

        // Ability scores — use parsed values or fall back to 10.
        result.Strength     = sheet.Strength     ?? 10;
        result.Dexterity    = sheet.Dexterity    ?? 10;
        result.Constitution = sheet.Constitution ?? 10;
        result.Intelligence = sheet.Intelligence ?? 10;
        result.Wisdom       = sheet.Wisdom       ?? 10;
        result.Charisma     = sheet.Charisma     ?? 10;

        result.Level            = sheet.ClassLevel ?? 1;
        result.ParseDiagnostics = sheet.ParseDiagnostics;

        // ── Element resolution ────────────────────────────────────────────────

        string? className = CleanClassName(sheet.ClassName);
        result.DisplayClass      = className ?? sheet.ClassName ?? "";
        result.DisplayRace       = sheet.Species    ?? "";
        result.DisplayBackground = sheet.Background ?? "";

        using var connection = OpenDb();

        // Race (and Sub Race if D&D Beyond reported a subrace name like "High Elf").
        if (!string.IsNullOrWhiteSpace(sheet.Species))
            ResolveRaceWithSubrace(connection, sheet.Species, result);

        // Class.
        if (!string.IsNullOrWhiteSpace(className))
            ResolveByName(connection, "Class", className, result, level: 1);

        // Background.
        if (!string.IsNullOrWhiteSpace(sheet.Background))
            ResolveByName(connection, "Background", sheet.Background, result, level: 1);

        // Level elements — one per class level.
        if (!string.IsNullOrWhiteSpace(className))
            InferLevelElements(connection, className, result.Level, result);

        // Subclass — inferred from features with sub-feature names.
        if (!string.IsNullOrWhiteSpace(className))
            InferSubclass(connection, className, sheet, result);

        // Feats.
        foreach (var feat in sheet.Feats)
            ResolveByName(connection, "Feat", feat.Name, result, level: 1);

        // Spells.
        foreach (var spell in sheet.Spells)
            ResolveByName(connection, "Spell", spell.Name, result, level: 1);

        // Languages (from proficiencies block, supplementary to what elements grant).
        foreach (string lang in sheet.Languages)
            ResolveByName(connection, "Language", lang, result, level: 1, warnIfMissing: false);

        // Choices resolved from D&D Beyond choiceDefinitions — things like infusions,
        // artisan tools, and any other class/race/background pick not already categorised.
        foreach (string choice in sheet.ImportChoices)
            ResolveChoiceByName(connection, choice, result);

        // Saving throw proficiencies.
        AddSavingThrowProficiency(connection, result, "Strength", sheet.SaveStrength);
        AddSavingThrowProficiency(connection, result, "Dexterity", sheet.SaveDexterity);
        AddSavingThrowProficiency(connection, result, "Constitution", sheet.SaveConstitution);
        AddSavingThrowProficiency(connection, result, "Intelligence", sheet.SaveIntelligence);
        AddSavingThrowProficiency(connection, result, "Wisdom", sheet.SaveWisdom);
        AddSavingThrowProficiency(connection, result, "Charisma", sheet.SaveCharisma);

        // Skill/tool/armor/weapon proficiencies.
        foreach (string skill in sheet.ProficientSkills)
            ResolveByCandidateNames(
                connection,
                "Proficiency",
                [$"{skill} Proficiency"],
                result,
                level: 1,
                warnIfMissing: false);

        foreach (string tool in sheet.ToolProficiencies)
            ResolveByCandidateNames(
                connection,
                "Proficiency",
                BuildToolProficiencyCandidates(tool),
                result,
                level: 1,
                warnIfMissing: false);

        foreach (string armor in sheet.ArmorProficiencies)
            ResolveByCandidateNames(
                connection,
                "Proficiency",
                BuildArmorProficiencyCandidates(armor),
                result,
                level: 1,
                warnIfMissing: false);

        foreach (string weapon in sheet.WeaponProficiencies)
            ResolveByCandidateNames(
                connection,
                "Proficiency",
                BuildWeaponProficiencyCandidates(weapon),
                result,
                level: 1,
                warnIfMissing: false);

        // Collect all resolved aurora IDs so we can detect feats that were automatically
        // granted (not spent from ASI slots) before running ASI inference.
        var allResolvedIds = result.Elements
            .Concat(result.Ambiguities.Where(a => a.Chosen != null).Select(a => a.Chosen!))
            .Select(e => e.AuroraId)
            .Distinct()
            .ToList();
        int freeFeats = CountAutoGrantedFeats(connection, allResolvedIds);

        // ASI inference.
        InferAsi(className, sheet, result, freeFeats);

        return result;
    }

    // ── Resolve by name ───────────────────────────────────────────────────────

    private void ResolveByName(
        SqliteConnection conn,
        string typeName,
        string name,
        ImportResult result,
        int level,
        bool warnIfMissing = true)
    {
        // Strip source suffix from feat names like "Svirfneblin Magic (EE)" → "Svirfneblin Magic".
        string cleanName = Regex.Replace(name, @"\s*\([A-Z][A-Za-z\-]*\d*\)\s*$", "").Trim();

        var matches = QueryByNameAndType(conn, typeName, cleanName);

        if (matches.Count == 0)
        {
            if (warnIfMissing)
                result.Warnings.Add(new ImportWarning
                {
                    Category = typeName,
                    Item     = cleanName,
                    Reason   = "No matching element found in installed content packages.",
                });
            return;
        }

        if (matches.Count == 1 || typeName == "Spell")
        {
            // For spells, take the first match (we're not deduplicating editions).
            result.Elements.Add(matches[0] with { Level = level });
            return;
        }

        // Multiple matches — record as ambiguity for UI resolution.
        result.Ambiguities.Add(new ImportAmbiguity
        {
            Category   = typeName,
            Item       = cleanName,
            Candidates = matches.Select(m => m with { Level = level }).ToList(),
        });

        // Pre-choose the highest-precedence match as the default.
        result.Ambiguities[^1].Chosen = matches[0] with { Level = level };
    }

    private void ResolveByCandidateNames(
        SqliteConnection conn,
        string typeName,
        IEnumerable<string> names,
        ImportResult result,
        int level,
        bool warnIfMissing = true)
    {
        foreach (string candidate in names
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int elementCount = result.Elements.Count;
            int ambiguityCount = result.Ambiguities.Count;
            int warningCount = result.Warnings.Count;

            ResolveByName(conn, typeName, candidate, result, level, warnIfMissing);

            if (result.Elements.Count > elementCount || result.Ambiguities.Count > ambiguityCount)
                return;

            if (!warnIfMissing && result.Warnings.Count > warningCount)
                result.Warnings.RemoveAt(result.Warnings.Count - 1);
        }
    }

    private List<ResolvedElement> QueryByNameAndType(SqliteConnection conn, string typeName, string name)
    {
        const string sql = """
            WITH candidate_matches AS (
                SELECT
                    e.aurora_id,
                    et.type_name,
                    e.name,
                    cp.package_name,
                    COALESCE(cp.precedence_rank, -2147483648) AS precedence_rank,
                    CASE
                        WHEN COALESCE(se.is_core, 0) = 1 OR COALESCE(cp.package_kind, '') = 'core' THEN 2
                        WHEN COALESCE(se.is_official, 0) = 1 OR COALESCE(cp.package_kind, '') = 'official' THEN 1
                        ELSE 0
                    END AS official_rank,
                    CASE
                        WHEN e.name = $name THEN 0
                        WHEN LOWER(e.name) = LOWER($name) THEN 1
                        ELSE 2
                    END AS match_rank,
                    ROW_NUMBER() OVER (
                        PARTITION BY e.aurora_id
                        ORDER BY
                            CASE
                                WHEN e.name = $name THEN 0
                                WHEN LOWER(e.name) = LOWER($name) THEN 1
                                ELSE 2
                            END ASC,
                            COALESCE(cp.precedence_rank, -2147483648) DESC,
                            e.element_id ASC
                    ) AS row_num
                FROM elements e
                JOIN element_types et ON e.element_type_id = et.element_type_id
                LEFT JOIN source_files sf ON e.source_file_id = sf.source_file_id
                LEFT JOIN content_packages cp ON sf.content_package_id = cp.content_package_id
                LEFT JOIN source_elements se ON e.element_id = se.element_id
                WHERE et.type_name = $type
                  AND (e.name = $name OR e.name LIKE $nameLike)
                  AND (cp.is_enabled IS NULL OR cp.is_enabled = 1)
            ),
            preferred_matches AS (
                SELECT *
                FROM candidate_matches
                WHERE official_rank = (SELECT MAX(official_rank) FROM candidate_matches)
            ),
            ranked_matches AS (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY aurora_id
                        ORDER BY
                            match_rank ASC,
                            official_rank DESC,
                            precedence_rank DESC,
                            name ASC,
                            aurora_id ASC
                    ) AS row_num
                FROM preferred_matches
            )
            SELECT aurora_id, type_name, name, package_name, precedence_rank, official_rank, match_rank
            FROM ranked_matches
            WHERE row_num = 1
            ORDER BY match_rank ASC, official_rank DESC, precedence_rank DESC, name ASC, aurora_id ASC
            LIMIT 50;
            """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$type",     typeName);
        cmd.Parameters.AddWithValue("$name",     name);
        cmd.Parameters.AddWithValue("$nameLike", $"%{name}%");

        var results = new List<ResolvedElement>();
        var seenDisplayKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var resolved = new ResolvedElement
            {
                AuroraId    = reader.GetString(0),
                TypeName    = reader.GetString(1),
                Name        = reader.GetString(2),
                PackageName = reader.IsDBNull(3) ? null : reader.GetString(3),
            };

            string displayKey = $"{resolved.Name}|{resolved.PackageName}|{resolved.AuroraId}";
            if (seenDisplayKeys.Add(displayKey))
                results.Add(resolved);
        }
        return results;
    }

    private void ResolveChoiceByName(SqliteConnection conn, string name, ImportResult result)
    {
        // Try element types in priority order; stop at the first hit.
        foreach (string type in (string[])[
            "Class Feature", "Infusion", "Archetype Feature",
            "Racial Trait", "Race Variant", "Vision",
            "Feat", "Spell", "Proficiency"])
        {
            int before = result.Elements.Count + result.Ambiguities.Count;
            ResolveByName(conn, type, name, result, level: 1, warnIfMissing: false);
            if (result.Elements.Count + result.Ambiguities.Count > before) return;
        }

        result.Warnings.Add(new ImportWarning
        {
            Category = "Choice",
            Item     = name,
            Reason   = "Could not match to an installed element; set it manually in the Build tab.",
        });
    }

    private static IEnumerable<string> BuildToolProficiencyCandidates(string name)
    {
        string normalized = NormalizeProficiencyName(name);
        yield return normalized;
        yield return $"Tool Proficiency ({normalized})";
    }

    private static IEnumerable<string> BuildArmorProficiencyCandidates(string name)
    {
        string normalized = NormalizeProficiencyName(name);
        yield return normalized;
        yield return $"Armor Proficiency ({normalized})";
        if (normalized.EndsWith(" Armor", StringComparison.OrdinalIgnoreCase))
            yield return $"Armor Proficiency ({normalized.Replace(" Armor", "", StringComparison.OrdinalIgnoreCase)})";
    }

    private static IEnumerable<string> BuildWeaponProficiencyCandidates(string name)
    {
        string normalized = NormalizeProficiencyName(name);
        yield return normalized;
        yield return $"Weapon Proficiency ({normalized})";
    }

    private void AddSavingThrowProficiency(
        SqliteConnection conn,
        ImportResult result,
        string abilityName,
        bool isProficient)
    {
        if (!isProficient)
            return;

        ResolveByCandidateNames(
            conn,
            "Proficiency",
            [$"Saving Throw Proficiency ({abilityName})"],
            result,
            level: 1,
            warnIfMissing: false);
    }

    private static string NormalizeProficiencyName(string name) =>
        name
            .Replace("'", "’", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();

    // ── Race / subrace resolution ─────────────────────────────────────────────

    /// <summary>
    /// Resolves the character's species against the content DB, handling both top-level
    /// Race elements (Human, Half-Elf, Dragonborn) and Sub Race elements (High Elf, Hill
    /// Dwarf) where D&D Beyond reports the subrace name rather than the parent race name.
    /// When a Sub Race match is found, the parent Race element is looked up automatically
    /// via the Sub Race's element_supports entry so that both are added to the result.
    /// </summary>
    private void ResolveRaceWithSubrace(SqliteConnection conn, string species, ImportResult result)
    {
        // First: try as a top-level Race element.
        int elementsBefore   = result.Elements.Count;
        int ambigsBefore     = result.Ambiguities.Count;
        ResolveByName(conn, "Race", species, result, level: 1, warnIfMissing: false);
        bool foundAsRace = result.Elements.Count > elementsBefore
                        || result.Ambiguities.Count > ambigsBefore;
        if (foundAsRace) return;

        // Second: try as a Sub Race element (e.g., "High Elf", "Hill Dwarf").
        var subRaceMatches = QueryByNameAndType(conn, "Sub Race", species);
        if (subRaceMatches.Count == 0)
        {
            result.Warnings.Add(new ImportWarning
            {
                Category = "Race",
                Item     = species,
                Reason   = "No matching Race or Sub Race element found in installed content packages.",
            });
            return;
        }

        ResolvedElement subRace;
        if (subRaceMatches.Count == 1)
        {
            subRace = subRaceMatches[0] with { Level = 1 };
            result.Elements.Add(subRace);
        }
        else
        {
            var amb = new ImportAmbiguity
            {
                Category   = "Sub Race",
                Item       = species,
                Candidates = subRaceMatches.Select(m => m with { Level = 1 }).ToList(),
            };
            amb.Chosen = amb.Candidates[0];
            result.Ambiguities.Add(amb);
            subRace = amb.Chosen;
        }

        // Look up the parent Race element via the sub race's element_supports entry
        // (the <supports> tag in the XML, e.g., "Elf" for High Elf).
        var parentRace = QueryParentRaceForSubRace(conn, subRace.AuroraId);
        if (parentRace != null)
        {
            result.Elements.Add(parentRace with { Level = 1 });
        }
        else
        {
            result.Warnings.Add(new ImportWarning
            {
                Category = "Race",
                Item     = species,
                Reason   = $"Resolved as Sub Race '{subRace.Name}' but could not locate the parent Race element.",
            });
        }
    }

    private ResolvedElement? QueryParentRaceForSubRace(SqliteConnection conn, string subRaceAuroraId)
    {
        const string sql = """
            SELECT parent.aurora_id, parent_et.type_name, parent.name,
                   cp.package_name
            FROM elements sr
            JOIN element_supports es   ON es.element_id        = sr.element_id
            JOIN elements parent       ON parent.name          = es.support_text
            JOIN element_types parent_et ON parent_et.element_type_id = parent.element_type_id
            LEFT JOIN source_files sf  ON parent.source_file_id = sf.source_file_id
            LEFT JOIN content_packages cp ON sf.content_package_id = cp.content_package_id
            WHERE sr.aurora_id = $aurora_id
              AND parent_et.type_name = 'Race'
              AND (cp.is_enabled IS NULL OR cp.is_enabled = 1)
            ORDER BY COALESCE(cp.precedence_rank, -2147483648) DESC
            LIMIT 1;
            """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$aurora_id", subRaceAuroraId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new ResolvedElement
        {
            AuroraId    = reader.GetString(0),
            TypeName    = reader.GetString(1),
            Name        = reader.GetString(2),
            PackageName = reader.IsDBNull(3) ? null : reader.GetString(3),
        };
    }

    // ── Level element inference ───────────────────────────────────────────────

    private void InferLevelElements(SqliteConnection conn, string className, int totalLevels, ImportResult result)
    {
        // Aurora's level elements are named "1", "2", … "20" (type="Level", id="ID_LEVEL_N").
        // They live in core-levels.xml which is an embedded resource and is not always present
        // in the user's SQLite DB.  Try a DB lookup first; if absent, synthesize from the
        // well-known canonical IDs so the character file is always correct.
        for (int lvl = 1; lvl <= totalLevels; lvl++)
        {
            var matches = QueryByNameAndType(conn, "Level", lvl.ToString());
            if (matches.Count > 0)
            {
                result.Elements.Add(matches[0] with { Level = lvl });
            }
            else
            {
                result.Elements.Add(new ResolvedElement
                {
                    AuroraId    = $"ID_LEVEL_{lvl}",
                    TypeName    = "Level",
                    Name        = lvl.ToString(),
                    PackageName = null,
                    Level       = lvl,
                });
            }
        }
    }

    // ── Subclass inference ────────────────────────────────────────────────────

    private void InferSubclass(SqliteConnection conn, string className, ParsedCharacterSheet sheet, ImportResult result)
    {
        // Find the "Blood Hunter Order" or "Arcane Tradition" sub-feature
        // whose first sub-feature line is the subclass name.
        string? subclassName = null;

        foreach (var feature in sheet.Features)
        {
            if (!feature.IsSubFeature) continue;

            // Sub-features like "| Order of the Lycan (archived)" are subclass selections.
            // Check if this name resolves as an Archetype for this class.
            string candidate = CleanArchivedSuffix(feature.Name);
            var matches = QueryByNameAndType(conn, "Archetype", candidate);
            if (matches.Count > 0)
            {
                result.Elements.Add(matches[0] with { Level = SubclassLevel(className) });
                subclassName = candidate;
                break;
            }
        }

        if (subclassName == null)
        {
            // Try matching from the "Blood Hunter Order" feature description.
            var orderFeature = sheet.Features.FirstOrDefault(f =>
                !f.IsSubFeature && f.Name.Contains("Order", StringComparison.OrdinalIgnoreCase));
            if (orderFeature != null)
            {
                result.Warnings.Add(new ImportWarning
                {
                    Category = "Subclass",
                    Item     = orderFeature.Name,
                    Reason   = "Subclass could not be automatically resolved. Select it manually after import.",
                });
            }
        }
    }

    // ── ASI inference ─────────────────────────────────────────────────────────

    private void InferAsi(string? className, ParsedCharacterSheet sheet, ImportResult result, int freeFeats)
    {
        if (className == null || result.Level < 4) return;

        string key = Regex.Replace(className, @"\s+", "").ToLowerInvariant();
        int[] asiLevels = _classAsiLevels.GetValueOrDefault(key)
                       ?? _classAsiLevels["default"];
        int[] relevantAsiLevels = asiLevels.Where(l => l <= result.Level).ToArray();
        if (relevantAsiLevels.Length == 0) return;

        // Subtract feats that were automatically granted by race/background/archetype
        // (i.e. not chosen in place of an ASI) so we only count ASI-slot feats.
        int featsFromAsi = Math.Max(0, sheet.Feats.Count - freeFeats);
        int asiCount     = relevantAsiLevels.Length - featsFromAsi;
        if (asiCount <= 0) return;

        // Determine which stat to bump: highest stat that is < 20.
        string asiStat = PickAsiStat(className, result);
        string asiStatLower = asiStat.ToLowerInvariant();

        // Look up the ASI element for the chosen stat.
        // Aurora represents ASIs as "Ability Score Improvement" options with rules
        // that grant +2 to one stat. We emit an ASI element for each ASI level.
        for (int i = 0; i < asiCount; i++)
        {
            int asiLevel = relevantAsiLevels[i];
            result.Warnings.Add(new ImportWarning
            {
                Category = "ASI",
                Item     = $"Level {asiLevel} ASI",
                Reason   = $"Assumed +2 {asiStat}. Adjust in the Build tab after import.",
            });
        }
    }

    private static string PickAsiStat(string className, ImportResult result)
    {
        string key = Regex.Replace(className, @"\s+", "").ToLowerInvariant();

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Strength"]     = result.Strength,
            ["Dexterity"]    = result.Dexterity,
            ["Constitution"] = result.Constitution,
            ["Intelligence"] = result.Intelligence,
            ["Wisdom"]       = result.Wisdom,
            ["Charisma"]     = result.Charisma,
        };

        // Try primary stats for the class first, pick the highest uncapped one.
        if (_classPrimaryStats.TryGetValue(key, out string[]? primary))
        {
            var capped = primary
                .Where(s => scores.GetValueOrDefault(s, 0) < 20)
                .OrderByDescending(s => scores.GetValueOrDefault(s, 0))
                .FirstOrDefault();
            if (capped != null) return capped;
        }

        // Fall back to the globally highest uncapped stat.
        return scores
            .Where(kv => kv.Value < 20)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .FirstOrDefault() ?? "Strength";
    }

    // ── Free-feat detection ───────────────────────────────────────────────────

    /// <summary>
    /// Counts feats that the character's resolved elements grant automatically
    /// (direct grants or choice selects owned by non-class-feature elements).
    /// These do NOT consume an ASI slot, so they must be excluded from the
    /// feat-vs-ASI accounting in <see cref="InferAsi"/>.
    /// </summary>
    private int CountAutoGrantedFeats(SqliteConnection conn, List<string> resolvedAuroraIds)
    {
        if (resolvedAuroraIds.Count == 0) return 0;

        using var cmd = conn.CreateCommand();

        var paramNames = resolvedAuroraIds.Select((_, i) => $"$id{i}").ToList();
        string inClause = string.Join(", ", paramNames);
        for (int i = 0; i < resolvedAuroraIds.Count; i++)
            cmd.Parameters.AddWithValue($"$id{i}", resolvedAuroraIds[i]);

        // Direct grants of type "Feat" from any resolved element, plus
        // <select type="Feat"> rules whose owning element is NOT a class
        // feature template (those are ASI-replacing feat slots).
        cmd.CommandText = $"""
            SELECT
                (SELECT COUNT(*)
                 FROM grants g
                 JOIN rule_scopes rs ON rs.rule_scope_id = g.rule_scope_id
                 JOIN elements e     ON e.element_id     = rs.owner_element_id
                 WHERE g.grant_type = 'Feat'
                   AND e.aurora_id IN ({inClause}))
                +
                (SELECT COUNT(DISTINCT s.select_id)
                 FROM selects s
                 JOIN rule_scopes rs  ON rs.rule_scope_id  = s.rule_scope_id
                 JOIN elements e      ON e.element_id      = rs.owner_element_id
                 JOIN element_types et ON et.element_type_id = e.element_type_id
                 WHERE s.select_type = 'Feat'
                   AND et.type_name NOT IN ('Class Feature', 'Level', 'Grants')
                   AND e.aurora_id IN ({inClause}))
            """;

        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SqliteConnection OpenDb()
    {
        var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        conn.Open();
        return conn;
    }

    private static string? CleanClassName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Strip "(archived)", "(legacy)", etc.
        return Regex.Replace(raw, @"\s*\(.*?\)\s*", "").Trim();
    }

    private static string CleanArchivedSuffix(string name) =>
        Regex.Replace(name, @"\s*\(.*?\)\s*$", "").Trim();

    private static int SubclassLevel(string className)
    {
        // Most classes get subclass at level 3; exceptions:
        string key = Regex.Replace(className, @"\s+", "").ToLowerInvariant();
        return key switch
        {
            "fighter"   => 3,
            "rogue"     => 3,
            "barbarian" => 3,
            "wizard"    => 2,
            "cleric"    => 1,
            "sorcerer"  => 1,
            "warlock"   => 1,
            _           => 3,
        };
    }
}
