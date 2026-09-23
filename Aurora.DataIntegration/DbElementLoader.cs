using Builder.Presentation.Services;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using Aurora.Content.Preparation;
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
/// the existing <see cref="ElementParser"/> pipeline exactly as DataManager does. Then overlay
/// local custom/user XML before calling <see cref="DataManager.RunPostProcessing"/> so local
/// scratch/homebrew content works without rebuilding the SQLite database.
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
    int? DataVersion,
    string? ImporterVersion,
    string? BuiltUtc,
    int? SourceFileCount,
    string? ContentRootHash,
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
                string schema = SchemaVersion.HasValue ? $"schema v{SchemaVersion.Value}" : "schema version unknown";
                string data = DataVersion.HasValue ? $", data v{DataVersion.Value}" : string.Empty;
                return $"Loaded {ElementCount} elements from SQLite ({schema}{data}{skipped}).";
            }

            if (MissingTables.Count > 0)
                return $"SQLite load unavailable: missing tables {string.Join(", ", MissingTables)}.";

            if (MissingColumns.Count > 0)
                return $"SQLite load unavailable: missing columns {string.Join(", ", MissingColumns)}.";

            return $"SQLite load unavailable: {FailureReason ?? "unknown reason"}";
        }
    }

    public static DbLoadResult NotAvailable(string? databasePath, string reason) =>
        new(false, databasePath, 0, 0, null, null, null, null, null, null, reason, [], []);

    public static DbLoadResult InvalidSchema(
        string databasePath,
        int? schemaVersion,
        IReadOnlyList<string> missingTables,
        IReadOnlyList<string> missingColumns) =>
        new(false, databasePath, 0, 0, schemaVersion,
            null,
            null,
            null,
            null,
            null,
            missingTables.Count > 0
                ? $"Database schema is missing required tables: {string.Join(", ", missingTables)}."
                : $"Database schema is missing required columns: {string.Join(", ", missingColumns)}.",
            missingTables,
            missingColumns);

    public static DbLoadResult Failed(string databasePath, int? schemaVersion, string reason) =>
        new(false, databasePath, 0, 0, schemaVersion, null, null, null, null, null, reason, [], []);

    public static DbLoadResult Loaded(
        string databasePath,
        int? schemaVersion,
        int? dataVersion,
        string? importerVersion,
        string? builtUtc,
        int? sourceFileCount,
        string? contentRootHash,
        int elementCount,
        int skippedElementCount) =>
        new(true, databasePath, elementCount, skippedElementCount, schemaVersion, dataVersion, importerVersion, builtUtc, sourceFileCount, contentRootHash, null, [], []);
}

internal static class DbElementLoader
{
    /// <summary>Schema and data version the shared library writes; anything older is rebuilt.</summary>
    private const int PreparedSchemaVersion = 1;
    private const int PreparedDataVersion = 12;

    private static readonly string[] RequiredTables =
    [
        "elements",
        "element_types",
        "source_books",
        "source_files",
        "source_elements",
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
        "database_metadata",
        "setter_scopes",
        "setter_entries",
        "setter_entry_attributes"
    ];
    private static readonly IReadOnlyDictionary<string, string[]> RequiredColumns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["elements"] = ["element_id", "aurora_id", "name", "element_type_id", "source_book_id", "source_file_id", "loader_priority"],
            ["element_types"] = ["element_type_id", "type_name"],
            ["source_books"] = ["source_book_id", "name"],
            ["source_files"] = ["source_file_id", "relative_path"],
            ["source_elements"] = ["element_id", "release_text"],
            ["element_supports"] = ["element_id", "support_text"],
            ["element_requirements"] = ["element_id", "requirement_text", "ordinal"],
            ["element_texts"] = ["element_id", "text_kind", "ordinal", "level", "display", "alt_text", "action_text", "usage_text", "body"],
            ["grants"] = ["rule_scope_id", "grant_type", "target_aurora_id", "name_text", "grant_level", "spellcasting_name", "is_prepared", "requirements_text", "ordinal"],
            ["rule_scopes"] = ["rule_scope_id", "owner_element_id", "owner_kind"],
            ["selects"] = ["rule_scope_id", "select_type", "name_text", "supports_text", "select_level", "number_to_choose", "default_choice_text", "is_optional", "spellcasting_profile_id", "raw_xml", "requirements_text", "ordinal"],
            ["stats"] = ["rule_scope_id", "stat_name", "value_expression_text", "bonus_expression_text", "equipped_expression_text", "stat_level", "inline_display", "alt_text", "requirements_text", "ordinal"],
            ["spellcasting_profiles"] = ["owner_element_id", "profile_name", "ability_name", "is_extended", "prepare_spells", "allow_replace", "list_text", "extend_text"],
            ["spells"] = ["element_id", "spell_level", "school_name", "casting_time_text", "range_text", "duration_text", "has_verbal", "has_somatic", "has_material", "material_text", "is_concentration", "is_ritual"],
            ["classes"] = ["element_id", "hit_die", "short_text"],
            ["class_multiclass"] = ["class_element_id", "multiclass_aurora_id", "prerequisite_text", "requirements_text", "proficiencies_text"],
            ["database_metadata"] = ["singleton_id", "schema_version", "data_version", "importer_version", "built_utc", "source_file_count", "element_count", "content_root_hash"],
            ["setter_scopes"] = ["setter_scope_id", "owner_element_id", "owner_kind"],
            ["setter_entries"] = ["setter_entry_id", "setter_scope_id", "ordinal", "setter_name", "setter_value"],
            ["setter_entry_attributes"] = ["setter_entry_id", "ordinal", "attribute_name", "attribute_value"]
        };

    // ── Runtime lookup caches (populated after a successful DB load) ────────

    /// <summary>Archetype aurora_id → parent class aurora_id. Empty until a DB load succeeds.</summary>
    private sealed class LookupState
    {
        public IReadOnlyDictionary<string, string> Archetypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, IReadOnlySet<string>> Spells = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, ElementSortMetadata> Sort = new Dictionary<string, ElementSortMetadata>(StringComparer.OrdinalIgnoreCase);
        public Action? PublishFallback;
    }
    private static LookupState _lookups = new();
    private static readonly AsyncLocal<LookupState?> PendingLookups = new();
    public static IReadOnlyDictionary<string, string> ArchetypeParentMap
    {
        get => (PendingLookups.Value ?? _lookups).Archetypes;
        private set => (PendingLookups.Value ?? _lookups).Archetypes = value;
    }

    /// <summary>Class/list name → set of spell aurora_ids that have access. Empty until a DB load succeeds.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> SpellAccessMap
    {
        get => (PendingLookups.Value ?? _lookups).Spells;
        private set => (PendingLookups.Value ?? _lookups).Spells = value;
    }

    /// <summary>Picker ordering metadata keyed by Aurora element id + source name. Empty when XML fallback is active.</summary>
    public static IReadOnlyDictionary<string, ElementSortMetadata> ElementSortMetadataMap
    {
        get => (PendingLookups.Value ?? _lookups).Sort;
        private set => (PendingLookups.Value ?? _lookups).Sort = value;
    }

    public static string? DbPath => ContentDatabaseService.GetDatabasePath();

    public static bool IsAvailable =>
        DbPath is { } path && File.Exists(path) && new FileInfo(path).Length > 0;

    public static void ResetCaches()
    {
        ArchetypeParentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SpellAccessMap = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        ElementSortMetadataMap = new Dictionary<string, ElementSortMetadata>(StringComparer.OrdinalIgnoreCase);
    }

    public static string MakeElementSortMetadataKey(string? auroraId, string? sourceName) =>
        $"{auroraId ?? string.Empty}\u001f{sourceName ?? string.Empty}";

    /// <summary>
    /// Returns the set of source book names that appear in the resolved elements cache,
    /// i.e. only names from enabled content packages. Returns an empty set if the DB is
    /// unavailable or the query fails.
    /// </summary>
    public static async Task<HashSet<string>> LoadEnabledSourceNamesAsync()
    {
        string? dbPath = DbPath;
        if (dbPath is null || !File.Exists(dbPath))
            return [];

        return await Task.Run(() =>
        {
            try
            {
                using var conn = ContentDatabase.OpenReadableConnection(dbPath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT sb.name
                    FROM source_books sb
                    JOIN elements e ON e.source_book_id = sb.source_book_id
                    JOIN resolved_elements_cache rec ON rec.winning_element_id = e.element_id
                    WHERE sb.name IS NOT NULL AND sb.name <> '';";
                using var r = cmd.ExecuteReader();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (r.Read())
                    names.Add(r.GetString(0));
                return names;
            }
            catch
            {
                return [];
            }
        });
    }

    /// <summary>
    /// Attempts to populate <paramref name="target"/> from the SQLite database.
    /// Returns a detailed result object; callers can automatically fall back to XML when
    /// <see cref="DbLoadResult.Success"/> is <c>false</c>.
    /// </summary>
    public static async Task<DbLoadResult> TryLoadAsync(ElementBaseCollection target)
    {
        return await TryLoadInternalAsync(target, runPostProcessing: true);
    }

    public static async Task<DbLoadResult> TryLoadSnapshotAsync(ElementBaseCollection target)
    {
        return await TryLoadInternalAsync(target, runPostProcessing: false);
    }

    private static async Task<DbLoadResult> TryLoadInternalAsync(ElementBaseCollection target, bool runPostProcessing)
    {
        string? dbPath = DbPath;
        if (dbPath is null)
            return DbLoadResult.NotAvailable(null, "Content database path is not initialized.");

        if (!File.Exists(dbPath))
            return DbLoadResult.NotAvailable(dbPath, "Database file was not found.");

        if (new FileInfo(dbPath).Length <= 0)
            return DbLoadResult.NotAvailable(dbPath, "Database file is empty.");

        var previousLookups = _lookups;
        PendingLookups.Value = new LookupState();
        var previousElements = target.ToList();
        var restoreFallback = XmlContentFallbackService.CaptureRestore();
        bool committed = false;
        bool replacingTarget = false;
        try
        {
            DebugLogService.Instance.Info("DbElementLoader: loading elements from DB.", dbPath);
            var candidate = new ElementBaseCollection();
            DbLoadResult result = await Task.Run(() => LoadFromDb(dbPath, candidate));
            if (result.Success && runPostProcessing)
            {
                if (result.DataVersion != 12)
                {
                    await Task.Run(() => RawUserXmlOverlayService.ApplyTo(candidate));
                    await Task.Run(() => XmlContentFallbackService.MergeUnsynced(candidate));
                }
                await Task.Run(() => DataManager.Current.RunPostProcessing(candidate, includeResources: result.DataVersion != 12, publish: false));
                DebugLogService.Instance.Info(
                    "DbElementLoader: load complete.",
                    result.Summary);
            }
            else if (!result.Success)
            {
                DebugLogService.Instance.Warn("DbElementLoader: falling back to XML.", result.Summary);
            }
            if (result.Success)
            {
                replacingTarget = true;
                target.Clear();
                target.AddRange(candidate);
                committed = true;
                if (runPostProcessing)
                {
                    _lookups = PendingLookups.Value!;
                    if (_lookups.PublishFallback is { } publishFallback)
                    {
                        publishFallback();
                        _lookups.PublishFallback = null;
                    }
                    else XmlContentFallbackService.Invalidate();
                    DataManager.Current.NotifyElementsLoaded();
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "DbElementLoader.TryLoadAsync");
            if (committed)
            {
                _lookups = previousLookups;
                restoreFallback();
            }
            if (replacingTarget)
            {
                target.Clear();
                target.AddRange(previousElements);
            }
            return DbLoadResult.Failed(
                dbPath,
                schemaVersion: null,
                reason: $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            PendingLookups.Value = null;
        }
    }

    // ── Raw DB rows ──────────────────────────────────────────────────────────

    public sealed record ElementSortMetadata(
        DateTimeOffset? SourceReleaseDate,
        DateTimeOffset? SourceFileModifiedUtc,
        string? SourceReleaseText);

    private record ElementRow(
        long Id,
        string AuroraId,
        string Name,
        string TypeName,
        string Source,
        string SourceFileRelativePath,
        string? SourceReleaseText);
    private record TextRow(long ElementId, string Kind, int Ordinal, int? Level, bool? Display,
        string? AltText, string? ActionText, string? UsageText, string Body);
    private record GrantRow(long ElementId, string OwnerKind, string GrantType,
        string? TargetId, string? Name, int? Level, string? SpellcastingName, bool? IsPrepared, string? Requirements);
    private record SelectRow(long ElementId, string OwnerKind, string SelectType, string Name,
        string? Supports, int? Level, int Number, string? Default, bool Optional,
        string? SpellcastingName, string? RawXml, string? Requirements);
    private record StatRow(long ElementId, string OwnerKind, string StatName, string? Value,
        string? Bonus, string? Equipped, int? Level, bool Inline, string? Alt, string? Requirements);
    private record SpellcastingRow(long ElementId, string ProfileName, string? Ability,
        bool IsExtended, bool? Prepare, bool? AllowReplace, string? ListText, string? ExtendText,
        XmlElement? RawProfile = null);
    private record SpellRow(long ElementId, int Level, string? School, string? CastingTime,
        string? Range, string? Duration, bool HasVerbal, bool HasSomatic, bool HasMaterial,
        string? Material, bool IsConcentration, bool IsRitual);
    private record ClassRow(long ElementId, string? HitDie, string? ShortText);
    private record MulticlassRow(long ClassElementId, string? MulticlassId,
        string? Prerequisite, string? Requirements, string? Proficiencies);
    private record SetterRow(string Name, string? Value, IReadOnlyList<(string Key, string? Val)> Attributes);
    private sealed record MetadataRow(
        int SchemaVersion,
        int DataVersion,
        string ImporterVersion,
        string BuiltUtc,
        int SourceFileCount,
        int ElementCount,
        string? ContentRootHash);

    // ── Main loader ──────────────────────────────────────────────────────────

    private static DbLoadResult LoadFromDb(string dbPath, ElementBaseCollection target)
    {
        using var conn = ContentDatabase.OpenReadableConnection(dbPath);

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

        MetadataRow? metadata = QueryMetadata(conn);
        if (metadata is null)
            return DbLoadResult.Failed(
                dbPath,
                schemaVersion,
                "Database metadata is missing. Sync the content database again before using SQLite loading.");

        if (metadata.SchemaVersion != PreparedSchemaVersion)
        {
            return DbLoadResult.Failed(
                dbPath,
                metadata.SchemaVersion,
                $"Database metadata schema v{metadata.SchemaVersion} is incompatible with expected schema v{PreparedSchemaVersion}. Re-sync the content database.");
        }

        // The app builds its own database through the shared library. An older one is rebuilt by a
        // refresh rather than read: staleness reports it, and reading it would mean keeping a second
        // reader for a format nothing writes any more.
        if (metadata.DataVersion != PreparedDataVersion)
        {
            return DbLoadResult.Failed(
                dbPath,
                metadata.SchemaVersion,
                $"Database data version v{metadata.DataVersion} predates the current content format (v{PreparedDataVersion}). Refresh the content database.");
        }

        return LoadPreparedCatalog(conn, dbPath, metadata, target);
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

    private static void AppendSet(XmlDocument doc, XmlElement parent, string name, string value)
    {
        XmlElement set = doc.CreateElement("set");
        set.SetAttribute("name", name);
        set.InnerText = value;
        parent.AppendChild(set);
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
                XmlElement sel = CreateRuleElementFromRawXml(doc, s.RawXml, "select")
                    ?? doc.CreateElement("select");
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
                if (!string.IsNullOrEmpty(s.SpellcastingName))
                    sel.SetAttribute("spellcasting", s.SpellcastingName);
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

    private static XmlElement? CreateRuleElementFromRawXml(XmlDocument doc, string? rawXml, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return null;

        try
        {
            XmlDocument rawDocument = new();
            rawDocument.LoadXml(rawXml);
            if (rawDocument.DocumentElement == null ||
                !rawDocument.DocumentElement.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                return null;

            return (XmlElement)doc.ImportNode(rawDocument.DocumentElement, deep: true);
        }
        catch
        {
            return null;
        }
    }

    // ── DB queries ───────────────────────────────────────────────────────────

    private static List<ElementRow> QueryElements(SqliteConnection conn)
    {
        var rows = new List<ElementRow>();
        IReadOnlyDictionary<string, string> sourceReleases = QuerySourceReleases(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                e.element_id,
                e.aurora_id,
                e.name,
                et.type_name,
                COALESCE(sb.name, ''),
                COALESCE(sf.relative_path, '')
            FROM resolved_elements_cache rec
            JOIN elements e ON e.element_id = rec.winning_element_id
            JOIN element_types et ON et.element_type_id = e.element_type_id
            LEFT JOIN source_books sb ON sb.source_book_id = e.source_book_id
            LEFT JOIN source_files sf ON sf.source_file_id = e.source_file_id
            ORDER BY rec.resolution_rank, e.loader_priority, e.element_id;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string source = r.GetString(4);
            rows.Add(new ElementRow(r.GetInt64(0), r.GetString(1), r.GetString(2),
                                    r.GetString(3), source, r.GetString(5),
                                    sourceReleases.GetValueOrDefault(source)));
        }
        return rows;
    }

    private static IReadOnlyDictionary<string, string> QuerySourceReleases(SqliteConnection conn)
    {
        var releasesBySource = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT src.name, se.release_text
            FROM source_elements se
            JOIN elements src ON src.element_id = se.element_id
            JOIN element_types src_type ON src_type.element_type_id = src.element_type_id
            WHERE src_type.type_name = 'Source'
              AND COALESCE(trim(se.release_text), '') <> ''
            ORDER BY se.element_id;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string source = reader.GetString(0);
            if (!releasesBySource.TryGetValue(source, out List<string>? releases))
                releasesBySource[source] = releases = [];
            releases.Add(reader.GetString(1));
        }

        return releasesBySource.ToDictionary(
            pair => pair.Key,
            pair => SourceReleaseTextSelector.SelectLatest(pair.Value)!,
            StringComparer.OrdinalIgnoreCase);
    }

    internal static PreparedCatalogProjection ReadPreparedProjection(SqliteConnection connection, bool fromXml = false)
    {
        var resources = DataManager.Current.LoadElementDocumentsFromResource()
            .SelectMany(doc => doc.DocumentElement?.ChildNodes.Cast<XmlNode>() ?? [])
            .Where(node => node.Name == "element" && node.Attributes?["id"] != null)
            .Select(node => new PreparedCatalogElement(node.Attributes!["id"]!.Value,
                new PreparedCatalogSource("resource://aurora/builtins", "runtime/builtins.xml", "runtime-builtins", "core"), node.OuterXml))
            .ToArray();
        var roots = ContentDirectoryResolver.GetContentDirectories();
        var runtimeFiles = RuntimeContentFiles.Read(connection, ContentDirectoryResolver.GetPrimaryContentDirectory(), roots.Skip(1), fromXml);
        // The whole catalog loads. What a character may use is decided at runtime by its source
        // restrictions, so content is never missing from the projection the engine reasons over.
        return PreparedCatalogReader.Read(connection, hostDefinitions: resources, runtimeFiles: runtimeFiles);
    }

    private static DbLoadResult LoadPreparedCatalog(SqliteConnection connection, string path,
        MetadataRow metadata, ElementBaseCollection target)
    {
        if (!PreparedCatalogReader.IsPrepared(connection))
            return DbLoadResult.Failed(path, metadata.SchemaVersion,
                "The database preparation contract is missing or unsupported.");
        var projection = ReadPreparedProjection(connection);
        var unknownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var diagnostics = connection.CreateCommand())
        {
            diagnostics.CommandText = "SELECT type_name FROM element_types WHERE loader_family='generic-unrecognized'";
            using var reader = diagnostics.ExecuteReader();
            while (reader.Read()) unknownTypes.Add(reader.GetString(0));
        }
        var parsers = ElementParserFactory.GetParsers().ToList();
        var fallback = new ElementParser();
        var parsed = new List<ElementBase>();
        foreach (var prepared in projection.Elements)
        {
            var doc = new XmlDocument();
            doc.LoadXml("<elements>" + prepared.Xml + "</elements>");
            XmlNode node = doc.DocumentElement!.FirstChild!;
            AuroraXmlCompatibilityRepair.RepairNode(node);
            var header = fallback.ParseElementHeader(node);
            var parser = parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? fallback;
            if (!prepared.Source.FilePath.StartsWith("resource://", StringComparison.Ordinal) && unknownTypes.Remove(header.Type))
                DebugLogService.Instance.Warn($"Prepared element uses generic parsing: {header.Id} ({header.Type}, {prepared.Source.RelativePath}). Shared content is retained; specialized behavior is not inferred.");
            var element = parser.ParseElement(node);
            ElementProvenance.SetContentFilePath(element, prepared.Source.FilePath);
            parsed.Add(element);
        }
        // Complete reconstruction only: an exception leaves the previous target
        // untouched and invokes the caller's complete XML fallback.
        var ids = parsed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var rows = QueryElements(connection).Where(e => ids.Contains(e.AuroraId)).ToList();
        // Runtime supports/grants remain intact. Do not expose a singular parent
        // from an unrestricted database as the answer for a filtered projection.
        ArchetypeParentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SpellAccessMap = parsed.Where(e => string.Equals(e.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => ContentText.SplitTopLevel(e.ElementNode["supports"]?.InnerText ?? "", ',')
                .Select(support => (Support: support, Id: e.Id)))
            .GroupBy(e => e.Support, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(e => e.Id).ToHashSet(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        ElementSortMetadataMap = BuildElementSortMetadataMap(rows);
        PendingLookups.Value!.PublishFallback = XmlContentFallbackService.PrepareProjection(parsed);
        target.Clear();
        target.AddRange(parsed);
        foreach (var operation in projection.UnresolvedAppends)
            DebugLogService.Instance.Warn($"Prepared append target remains unresolved: {operation.TargetId} ({operation.Source.RelativePath}, append {operation.Ordinal}).");
        return DbLoadResult.Loaded(path, metadata.SchemaVersion, metadata.DataVersion,
            metadata.ImporterVersion, metadata.BuiltUtc, metadata.SourceFileCount,
            metadata.ContentRootHash, target.Count, 0);
    }

    private static IReadOnlyDictionary<string, ElementSortMetadata> BuildElementSortMetadataMap(
        IReadOnlyList<ElementRow> elements)
    {
        var map = new Dictionary<string, ElementSortMetadata>(StringComparer.OrdinalIgnoreCase);
        string contentRoot = ContentDatabaseService.GetContentDirectory();

        foreach (var element in elements)
        {
            DateTimeOffset? fileModifiedUtc = TryGetSourceFileModifiedUtc(contentRoot, element.SourceFileRelativePath);
            map[MakeElementSortMetadataKey(element.AuroraId, element.Source)] = new ElementSortMetadata(
                SourceReleaseDate: TryParseSourceReleaseDate(element.SourceReleaseText),
                SourceFileModifiedUtc: fileModifiedUtc,
                SourceReleaseText: element.SourceReleaseText);
        }

        return map;
    }

    private static DateTimeOffset? TryGetSourceFileModifiedUtc(string contentRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(contentRoot) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                                    .Replace('\\', Path.DirectorySeparatorChar);
            string path = Path.Combine(contentRoot, normalizedRelative);
            return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseSourceReleaseDate(string? releaseText)
        => SourceReleaseTextSelector.TryParseDate(releaseText);

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

    private static MetadataRow? QueryMetadata(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT schema_version,
                   data_version,
                   importer_version,
                   built_utc,
                   source_file_count,
                   element_count,
                   content_root_hash
            FROM database_metadata
            WHERE singleton_id = 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new MetadataRow(
            SchemaVersion: reader.GetInt32(0),
            DataVersion: reader.GetInt32(1),
            ImporterVersion: reader.GetString(2),
            BuiltUtc: reader.GetString(3),
            SourceFileCount: reader.GetInt32(4),
            ElementCount: reader.GetInt32(5),
            ContentRootHash: reader.IsDBNull(6) ? null : reader.GetString(6));
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
