using Aurora.Content.Preparation;
using Aurora.PdfImport;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Reflections.Services;

/// <summary>
/// Thin service wrapper around Aurora.PdfImport for use from Blazor pages.
/// On Windows, PDF parsing uses Windows.Media.Ocr for accurate extraction of
/// D&amp;D Beyond's graphics-rendered character values.
/// </summary>
public sealed class PdfImportService
{
    private readonly ContentDatabaseService _db;

    public PdfImportService(ContentDatabaseService db) => _db = db;

    // ── PDF import ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a D&amp;D Beyond PDF via Windows OCR (Windows) or PdfPig text extraction
    /// (other platforms), then resolves elements against the content database.
    /// Returns null if the database is not available.
    /// </summary>
    public async Task<(ParsedCharacterSheet Sheet, ImportResult Result)?> ImportAsync(string pdfPath)
    {
        string? dbPath = _db.DatabasePath;
        if (dbPath == null || !File.Exists(dbPath)) return null;

        ParsedCharacterSheet sheet;

#if WINDOWS
        // Windows: render each page to a bitmap and run Windows OCR so we can
        // read character values that D&D Beyond renders as non-extractable graphics.
        IReadOnlyList<Aurora.PdfImport.OcrPage> ocrPages = await WindowsPdfOcrService.RenderAndOcrAsync(pdfPath);
        sheet = await Task.Run(() => DndBeyondPdfParser.ParseFromOcr(ocrPages));
#else
        // Other platforms: fall back to PdfPig text extraction.
        // NOTE: D&D Beyond PDFs render character values as graphics; this path
        // will only populate template-label fields on the current sheet format.
        sheet = await Task.Run(() => DndBeyondPdfParser.Parse(pdfPath));
#endif

        var result = await Task.Run(() =>
        {
            var engine = CreateInferenceEngine(dbPath);
            return engine.Infer(sheet);
        });

        sheet.StagedPortrait = await TryExtractAndSavePdfPortraitAsync(pdfPath);
        sheet.PortraitLocalPath = sheet.StagedPortrait?.Path;

        return (sheet, result);
    }

    // ── JSON import ───────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a D&amp;D Beyond character export JSON file and resolves elements
    /// against the content database.
    /// Returns null if the database is not available.
    /// </summary>
    public async Task<(ParsedCharacterSheet Sheet, ImportResult Result)?> ImportFromJsonAsync(
        string jsonPath)
    {
        string? dbPath = _db.DatabasePath;
        if (dbPath == null || !File.Exists(dbPath)) return null;

        var (sheet, result) = await Task.Run(() =>
        {
            var s  = DndBeyondJsonImporter.Parse(jsonPath);
            var engine = CreateInferenceEngine(dbPath);
            return (s, engine.Infer(s));
        });

        // Try to download the portrait; failure is non-fatal — falls back to placeholder.
        if (!string.IsNullOrWhiteSpace(sheet.PortraitUrl))
            sheet.StagedPortrait = await TryDownloadPortraitAsync(sheet.PortraitUrl);
        sheet.PortraitLocalPath = sheet.StagedPortrait?.Path;

        return (sheet, result);
    }

    internal static CharacterInferenceEngine CreateInferenceEngine(string dbPath)
    {
        using var connection = ContentDatabase.OpenReadableConnection(dbPath);
        var projection = DbElementLoader.ReadPreparedProjection(connection);
        var packages = new Dictionary<string, (string Name, int Rank)>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT package_key, package_name, precedence_rank FROM content_packages";
            using var reader = command.ExecuteReader();
            while (reader.Read()) packages[reader.GetString(0)] = (reader.GetString(1), reader.GetInt32(2));
        }
        return new CharacterInferenceEngine(dbPath, projection.Elements.Select(element =>
        {
            var package = packages.GetValueOrDefault(element.Source.PackageKey, ("Bundled content", int.MinValue));
            return new InferenceCatalogElement(element.Xml, package.Item1, element.Source.PackageKind, package.Item2);
        }));
    }

    private static async Task<StagedImportPortrait?> TryDownloadPortraitAsync(string url)
    {
        try
        {
            string ext = Path.GetExtension(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            byte[] data = await http.GetByteArrayAsync(url);
            return await StagedImportPortrait.CreateAsync(data, ext);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<StagedImportPortrait?> TryExtractAndSavePdfPortraitAsync(string pdfPath)
    {
        try
        {
            var portrait = await Task.Run(() => DndBeyondPdfParser.TryExtractPortrait(pdfPath));
            if (portrait == null) return null;
            return await StagedImportPortrait.CreateAsync(portrait.Value.Bytes, portrait.Value.Extension);
        }
        catch
        {
            return null;
        }
    }

    // ── File writing ──────────────────────────────────────────────────────────

    /// <summary>Writes the import result to a .dnd5e file at <paramref name="outputPath"/>.</summary>
    public async Task WriteCharacterFileAsync(
        ParsedCharacterSheet sheet,
        ImportResult result,
        string outputPath)
    {
        CharacterImportSupport.EnsureSupported(sheet);
        using var scope = await CharacterContext.EnterForLoadAsync();
        string fullOutputPath = Path.GetFullPath(outputPath);
        string temporaryPath = Path.Combine(Path.GetDirectoryName(fullOutputPath)!,
            $".aurora-import-{Guid.NewGuid():N}.dnd5e");
        string? previewPath = sheet.PortraitLocalPath;
        try
        {
            if (sheet.StagedPortrait is { } portrait)
                sheet.PortraitLocalPath = portrait.CopyForCommit(
                    DataManager.Current.UserDocumentsPortraitsDirectory, sheet.CharacterName);

            await BuildAndSaveCharacterFileAsync(sheet, result, temporaryPath);
            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            sheet.StagedPortrait?.AcceptCommit();
            sheet.StagedPortrait = null;
        }
        catch
        {
            sheet.StagedPortrait?.RollbackCommit();
            sheet.PortraitLocalPath = previewPath;
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task BuildAndSaveCharacterFileAsync(
        ParsedCharacterSheet sheet,
        ImportResult result,
        string outputPath)
    {
        result.ImportDiagnostics.Clear();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);

        List<ResolvedElement> resolved = GetResolvedElements(result);
        List<PendingImportSelection> pending = resolved
            .Where(e => e.TypeName is not ("Level" or "Multiclass"))
            .Select(e => new PendingImportSelection(e, ResolveOwnerClassId(e.OwnerClass, resolved)))
            .ToList();

        RequireSelection(pending, "Race");
        RequireSelection(pending, "Class");
        RequireSelection(pending, "Background", required: false);

        ApplyPendingSelections(pending, cm.Character.Level);

        int mainTarget = result.Classes.Count > 0 ? result.Classes[0].Level : Math.Max(1, result.Level);
        while (cm.Character.Level < mainTarget)
        {
            int before = cm.Character.Level;
            cm.LevelUpMain();
            if (cm.Character.Level != before + 1)
                throw new InvalidOperationException("The imported main class could not gain its requested level.");
            try { cm.ReprocessCharacter(); }
            catch (Exception ex)
            {
                result.ImportDiagnostics.Add(
                    $"Warning: processing threw during level-up to {cm.Character.Level} — {ex.GetType().Name}: {ex.Message}");
            }
            ApplyPendingSelections(pending, cm.Character.Level);
        }

        if (result.Classes.Count > 1)
        {
            var option = DataManager.Current.ElementsCollection.GetElement(Builder.Data.Strings.InternalOptions.AllowMulticlassing)
                ?? throw new InvalidOperationException("Multiclassing core content is missing. Refresh content before importing.");
            cm.RegisterElement(option);
            foreach (var importedClass in result.Classes.Skip(1))
            {
                var choice = resolved.SingleOrDefault(element => element.TypeName == "Multiclass"
                    && string.Equals(element.OwnerClass, importedClass.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Resolve the Multiclass selection for {importedClass.Name} before importing.");
                int before = cm.Character.Level;
                cm.NewMulticlass();
                if (cm.Character.Level != before + 1)
                    throw new InvalidOperationException($"Could not open a multiclass level for {importedClass.Name}.");
                var selection = new PendingImportSelection(choice);
                if (!TryApplySelection(selection))
                    throw new InvalidOperationException($"Could not apply the imported multiclass {importedClass.Name}.");
                cm.ReprocessCharacter();
                var progression = cm.ClassProgressionManagers.SingleOrDefault(manager => manager.IsMulticlass
                    && manager.ClassElement?.Id == choice.AuroraId)
                    ?? throw new InvalidOperationException($"The {importedClass.Name} progression was not created.");
                ApplyPendingSelections(pending, cm.Character.Level);
                while (progression.ProgressionLevel < importedClass.Level)
                {
                    before = cm.Character.Level;
                    int classBefore = progression.ProgressionLevel;
                    cm.LevelUpMulti((Multiclass)progression.ClassElement);
                    if (cm.Character.Level != before + 1 || progression.ProgressionLevel != classBefore + 1)
                        throw new InvalidOperationException($"Could not add the requested level of {importedClass.Name}.");
                    cm.ReprocessCharacter();
                    ApplyPendingSelections(pending, cm.Character.Level);
                }
            }
        }

        ApplyPendingSelections(pending, cm.Character.Level, includeDeferred: true);

        foreach (var importedClass in result.Classes.Where(importedClass => !string.IsNullOrWhiteSpace(importedClass.Subclass)))
        {
            if (!pending.Any(selection => selection.Element.TypeName == "Archetype" && selection.Applied
                    && string.Equals(selection.Element.OwnerClass, importedClass.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"The subclass {importedClass.Subclass} could not be applied to {importedClass.Name}. Resolve its content match before importing.");
        }
        if (cm.Character.Level != Math.Max(1, result.Level))
            throw new InvalidOperationException("The imported class levels do not match the export. No character file was replaced.");

        ApplyEditableFields(cm.Character, sheet, result);
        HarmonizeBaseAbilityScores(cm.Character, result);
        cm.ReprocessCharacter();
        ApplyPreparedSpellState(sheet, resolved, result);
        AppendImportDiagnostics(result, pending, sheet, resolved);

        var file = new CharacterFile(outputPath);
        if (!file.Save(cm.Character))
            throw new IOException("The imported character could not be written.");

        var snapshot = CharacterSnapshot.From(cm.Character);
        OverrideSnapshotFields(snapshot, sheet, result);
        if (!file.SaveTextEdits(snapshot))
            throw new InvalidOperationException("Imported character file was saved, but snapshot-backed field patching failed.");
    }

    private static List<ResolvedElement> GetResolvedElements(ImportResult result)
    {
        var resolved = new List<ResolvedElement>(result.Elements);
        resolved.AddRange(result.Ambiguities
            .Where(a => a.Chosen != null)
            .Select(a => a.Chosen!));
        return resolved;
    }

    private static void RequireSelection(
        List<PendingImportSelection> pending,
        string typeName,
        bool required = true)
    {
        PendingImportSelection? selection = pending
            .FirstOrDefault(p => p.MatchesType(typeName));

        if (selection == null)
        {
            if (required)
                throw new InvalidOperationException($"The imported character could not resolve a required {typeName}.");
            return;
        }

        if (!TryApplySelection(selection))
        {
            if (required)
                throw new InvalidOperationException($"The imported {typeName} could not be applied to a new Aurora character.");
            return;
        }

        selection.Applied = true;
        CharacterManager.Current.ReprocessCharacter();
    }

    private static void ApplyPendingSelections(
        List<PendingImportSelection> pending,
        int currentLevel,
        bool includeDeferred = false)
    {
        bool progressed;
        do
        {
            progressed = false;

            foreach (PendingImportSelection selection in pending
                .Where(p => !p.Applied)
                .Where(p => includeDeferred || p.Element.Level <= currentLevel)
                .OrderBy(p => GetSelectionPriority(p.Element.TypeName))
                .ThenBy(p => p.Element.Level)
                .ThenBy(p => p.Element.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!ShouldAttemptSelection(selection, out string? skipReason))
                {
                    selection.LastFailureReason ??= skipReason;
                    continue;
                }

                if (!TryApplySelection(selection))
                    continue;

                selection.Applied = true;
                progressed = true;
                CharacterManager.Current.ReprocessCharacter();
            }
        }
        while (progressed);
    }

    private static bool ShouldAttemptSelection(PendingImportSelection selection, out string? skipReason)
    {
        var element = selection.Element;
        skipReason = null;
        if (!string.Equals(element.TypeName, "Spell", StringComparison.OrdinalIgnoreCase))
            return true;

        var profiles = GetOwnedSpellcastingProfiles(element.OwnerClass, selection.OwnerClassId).ToArray();
        SpellcastingInformation? spellInfo = profiles.Length == 1 ? profiles[0] : null;
        if (spellInfo == null)
            return true;

        bool preparedCaster = spellInfo.Prepare;
        bool isSpellbookCaster = preparedCaster
            && CharacterManager.Current.SelectionRules.Any(r => r.Attributes.Type == "Spell")
            && string.IsNullOrEmpty(spellInfo.InitialSupportedSpellsExpression?.Supports);

        if (!preparedCaster || isSpellbookCaster)
            return true;

        ElementBase? spell = DataManager.Current.ElementsCollection.GetElement(element.AuroraId);
        int spellLevel = GetSpellLevel(spell);
        if (spellLevel == 0)
            return true;

        skipReason = "Skipped non-cantrip spell because no matching live spell selection rule was available yet.";
        return false;
    }

    private static bool TryApplySelection(PendingImportSelection selection)
    {
        bool sawMatchingTypeRule = false;
        bool sawMatchingOption = false;

        foreach (SelectRule rule in CharacterManager.Current.SelectionRules
            .Where(r => string.Equals(r.Attributes.Type, selection.Element.TypeName, StringComparison.OrdinalIgnoreCase))
            .Where(r => selection.Element.TypeName is not ("Archetype" or "Spell") || string.IsNullOrWhiteSpace(selection.Element.OwnerClass)
                || RuleBelongsToClass(r, selection.OwnerClassId))
            .OrderBy(r => r.Attributes.RequiredLevel)
            .ThenBy(r => r.Attributes.Name, StringComparer.OrdinalIgnoreCase))
        {
            sawMatchingTypeRule = true;
            IReadOnlyList<ElementOption> options;
            try { options = BuildService.GetOptions(rule); }
            catch { continue; }

            if (!options.Any(o => string.Equals(o.Id, selection.Element.AuroraId, StringComparison.OrdinalIgnoreCase)))
                continue;
            sawMatchingOption = true;

            for (int number = 1; number <= rule.Attributes.Number; number++)
            {
                var current = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, number) as ElementBase;
                if (current?.Id == selection.Element.AuroraId)
                {
                    selection.LastFailureReason = null;
                    return true;
                }
                if (current != null)
                    continue;

                SelectionRuleExpanderContext.Current?.SetRegisteredElement(rule, selection.Element.AuroraId, number);
                selection.LastFailureReason = null;
                return true;
            }
        }

        selection.LastFailureReason = !sawMatchingTypeRule
            ? "No live selection rule of the required type was available."
            : !sawMatchingOption
                ? "A rule existed, but the resolved element was not offered by any current option list."
                : "Matching rules were already filled or unavailable.";
        return false;
    }

    private static bool RuleBelongsToClass(SelectRule rule, string? classId) =>
        CharacterManager.Current.ClassProgressionManagers.Any(progression =>
            classId != null && string.Equals(progression.ClassElement?.Id, classId, StringComparison.OrdinalIgnoreCase)
            && progression.SelectionRules.Any(candidate => candidate.UniqueIdentifier == rule.UniqueIdentifier));

    private static string? ResolveOwnerClassId(string? source, IReadOnlyList<ResolvedElement> resolved)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        // Keep exported names for display, but bind ownership to the user's resolved content
        // choice. A suffix such as "(legacy)" must not become a second engine class name.
        var ids = resolved.Where(element => (element.TypeName is "Class" or "Multiclass")
                && string.Equals(element.OwnerClass ?? element.Name, source, StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AuroraId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return ids.Length == 1 ? ids[0] : null;
    }

    private static IEnumerable<SpellcastingInformation> GetOwnedSpellcastingProfiles(string? source, string? classId)
    {
        var manager = CharacterManager.Current;
        if (classId != null)
            return manager.ClassProgressionManagers
                .Where(progression => string.Equals(progression.ClassElement?.Id, classId, StringComparison.OrdinalIgnoreCase))
                .SelectMany(progression => progression.SpellcastingInformations)
                .Where(profile => !profile.IsExtension);
        return manager.GetSpellcastingInformations().Where(profile => !profile.IsExtension
            && (string.IsNullOrWhiteSpace(source) || string.Equals(profile.Name, source, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ApplyEditableFields(
        Builder.Presentation.Models.Character character,
        ParsedCharacterSheet sheet,
        ImportResult result)
    {
        character.Name             = result.CharacterName;
        character.PlayerName       = result.PlayerName;
        character.Gender           = result.Gender;
        character.Alignment        = result.Alignment;
        character.Backstory        = result.Backstory;
        character.OrganisationName = "";
        character.Allies           = "";
        character.Eyes             = result.Eyes;
        character.Skin             = result.Skin;
        character.Hair             = result.Hair;
        character.PortraitFilename = !string.IsNullOrWhiteSpace(sheet.PortraitLocalPath)
            ? sheet.PortraitLocalPath
            : EnsureImportedPortraitPlaceholder();

        character.AgeField.Content    = result.Age;
        character.HeightField.Content = result.Height;
        character.WeightField.Content = result.Weight;

        try
        {
            character.BackgroundStory.Content = result.Backstory;
        }
        catch (Exception ex)
        {
            result.ImportDiagnostics.Add($"Note: background story field could not be set ({ex.Message}).");
        }

        try
        {
            character.FillableBackgroundCharacteristics.Traits.Content = result.PersonalityTraits;
            character.FillableBackgroundCharacteristics.Ideals.Content = result.Ideals;
            character.FillableBackgroundCharacteristics.Bonds.Content  = result.Bonds;
            character.FillableBackgroundCharacteristics.Flaws.Content  = result.Flaws;
        }
        catch (Exception ex)
        {
            result.ImportDiagnostics.Add($"Note: personality traits/ideals/bonds/flaws could not be set ({ex.Message}).");
        }

        character.Inventory.Coins.Set(0, 0, 0, 0, 0);
        character.Inventory.Equipment = "";
        character.Inventory.Treasure  = "";
        character.Inventory.QuestItems = "";

        if (TryParseExperience(sheet.ExperiencePoints, out int xp))
            character.Experience = xp;
    }

    private static void HarmonizeBaseAbilityScores(
        Builder.Presentation.Models.Character character,
        ImportResult result)
    {
        HarmonizeOne(character.Abilities.Strength,     result.Strength,     "Strength",     result);
        HarmonizeOne(character.Abilities.Dexterity,    result.Dexterity,    "Dexterity",    result);
        HarmonizeOne(character.Abilities.Constitution, result.Constitution, "Constitution", result);
        HarmonizeOne(character.Abilities.Intelligence, result.Intelligence, "Intelligence", result);
        HarmonizeOne(character.Abilities.Wisdom,       result.Wisdom,       "Wisdom",       result);
        HarmonizeOne(character.Abilities.Charisma,     result.Charisma,     "Charisma",     result);

        static void HarmonizeOne(dynamic ability, int targetFinal, string statName, ImportResult result)
        {
            try
            {
                int currentFinal = (int)ability.FinalScore;
                int currentBase  = (int)ability.BaseScore;
                int delta = targetFinal - currentFinal;
                ability.BaseScore = Math.Max(1, currentBase + delta);
            }
            catch (Exception ex)
            {
                result.ImportDiagnostics.Add(
                    $"Note: {statName} could not be harmonized ({ex.Message}); setting base score to {targetFinal} directly.");
                try { ability.BaseScore = Math.Max(1, targetFinal); }
                catch (Exception fallbackEx) { DebugLogService.Instance.LogException(fallbackEx, $"PdfImportService.HarmonizeAbilityScores fallback for '{statName}'"); }
            }
        }
    }

    private static string EnsureImportedPortraitPlaceholder()
    {
        try
        {
            string portraitsDir = DataManager.Current.UserDocumentsPortraitsDirectory;
            Directory.CreateDirectory(portraitsDir);

            string path = Path.Combine(portraitsDir, "_import-placeholder.png");
            if (!File.Exists(path))
            {
                const string transparentPixelBase64 =
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==";
                File.WriteAllBytes(path, Convert.FromBase64String(transparentPixelBase64));
            }

            return path;
        }
        catch
        {
            return "";
        }
    }

    private static void ApplyPreparedSpellState(
        ParsedCharacterSheet sheet,
        IReadOnlyList<ResolvedElement> resolvedElements,
        ImportResult result)
    {
        var byName = resolvedElements
            .Where(e => string.Equals(e.TypeName, "Spell", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().AuroraId, StringComparer.OrdinalIgnoreCase);

        int missedPrepared = 0;
        foreach (ParsedSpell spell in sheet.Spells.Where(s => s.IsPrepared))
        {
            var candidates = GetOwnedSpellcastingProfiles(spell.Source, ResolveOwnerClassId(spell.Source, resolvedElements))
                .Where(profile => profile.Prepare).ToArray();
            var spellInfo = candidates.Length == 1 ? candidates[0] : null;
            if (spellInfo is null)
            {
                missedPrepared++;
                continue;
            }
            if (!byName.TryGetValue(spell.Name, out string? id))
            {
                missedPrepared++;
                continue;
            }
            SpellcastingSectionContext.Current?.SetPrepareSpell(spellInfo, id);
        }

        if (missedPrepared > 0)
            result.ImportDiagnostics.Add(
                $"Note: {missedPrepared} prepared spell(s) need review because their spell or casting class could not be resolved unambiguously.");
    }

    private static void OverrideSnapshotFields(
        CharacterSnapshot snapshot,
        ParsedCharacterSheet sheet,
        ImportResult result)
    {
        snapshot.Name       = result.CharacterName;
        snapshot.PlayerName = result.PlayerName;
        snapshot.Alignment  = result.Alignment;
        snapshot.Backstory  = result.Backstory;
        snapshot.Gender     = result.Gender;
        snapshot.Age        = result.Age;
        snapshot.Height     = result.Height;
        snapshot.Weight     = result.Weight;
        snapshot.Eyes       = result.Eyes;
        snapshot.Skin       = result.Skin;
        snapshot.Hair       = result.Hair;

        if (TryParseExperience(sheet.ExperiencePoints, out int xp))
            snapshot.Experience = xp;
    }

    private static bool TryParseExperience(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string digits = new(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out value);
    }

    private static int GetSelectionPriority(string? typeName) =>
        typeName switch
        {
            "Race"     => 0,
            "Sub Race" => 1,
            "Class"    => 2,
            "Background" => 3,
            "Archetype" => 4,
            "Feat" => 5,
            "Language" => 6,
            "Proficiency" => 6,
            "Skill" => 6,
            "Tool Proficiency" => 6,
            "Spell" => 7,
            _ => 8,
        };

    private static int GetSpellLevel(ElementBase? spell)
    {
        if (spell == null) return -1;
        try { return (int)((dynamic)spell).Level; }
        catch { return -1; }
    }

    private static void AppendImportDiagnostics(
        ImportResult result,
        List<PendingImportSelection> pending,
        ParsedCharacterSheet sheet,
        IReadOnlyList<ResolvedElement> resolved)
    {
        result.ImportDiagnostics.Add(
            $"Resolved {resolved.Count} selections; applied {pending.Count(p => p.Applied)}; unapplied {pending.Count(p => !p.Applied)}.");

        int parsedSpells = sheet.Spells.Count;
        int resolvedSpells = resolved.Count(x => string.Equals(x.TypeName, "Spell", StringComparison.OrdinalIgnoreCase));
        int preparedSpells = sheet.Spells.Count(x => x.IsPrepared);
        result.ImportDiagnostics.Add(
            $"Spells: parsed {parsedSpells}, resolved {resolvedSpells}, prepared {preparedSpells}.");

        var unapplied = pending.Where(p => !p.Applied).ToList();
        foreach (PendingImportSelection selection in unapplied.Take(20))
        {
            result.ImportDiagnostics.Add(
                $"Unapplied {selection.Element.TypeName}: {selection.Element.Name} — {selection.LastFailureReason ?? "No diagnostic available."}");
        }
        if (unapplied.Count > 20)
            result.ImportDiagnostics.Add($"...and {unapplied.Count - 20} more unapplied selection(s) not shown.");
    }

    private sealed class PendingImportSelection
    {
        public PendingImportSelection(ResolvedElement element, string? ownerClassId = null)
        {
            Element = element;
            OwnerClassId = ownerClassId;
        }

        public ResolvedElement Element { get; }
        public string? OwnerClassId { get; }
        public bool Applied { get; set; }
        public string? LastFailureReason { get; set; }

        public bool MatchesType(string typeName) =>
            string.Equals(Element.TypeName, typeName, StringComparison.OrdinalIgnoreCase);
    }
}
