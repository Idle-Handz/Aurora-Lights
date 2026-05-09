using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.App.Services;

/// <summary>
/// Loads Aurora element data from a pre-built SQLite database, bypassing the XML parsing
/// pipeline in <see cref="DataManager.InitializeElementDataAsync"/>.
///
/// Strategy: bulk-load all element tables from the DB, reconstruct minimal XmlNodes per
/// element (id/name/type/source attributes + supports + requirements + description + sheet
/// + type-specific setters + spellcasting + multiclass + rules), then feed each node through
/// the existing <see cref="ElementParser"/> pipeline exactly as DataManager does. Finally call
/// <see cref="DataManager.RunPostProcessing"/> to synthesize multiclass/ASI/scroll elements.
///
/// Known DB schema gaps:
///   - No general element_setters table; types not covered by a typed subtype table will have
///     empty setters and may parse as plain ElementBase rather than a subclass.
///</summary>
public sealed record DbLoadResult(
    bool Success,
    string? DatabasePath,
    int ElementCount,
    int SkippedElementCount,
    int? SchemaVersion,
    string? FailureReason,
    IReadOnlyList<string> MissingTables,
    IReadOnlyList<string> MissingColumns)
{
    public string SourceLabel => Success ? "SQLite" : "XML fallback";

    public string Summary
    {
        get
        {
            if (Success)
            {
                string skipped = SkippedElementCount > 0
                    ? $", skipped {SkippedElementCount}"
                    : string.Empty;
                string schema = SchemaVersion.HasValue
                    ? $"schema v{SchemaVersion.Value}"
                    : "schema version unknown";
                return $"Loaded {ElementCount} elements from SQLite ({schema}{skipped}).";
            }

            if (MissingTables.Count > 0)
                return $"SQLite load unavailable: missing tables {string.Join(", ", MissingTables)}.";

            if (MissingColumns.Count > 0)
                return $"SQLite load unavailable: missing columns {string.Join(", ", MissingColumns)}.";

            return $"SQLite load unavailable: {FailureReason ?? "unknown reason"}";
        }
    }

    public static DbLoadResult NotAvailable(string? databasePath, string reason) =>
        new(false, databasePath, 0, 0, null, reason, [], []);

    public static DbLoadResult InvalidSchema(
        string databasePath,
        int? schemaVersion,
        IReadOnlyList<string> missingTables,
        IReadOnlyList<string> missingColumns) =>
        new(false, databasePath, 0, 0, schemaVersion,
            missingTables.Count > 0
                ? $"Database schema is missing required tables: {string.Join(", ", missingTables)}."
                : $"Database schema is missing required columns: {string.Join(", ", missingColumns)}.",
            missingTables,
            missingColumns);

    public static DbLoadResult Failed(string databasePath, int? schemaVersion, string reason) =>
        new(false, databasePath, 0, 0, schemaVersion, reason, [], []);

    public static DbLoadResult Loaded(
        string databasePath,
        int? schemaVersion,
        int elementCount,
        int skippedElementCount) =>
        new(true, databasePath, elementCount, skippedElementCount, schemaVersion, null, [], []);
}

internal static class DbElementLoader
{
    private const string DbFileName = "aurora-elements.sqlite";
    private static readonly string[] RequiredTables =
    [
        "elements",
        "element_types",
        "source_books",
        "element_supports",
        "element_requirements",
        "element_texts",
        "grants",
        "rule_scopes",
        "selects",
        "stats",
        "spellcasting_profiles",
        "spells",
        "classes",
        "class_multiclass",
        "setter_scopes",
        "setter_entries",
        "setter_entry_attributes"
    ];
    private static readonly IReadOnlyDictionary<string, string[]> RequiredColumns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["elements"] = ["element_id", "aurora_id", "name", "element_type_id", "source_book_id", "loader_priority"],
            ["element_types"] = ["element_type_id", "type_name"],
            ["source_books"] = ["source_book_id", "name"],
            ["element_supports"] = ["element_id", "support_text"],
            ["element_requirements"] = ["element_id", "requirement_text", "ordinal"],
            ["element_texts"] = ["element_id", "text_kind", "ordinal", "level", "display", "alt_text", "action_text", "usage_text", "body"],
            ["grants"] = ["rule_scope_id", "grant_type", "target_aurora_id", "name_text", "grant_level", "spellcasting_name", "is_prepared", "requirements_text", "ordinal"],
            ["rule_scopes"] = ["rule_scope_id", "owner_element_id", "owner_kind"],
            ["selects"] = ["rule_scope_id", "select_type", "name_text", "supports_text", "select_level", "number_to_choose", "default_choice_text", "is_optional", "requirements_text", "ordinal"],
            ["stats"] = ["rule_scope_id", "stat_name", "value_expression_text", "bonus_expression_text", "equipped_expression_text", "stat_level", "inline_display", "alt_text", "requirements_text", "ordinal"],
            ["spellcasting_profiles"] = ["owner_element_id", "profile_name", "ability_name", "is_extended", "prepare_spells", "allow_replace", "list_text"],
            ["spells"] = ["element_id", "spell_level", "school_name", "casting_time_text", "range_text", "duration_text", "has_verbal", "has_somatic", "has_material", "material_text", "is_concentration", "is_ritual"],
            ["classes"] = ["element_id", "hit_die", "short_text"],
            ["class_multiclass"] = ["class_element_id", "multiclass_aurora_id", "prerequisite_text", "requirements_text", "proficiencies_text"],
            ["setter_scopes"] = ["setter_scope_id", "owner_element_id", "owner_kind"],
            ["setter_entries"] = ["setter_entry_id", "setter_scope_id", "ordinal", "setter_name", "setter_value"],
            ["setter_entry_attributes"] = ["setter_entry_id", "ordinal", "attribute_name", "attribute_value"]
        };

    // ── Runtime lookup caches (populated after a successful DB load) ────────

    /// <summary>Archetype aurora_id → parent class aurora_id. Empty until a DB load succeeds.</summary>
    public static IReadOnlyDictionary<string, string> ArchetypeParentMap { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Class/list name → set of spell aurora_ids that have access. Empty until a DB load succeeds.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> SpellAccessMap { get; private set; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

    public static string? DbPath =>
        DataManager.Current.LocalAppDataRootDirectory is { Length: > 0 } root
            ? Path.Combine(root, DbFileName)
            : null;

    public static bool IsAvailable =>
        DbPath is { } path && File.Exists(path) && new FileInfo(path).Length > 0;

    /// <summary>
    /// Attempts to populate <paramref name="target"/> from the SQLite database.
    /// Returns a detailed result object; callers can automatically fall back to XML when
    /// <see cref="DbLoadResult.Success"/> is <c>false</c>.
    /// </summary>
    public static async Task<DbLoadResult> TryLoadAsync(ElementBaseCollection target)
    {
        string? dbPath = DbPath;
        if (dbPath is null)
            return DbLoadResult.NotAvailable(null, "Local app data directory is not initialized.");

        if (!File.Exists(dbPath))
            return DbLoadResult.NotAvailable(dbPath, "Database file was not found.");

        if (new FileInfo(dbPath).Length <= 0)
            return DbLoadResult.NotAvailable(dbPath, "Database file is empty.");

        try
        {
            DebugLogService.Instance.Info("DbElementLoader: loading elements from DB.", dbPath);
            DbLoadResult result = await Task.Run(() => LoadFromDb(dbPath, target));
            if (result.Success)
            {
                await Task.Run(() => DataManager.Current.RunPostProcessing());
                DebugLogService.Instance.Info(
                    "DbElementLoader: load complete.",
                    result.Summary);
            }
            else
            {
                DebugLogService.Instance.Warn("DbElementLoader: falling back to XML.", result.Summary);
            }
            return result;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "DbElementLoader.TryLoadAsync");
            target.Clear();
            return DbLoadResult.Failed(
                dbPath,
                schemaVersion: null,
                reason: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Raw DB rows ──────────────────────────────────────────────────────────

    private record ElementRow(long Id, string AuroraId, string Name, string TypeName, string Source);
    private record TextRow(long ElementId, string Kind, int Ordinal, int? Level, bool? Display,
        string? AltText, string? ActionText, string? UsageText, string Body);
    private record GrantRow(long ElementId, string OwnerKind, string GrantType,
        string? TargetId, string? Name, int? Level, string? SpellcastingName, bool? IsPrepared, string? Requirements);
    private record SelectRow(long ElementId, string OwnerKind, string SelectType, string Name,
        string? Supports, int? Level, int Number, string? Default, bool Optional, string? Requirements);
    private record StatRow(long ElementId, string OwnerKind, string StatName, string? Value,
        string? Bonus, string? Equipped, int? Level, bool Inline, string? Alt, string? Requirements);
    private record SpellcastingRow(long ElementId, string ProfileName, string? Ability,
        bool IsExtended, bool? Prepare, bool? AllowReplace, string? ListText);
    private record SpellRow(long ElementId, int Level, string? School, string? CastingTime,
        string? Range, string? Duration, bool HasVerbal, bool HasSomatic, bool HasMaterial,
        string? Material, bool IsConcentration, bool IsRitual);
    private record ClassRow(long ElementId, string? HitDie, string? ShortText);
    private record MulticlassRow(long ClassElementId, string? MulticlassId,
        string? Prerequisite, string? Requirements, string? Proficiencies);
    private record SetterRow(string Name, string? Value, IReadOnlyList<(string Key, string? Val)> Attributes);

    // ── Main loader ──────────────────────────────────────────────────────────

    private static DbLoadResult LoadFromDb(string dbPath, ElementBaseCollection target)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();

        int? schemaVersion = QuerySchemaVersion(conn);
        HashSet<string> existingTables = QueryExistingTables(conn);
        List<string> missingTables = RequiredTables
            .Where(table => !existingTables.Contains(table))
            .OrderBy(table => table)
            .ToList();
        if (missingTables.Count > 0)
            return DbLoadResult.InvalidSchema(dbPath, schemaVersion, missingTables, []);

        List<string> missingColumns = QueryMissingColumns(conn);
        if (missingColumns.Count > 0)
            return DbLoadResult.InvalidSchema(dbPath, schemaVersion, [], missingColumns);

        // Quick sanity check.
        using (var chk = conn.CreateCommand())
        {
            chk.CommandText = "SELECT COUNT(*) FROM elements;";
            if ((long)(chk.ExecuteScalar() ?? 0L) == 0)
            {
                DebugLogService.Instance.Warn("DbElementLoader: elements table is empty.");
                return DbLoadResult.Failed(dbPath, schemaVersion, "Elements table is empty.");
            }
        }

        // Bulk-load all tables needed for XML reconstruction.
        var elements       = QueryElements(conn);
        var supportsMap    = QuerySupports(conn);
        var requirementMap = QueryRequirements(conn);
        var textsMap       = QueryTexts(conn);
        var grantsMap      = QueryGrants(conn);
        var selectsMap     = QuerySelects(conn);
        var statsMap       = QueryStats(conn);
        var spellcastingMap = QuerySpellcasting(conn);
        var spellMap       = QuerySpells(conn);
        var classMap       = QueryClasses(conn);
        var multiclassMap  = QueryMulticlass(conn);
        var settersMap     = QuerySetters(conn);
        // Optional tables — gracefully absent in older DBs.
        var featureMinLevelMap  = QueryFeatureMinLevels(conn, existingTables);
        var archetypeParentIds  = QueryArchetypeParentIds(conn, existingTables);
        var spellAccessMap      = QuerySpellAccess(conn, existingTables);

        // Set up the ElementParser pipeline (identical to DataManager).
        var parsers = ElementParserFactory.GetParsers().ToList();
        var defaultParser = new ElementParser();
        var currentParser = new ElementParser();

        // Give the document a proper root so node.OwnerDocument.DocumentElement is non-null,
        // matching what the ElementParser pipeline expects from a loaded XmlDocument.
        var doc = new XmlDocument();
        XmlElement docRoot = doc.CreateElement("elements");
        doc.AppendChild(docRoot);

        int skippedElements = 0;
        var parsed = new List<ElementBase>(elements.Count);
        foreach (var el in elements)
        {
            try
            {
                XmlElement node = BuildElementNode(doc, el,
                    supportsMap, requirementMap, textsMap,
                    grantsMap, selectsMap, statsMap,
                    spellcastingMap, spellMap, classMap, multiclassMap, settersMap,
                    featureMinLevelMap);

                // Attach to the document root so the parser sees a proper node tree.
                docRoot.AppendChild(node);
                try
                {
                    ElementHeader header = currentParser.ParseElementHeader(node);
                    if (currentParser.ParserType != header.Type)
                        currentParser = parsers.FirstOrDefault(p => p.ParserType == header.Type)
                                        ?? defaultParser;

                    ElementBase element = currentParser.ParseElement(node);
                    parsed.Add(element);
                }
                finally
                {
                    docRoot.RemoveChild(node);
                }
            }
            catch (Exception ex)
            {
                skippedElements++;
                DebugLogService.Instance.Warn(
                    $"DbElementLoader: skipped element {el.AuroraId} ({el.TypeName}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        target.Clear();
        target.AddRange(parsed);
        if (target.Count == 0)
            return DbLoadResult.Failed(dbPath, schemaVersion, "No elements could be reconstructed from the database.");

        // Populate runtime caches for use by services after load.
        ArchetypeParentMap = BuildArchetypeParentMap(elements, archetypeParentIds);
        SpellAccessMap     = spellAccessMap;

        return DbLoadResult.Loaded(dbPath, schemaVersion, target.Count, skippedElements);
    }

    // ── XML node construction ────────────────────────────────────────────────

    private static XmlElement BuildElementNode(
        XmlDocument doc,
        ElementRow el,
        Dictionary<long, string> supportsMap,
        Dictionary<long, string> requirementMap,
        Dictionary<long, List<TextRow>> textsMap,
        Dictionary<long, List<GrantRow>> grantsMap,
        Dictionary<long, List<SelectRow>> selectsMap,
        Dictionary<long, List<StatRow>> statsMap,
        Dictionary<long, SpellcastingRow> spellcastingMap,
        Dictionary<long, SpellRow> spellMap,
        Dictionary<long, ClassRow> classMap,
        Dictionary<long, MulticlassRow> multiclassMap,
        Dictionary<long, List<SetterRow>> settersMap,
        Dictionary<long, int> featureMinLevelMap)
    {
        XmlElement node = doc.CreateElement("element");
        node.SetAttribute("id",     el.AuroraId);
        node.SetAttribute("name",   el.Name);
        node.SetAttribute("type",   el.TypeName);
        node.SetAttribute("source", el.Source);

        // Level attribute — authoritative minimum level for feature elements.
        // BuildService.GetAdvancementTimeline reads d.Attributes.Level via dynamic dispatch.
        if (featureMinLevelMap.TryGetValue(el.Id, out int featureLevel) && featureLevel > 0)
            node.SetAttribute("level", featureLevel.ToString());

        if (supportsMap.TryGetValue(el.Id, out string? supports) && !string.IsNullOrEmpty(supports))
        {
            // Aurora XML uses a <supports> child element, not an attribute — the ElementParser
            // reads the child element text, so setting an attribute silently drops all supports.
            XmlElement supportsEl = doc.CreateElement("supports");
            supportsEl.InnerText = supports;
            node.AppendChild(supportsEl);
        }

        if (requirementMap.TryGetValue(el.Id, out string? req) && !string.IsNullOrEmpty(req))
        {
            XmlElement reqNode = doc.CreateElement("requirements");
            reqNode.InnerText = req;
            node.AppendChild(reqNode);
        }

        // Description + sheet text blocks.
        if (textsMap.TryGetValue(el.Id, out var texts))
            AppendTexts(doc, node, texts);

        // Setters: prefer explicit DB setter rows (covers all element types); fall back to
        // typed-subtype helpers for Spell and Class which store their setters in dedicated tables.
        bool hasGeneralSetters = settersMap.TryGetValue(el.Id, out var setterRows) && setterRows.Count > 0;
        bool hasTypedSetters   = !hasGeneralSetters && (spellMap.ContainsKey(el.Id) || classMap.ContainsKey(el.Id));
        if (hasGeneralSetters || hasTypedSetters)
        {
            XmlElement setters = doc.CreateElement("setters");
            if (hasGeneralSetters)
                AppendGeneralSetters(doc, setters, setterRows!);
            else if (spellMap.TryGetValue(el.Id, out var spell))
                AppendSpellSetters(doc, setters, spell);
            else if (classMap.TryGetValue(el.Id, out var cls))
                AppendClassSetters(doc, setters, cls);
            node.AppendChild(setters);
        }

        // Spellcasting profile.
        if (spellcastingMap.TryGetValue(el.Id, out var sc))
            AppendSpellcasting(doc, node, sc);

        // Multiclass block (classes only).
        if (multiclassMap.TryGetValue(el.Id, out var mc))
            AppendMulticlass(doc, node, mc,
                grantsMap, selectsMap, statsMap);

        // Rules (element-scope only; multiclass rules handled inside AppendMulticlass).
        bool hasRules =
            (grantsMap.TryGetValue(el.Id, out var grants)  && grants.Any(g => g.OwnerKind == "element")) ||
            (selectsMap.TryGetValue(el.Id, out var selects) && selects.Any(s => s.OwnerKind == "element")) ||
            (statsMap.TryGetValue(el.Id, out var stats)     && stats.Any(s => s.OwnerKind == "element"));

        if (hasRules)
        {
            XmlElement rules = doc.CreateElement("rules");
            AppendRules(doc, rules, el.Id, "element", grantsMap, selectsMap, statsMap);
            node.AppendChild(rules);
        }

        return node;
    }

    private static void AppendTexts(XmlDocument doc, XmlElement node, List<TextRow> texts)
    {
        var description = texts.Where(t => t.Kind == "description").OrderBy(t => t.Ordinal).FirstOrDefault();
        var summaryRows = texts.Where(t => t.Kind == "summary").OrderBy(t => t.Ordinal).ToList();

        if (description != null && !string.IsNullOrWhiteSpace(description.Body))
        {
            XmlElement desc = doc.CreateElement("description");
            // Body stores inner XML (e.g. "<p>text</p>") after the importer fix.
            // Use InnerXml so the HTML structure is preserved for GeneratePlainDescription.
            // Fall back to InnerText for old DB rows that stored plain text.
            try { desc.InnerXml = description.Body; }
            catch (XmlException) { desc.InnerText = description.Body; }
            // Append summary content (e.g. "At Higher Levels" for spells) into the same block.
            AppendSummaryContent(doc, desc, summaryRows);
            node.AppendChild(desc);
        }
        else if (summaryRows.Any(s => !string.IsNullOrWhiteSpace(s.Body)))
        {
            XmlElement desc = doc.CreateElement("description");
            AppendSummaryContent(doc, desc, summaryRows);
            if (desc.HasChildNodes)
                node.AppendChild(desc);
        }

        var sheetRows = texts.Where(t => t.Kind == "sheet").OrderBy(t => t.Ordinal).ToList();
        if (sheetRows.Count > 0)
        {
            XmlElement sheet = doc.CreateElement("sheet");
            var first = sheetRows[0];
            if (first.Display.HasValue && !first.Display.Value)
                sheet.SetAttribute("display", "false");
            if (!string.IsNullOrEmpty(first.AltText))
                sheet.SetAttribute("alt", first.AltText);
            if (!string.IsNullOrEmpty(first.ActionText))
                sheet.SetAttribute("action", first.ActionText);
            if (!string.IsNullOrEmpty(first.UsageText))
                sheet.SetAttribute("usage", first.UsageText);

            if (sheetRows.Count == 1 && !first.Level.HasValue)
            {
                sheet.InnerText = first.Body;
            }
            else
            {
                foreach (var row in sheetRows)
                {
                    XmlElement descChild = doc.CreateElement("description");
                    if (row.Level.HasValue)
                        descChild.SetAttribute("level", row.Level.Value.ToString());
                    descChild.InnerText = row.Body;
                    sheet.AppendChild(descChild);
                }
            }
            node.AppendChild(sheet);
        }
    }

    private static void AppendSummaryContent(XmlDocument doc, XmlElement desc, IEnumerable<TextRow> summaryRows)
    {
        foreach (var summary in summaryRows)
        {
            if (string.IsNullOrWhiteSpace(summary.Body)) continue;
            try
            {
                XmlDocumentFragment frag = doc.CreateDocumentFragment();
                frag.InnerXml = summary.Body;
                desc.AppendChild(frag);
            }
            catch (XmlException)
            {
                XmlElement p = doc.CreateElement("p");
                p.InnerText = summary.Body;
                desc.AppendChild(p);
            }
        }
    }

    private static void AppendSpellSetters(XmlDocument doc, XmlElement setters, SpellRow spell)
    {
        AppendSet(doc, setters, "level",               spell.Level.ToString());
        AppendSet(doc, setters, "school",              spell.School ?? "");
        AppendSet(doc, setters, "time",                spell.CastingTime ?? "");
        AppendSet(doc, setters, "duration",            spell.Duration ?? "");
        AppendSet(doc, setters, "range",               spell.Range ?? "");
        AppendSet(doc, setters, "hasVerbalComponent",  spell.HasVerbal.ToString().ToLower());
        AppendSet(doc, setters, "hasSomaticComponent", spell.HasSomatic.ToString().ToLower());
        AppendSet(doc, setters, "hasMaterialComponent", spell.HasMaterial.ToString().ToLower());
        AppendSet(doc, setters, "materialComponent",   spell.Material ?? "");
        AppendSet(doc, setters, "isConcentration",     spell.IsConcentration.ToString().ToLower());
        AppendSet(doc, setters, "isRitual",            spell.IsRitual.ToString().ToLower());
    }

    private static void AppendClassSetters(XmlDocument doc, XmlElement setters, ClassRow cls)
    {
        if (!string.IsNullOrEmpty(cls.HitDie))
            AppendSet(doc, setters, "hd", cls.HitDie);
        if (!string.IsNullOrEmpty(cls.ShortText))
            AppendSet(doc, setters, "short", cls.ShortText);
    }

    private static void AppendGeneralSetters(XmlDocument doc, XmlElement setters, List<SetterRow> rows)
    {
        foreach (var row in rows)
        {
            XmlElement set = doc.CreateElement("set");
            set.SetAttribute("name", row.Name);
            foreach (var (key, val) in row.Attributes)
                set.SetAttribute(key, val ?? "");
            set.InnerText = row.Value ?? "";
            setters.AppendChild(set);
        }
    }

    private static void AppendSet(XmlDocument doc, XmlElement parent, string name, string value)
    {
        XmlElement set = doc.CreateElement("set");
        set.SetAttribute("name", name);
        set.InnerText = value;
        parent.AppendChild(set);
    }

    private static void AppendSpellcasting(XmlDocument doc, XmlElement node, SpellcastingRow sc)
    {
        XmlElement el = doc.CreateElement("spellcasting");
        el.SetAttribute("name", sc.ProfileName);
        if (!string.IsNullOrEmpty(sc.Ability))
            el.SetAttribute("ability", sc.Ability);
        if (sc.IsExtended)
            el.SetAttribute("extend", "true");
        if (sc.Prepare.HasValue)
            el.SetAttribute("prepare", sc.Prepare.Value.ToString().ToLower());
        if (sc.AllowReplace.HasValue)
            el.SetAttribute("allowReplace", sc.AllowReplace.Value.ToString().ToLower());
        if (!string.IsNullOrEmpty(sc.ListText))
        {
            XmlElement list = doc.CreateElement("list");
            list.InnerText = sc.ListText;
            el.AppendChild(list);
        }
        node.AppendChild(el);
    }

    private static void AppendMulticlass(
        XmlDocument doc, XmlElement classNode, MulticlassRow mc,
        Dictionary<long, List<GrantRow>> grantsMap,
        Dictionary<long, List<SelectRow>> selectsMap,
        Dictionary<long, List<StatRow>> statsMap)
    {
        XmlElement mcNode = doc.CreateElement("multiclass");
        if (!string.IsNullOrEmpty(mc.MulticlassId))
            mcNode.SetAttribute("id", mc.MulticlassId);
        if (!string.IsNullOrEmpty(mc.Prerequisite))
        {
            XmlElement prereq = doc.CreateElement("prerequisite");
            prereq.InnerText = mc.Prerequisite;
            mcNode.AppendChild(prereq);
        }
        if (!string.IsNullOrEmpty(mc.Requirements))
        {
            XmlElement req = doc.CreateElement("requirements");
            req.InnerText = mc.Requirements;
            mcNode.AppendChild(req);
        }
        if (!string.IsNullOrEmpty(mc.Proficiencies))
        {
            XmlElement setters = doc.CreateElement("setters");
            AppendSet(doc, setters, "multiclass proficiencies", mc.Proficiencies);
            mcNode.AppendChild(setters);
        }

        bool mcHasRules =
            (grantsMap.TryGetValue(mc.ClassElementId, out var g)  && g.Any(r => r.OwnerKind == "class-multiclass")) ||
            (selectsMap.TryGetValue(mc.ClassElementId, out var s) && s.Any(r => r.OwnerKind == "class-multiclass")) ||
            (statsMap.TryGetValue(mc.ClassElementId,  out var st) && st.Any(r => r.OwnerKind == "class-multiclass"));
        if (mcHasRules)
        {
            XmlElement rules = doc.CreateElement("rules");
            AppendRules(doc, rules, mc.ClassElementId, "class-multiclass", grantsMap, selectsMap, statsMap);
            mcNode.AppendChild(rules);
        }

        classNode.AppendChild(mcNode);
    }

    private static void AppendRules(
        XmlDocument doc, XmlElement rulesNode, long elementId, string ownerKind,
        Dictionary<long, List<GrantRow>> grantsMap,
        Dictionary<long, List<SelectRow>> selectsMap,
        Dictionary<long, List<StatRow>> statsMap)
    {
        if (grantsMap.TryGetValue(elementId, out var grants))
        {
            foreach (var g in grants.Where(r => r.OwnerKind == ownerKind))
            {
                XmlElement grant = doc.CreateElement("grant");
                grant.SetAttribute("type", g.GrantType);
                if (!string.IsNullOrEmpty(g.TargetId))
                    grant.SetAttribute("id", g.TargetId);
                if (!string.IsNullOrEmpty(g.Name))
                    grant.SetAttribute("name", g.Name);
                if (g.Level.HasValue)
                    grant.SetAttribute("level", g.Level.Value.ToString());
                if (!string.IsNullOrEmpty(g.SpellcastingName))
                    grant.SetAttribute("spellcasting", g.SpellcastingName);
                if (g.IsPrepared.HasValue)
                    grant.SetAttribute("prepared", g.IsPrepared.Value ? "true" : "false");
                if (!string.IsNullOrEmpty(g.Requirements))
                    grant.SetAttribute("requirements", g.Requirements);
                rulesNode.AppendChild(grant);
            }
        }

        if (selectsMap.TryGetValue(elementId, out var selects))
        {
            foreach (var s in selects.Where(r => r.OwnerKind == ownerKind))
            {
                XmlElement sel = doc.CreateElement("select");
                sel.SetAttribute("type", s.SelectType);
                sel.SetAttribute("name", s.Name);
                if (!string.IsNullOrEmpty(s.Supports))
                    sel.SetAttribute("supports", s.Supports);
                if (s.Level.HasValue)
                    sel.SetAttribute("level", s.Level.Value.ToString());
                if (s.Number != 1)
                    sel.SetAttribute("number", s.Number.ToString());
                if (!string.IsNullOrEmpty(s.Default))
                    sel.SetAttribute("default", s.Default);
                if (s.Optional)
                    sel.SetAttribute("optional", "true");
                if (!string.IsNullOrEmpty(s.Requirements))
                    sel.SetAttribute("requirements", s.Requirements);
                rulesNode.AppendChild(sel);
            }
        }

        if (statsMap.TryGetValue(elementId, out var stats))
        {
            foreach (var st in stats.Where(r => r.OwnerKind == ownerKind))
            {
                XmlElement stat = doc.CreateElement("stat");
                stat.SetAttribute("name", st.StatName);
                if (!string.IsNullOrEmpty(st.Value))
                    stat.SetAttribute("value", st.Value);
                if (!string.IsNullOrEmpty(st.Bonus))
                    stat.SetAttribute("bonus", st.Bonus);
                if (!string.IsNullOrEmpty(st.Equipped))
                    stat.SetAttribute("equipped", st.Equipped);
                if (st.Level.HasValue)
                    stat.SetAttribute("level", st.Level.Value.ToString());
                if (st.Inline)
                    stat.SetAttribute("inline", "true");
                if (!string.IsNullOrEmpty(st.Alt))
                    stat.SetAttribute("alt", st.Alt);
                if (!string.IsNullOrEmpty(st.Requirements))
                    stat.SetAttribute("requirements", st.Requirements);
                rulesNode.AppendChild(stat);
            }
        }
    }

    // ── DB queries ───────────────────────────────────────────────────────────

    private static List<ElementRow> QueryElements(SqliteConnection conn)
    {
        var rows = new List<ElementRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT e.element_id, e.aurora_id, e.name, et.type_name, COALESCE(sb.name, '')
            FROM elements e
            JOIN element_types et ON et.element_type_id = e.element_type_id
            LEFT JOIN source_books sb ON sb.source_book_id = e.source_book_id
            ORDER BY e.loader_priority, e.element_id;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(new ElementRow(r.GetInt64(0), r.GetString(1), r.GetString(2),
                                    r.GetString(3), r.GetString(4)));
        return rows;
    }

    private static Dictionary<long, string> QuerySupports(SqliteConnection conn)
    {
        var map = new Dictionary<long, string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT element_id, group_concat(support_text, ',')
            FROM element_supports
            GROUP BY element_id;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = r.GetString(1);
        return map;
    }

    private static Dictionary<long, string> QueryRequirements(SqliteConnection conn)
    {
        // Concatenate multiple requirement rows back with && (most elements have only one row).
        var raw = new Dictionary<long, List<string>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT element_id, requirement_text
            FROM element_requirements
            ORDER BY element_id, ordinal;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            if (!raw.TryGetValue(id, out var list)) raw[id] = list = new List<string>();
            list.Add(r.GetString(1));
        }
        return raw.ToDictionary(kvp => kvp.Key, kvp => string.Join("&&", kvp.Value));
    }

    private static Dictionary<long, List<TextRow>> QueryTexts(SqliteConnection conn)
    {
        var map = new Dictionary<long, List<TextRow>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT element_id, text_kind, ordinal, level, display,
                   alt_text, action_text, usage_text, body
            FROM element_texts
            WHERE text_kind IN ('description', 'sheet', 'summary')
            ORDER BY element_id, text_kind, ordinal;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<TextRow>();
            list.Add(new TextRow(id, r.GetString(1), r.GetInt32(2),
                r.IsDBNull(3) ? null : r.GetInt32(3),
                r.IsDBNull(4) ? null : r.GetInt32(4) == 1,
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.GetString(8)));
        }
        return map;
    }

    private static Dictionary<long, List<GrantRow>> QueryGrants(SqliteConnection conn)
    {
        var map = new Dictionary<long, List<GrantRow>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT rs.owner_element_id, rs.owner_kind,
                   g.grant_type, g.target_aurora_id, g.name_text, g.grant_level,
                   g.spellcasting_name, g.is_prepared, g.requirements_text
            FROM grants g
            JOIN rule_scopes rs ON rs.rule_scope_id = g.rule_scope_id
            ORDER BY rs.owner_element_id, g.ordinal;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<GrantRow>();
            list.Add(new GrantRow(id, r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetInt32(7) == 1,
                r.IsDBNull(8) ? null : r.GetString(8)));
        }
        return map;
    }

    private static Dictionary<long, List<SelectRow>> QuerySelects(SqliteConnection conn)
    {
        var map = new Dictionary<long, List<SelectRow>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT rs.owner_element_id, rs.owner_kind,
                   s.select_type, s.name_text, s.supports_text, s.select_level,
                   s.number_to_choose, s.default_choice_text, s.is_optional, s.requirements_text
            FROM selects s
            JOIN rule_scopes rs ON rs.rule_scope_id = s.rule_scope_id
            ORDER BY rs.owner_element_id, s.ordinal;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<SelectRow>();
            list.Add(new SelectRow(id, r.GetString(1), r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5),
                r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.GetInt32(8) == 1,
                r.IsDBNull(9) ? null : r.GetString(9)));
        }
        return map;
    }

    private static Dictionary<long, List<StatRow>> QueryStats(SqliteConnection conn)
    {
        var map = new Dictionary<long, List<StatRow>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT rs.owner_element_id, rs.owner_kind,
                   st.stat_name, st.value_expression_text, st.bonus_expression_text,
                   st.equipped_expression_text, st.stat_level, st.inline_display,
                   st.alt_text, st.requirements_text
            FROM stats st
            JOIN rule_scopes rs ON rs.rule_scope_id = st.rule_scope_id
            ORDER BY rs.owner_element_id, st.ordinal;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<StatRow>();
            list.Add(new StatRow(id, r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt32(6),
                r.GetInt32(7) == 1,
                r.IsDBNull(8) ? null : r.GetString(8),
                r.IsDBNull(9) ? null : r.GetString(9)));
        }
        return map;
    }

    private static Dictionary<long, SpellcastingRow> QuerySpellcasting(SqliteConnection conn)
    {
        var map = new Dictionary<long, SpellcastingRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT owner_element_id, profile_name, ability_name,
                   is_extended, prepare_spells, allow_replace, list_text
            FROM spellcasting_profiles;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = new SpellcastingRow(
                r.GetInt64(0), r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetInt32(3) == 1,
                r.IsDBNull(4) ? null : r.GetInt32(4) == 1,
                r.IsDBNull(5) ? null : r.GetInt32(5) == 1,
                r.IsDBNull(6) ? null : r.GetString(6));
        return map;
    }

    private static Dictionary<long, SpellRow> QuerySpells(SqliteConnection conn)
    {
        var map = new Dictionary<long, SpellRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT element_id, spell_level, school_name, casting_time_text, range_text,
                   duration_text, has_verbal, has_somatic, has_material, material_text,
                   is_concentration, is_ritual
            FROM spells;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = new SpellRow(
                r.GetInt64(0), r.GetInt32(1),
                r.IsDBNull(2)  ? null : r.GetString(2),
                r.IsDBNull(3)  ? null : r.GetString(3),
                r.IsDBNull(4)  ? null : r.GetString(4),
                r.IsDBNull(5)  ? null : r.GetString(5),
                r.GetInt32(6) == 1, r.GetInt32(7) == 1, r.GetInt32(8) == 1,
                r.IsDBNull(9)  ? null : r.GetString(9),
                r.GetInt32(10) == 1, r.GetInt32(11) == 1);
        return map;
    }

    private static Dictionary<long, ClassRow> QueryClasses(SqliteConnection conn)
    {
        var map = new Dictionary<long, ClassRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT element_id, hit_die, short_text FROM classes;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = new ClassRow(
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2));
        return map;
    }

    private static Dictionary<long, MulticlassRow> QueryMulticlass(SqliteConnection conn)
    {
        var map = new Dictionary<long, MulticlassRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT class_element_id, multiclass_aurora_id,
                   prerequisite_text, requirements_text, proficiencies_text
            FROM class_multiclass;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = new MulticlassRow(
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4));
        return map;
    }

    private static Dictionary<long, List<SetterRow>> QuerySetters(SqliteConnection conn)
    {
        // Load setter entries with their extra attributes, grouped by element.
        // First pass: load all entry rows.
        var entries = new Dictionary<long, (long ElementId, string Name, string? Value, List<(string, string?)> Attrs)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT ss.owner_element_id, se.setter_entry_id, se.setter_name, se.setter_value
                FROM setter_entries se
                JOIN setter_scopes ss ON ss.setter_scope_id = se.setter_scope_id
                WHERE ss.owner_kind = 'element'
                ORDER BY ss.owner_element_id, se.ordinal;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                entries[r.GetInt64(1)] = (r.GetInt64(0), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3), new List<(string, string?)>());
        }
        // Second pass: load attributes and attach to their entry.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT setter_entry_id, attribute_name, attribute_value
                FROM setter_entry_attributes
                ORDER BY setter_entry_id, ordinal;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                long entryId = r.GetInt64(0);
                if (entries.TryGetValue(entryId, out var entry))
                    entry.Attrs.Add((r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
            }
        }
        // Group by element.
        var map = new Dictionary<long, List<SetterRow>>();
        foreach (var (entryId, (elemId, name, value, attrs)) in entries)
        {
            if (!map.TryGetValue(elemId, out var list)) map[elemId] = list = new List<SetterRow>();
            list.Add(new SetterRow(name, value, attrs));
        }
        return map;
    }

    private static Dictionary<long, int> QueryFeatureMinLevels(
        SqliteConnection conn, HashSet<string> existingTables)
    {
        var map = new Dictionary<long, int>();
        if (!existingTables.Contains("features")) return map;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT element_id, min_level
            FROM features
            WHERE min_level IS NOT NULL AND min_level > 0;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = r.GetInt32(1);
        return map;
    }

    private static Dictionary<long, string> QueryArchetypeParentIds(
        SqliteConnection conn, HashSet<string> existingTables)
    {
        // Returns archetype element_id → parent class aurora_id.
        var map = new Dictionary<long, string>();
        if (!existingTables.Contains("archetypes")) return map;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.element_id, parent.aurora_id
            FROM archetypes a
            JOIN elements parent ON parent.element_id = a.parent_class_element_id
            WHERE a.parent_class_element_id IS NOT NULL;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetInt64(0)] = r.GetString(1);
        return map;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> QuerySpellAccess(
        SqliteConnection conn, HashSet<string> existingTables)
    {
        // Returns class/list name → set of spell aurora_ids that have access.
        if (!existingTables.Contains("spell_access"))
            return new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT sa.access_text, e.aurora_id
            FROM spell_access sa
            JOIN elements e ON e.element_id = sa.spell_element_id;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string access  = r.GetString(0);
            string spellId = r.GetString(1);
            if (!map.TryGetValue(access, out var set))
                map[access] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(spellId);
        }
        return map.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlySet<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> BuildArchetypeParentMap(
        List<ElementRow> elements, Dictionary<long, string> archetypeParentIds)
    {
        // archetypeParentIds: archetype element_id → parent class aurora_id
        // Convert to: archetype aurora_id → parent class aurora_id for external use.
        var idToAuroraId = elements.ToDictionary(e => e.Id, e => e.AuroraId);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (elemId, parentAuroraId) in archetypeParentIds)
        {
            if (idToAuroraId.TryGetValue(elemId, out var archetypeAuroraId))
                result[archetypeAuroraId] = parentAuroraId;
        }
        return result;
    }

    private static int? QuerySchemaVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        object? raw = cmd.ExecuteScalar();
        return raw is long longValue
            ? (int)longValue
            : raw is int intValue
                ? intValue
                : null;
    }

    private static HashSet<string> QueryExistingTables(SqliteConnection conn)
    {
        HashSet<string> tables = new(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT name
            FROM sqlite_master
            WHERE type = 'table';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static List<string> QueryMissingColumns(SqliteConnection conn)
    {
        List<string> missing = [];
        foreach ((string table, string[] expectedColumns) in RequiredColumns)
        {
            HashSet<string> actualColumns = QueryTableColumns(conn, table);
            foreach (string column in expectedColumns)
            {
                if (!actualColumns.Contains(column))
                    missing.Add($"{table}.{column}");
            }
        }

        return missing
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> QueryTableColumns(SqliteConnection conn, string tableName)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }
}
