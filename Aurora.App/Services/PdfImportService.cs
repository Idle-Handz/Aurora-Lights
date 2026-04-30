using Aurora.PdfImport;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

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
            var engine = new CharacterInferenceEngine(dbPath);
            return engine.Infer(sheet);
        });

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

        var result = await Task.Run(() =>
        {
            var sheet  = DndBeyondJsonImporter.Parse(jsonPath);
            var engine = new CharacterInferenceEngine(dbPath);
            return (sheet, engine.Infer(sheet));
        });

        return result;
    }

    // ── File writing ──────────────────────────────────────────────────────────

    /// <summary>Writes the import result to a .dnd5e file at <paramref name="outputPath"/>.</summary>
    public async Task WriteCharacterFileAsync(
        ParsedCharacterSheet sheet,
        ImportResult result,
        string outputPath)
    {
        using var scope = await CharacterContext.EnterForLoadAsync();
        await BuildAndSaveCharacterFileAsync(sheet, result, outputPath);
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
            .Where(e => !string.Equals(e.TypeName, "Level", StringComparison.OrdinalIgnoreCase))
            .Select(e => new PendingImportSelection(e))
            .ToList();

        RequireSelection(pending, "Race");
        RequireSelection(pending, "Class");
        RequireSelection(pending, "Background", required: false);

        ApplyPendingSelections(pending, cm.Character.Level);

        while (cm.Character.Level < Math.Max(1, result.Level))
        {
            cm.LevelUpMain();
            cm.ReprocessCharacter();
            ApplyPendingSelections(pending, cm.Character.Level);
        }

        ApplyPendingSelections(pending, cm.Character.Level, includeDeferred: true);

        ApplyEditableFields(cm.Character, sheet, result);
        HarmonizeBaseAbilityScores(cm.Character, result);
        cm.ReprocessCharacter();
        ApplyPreparedSpellState(sheet, resolved);
        AppendImportDiagnostics(result, pending, sheet, resolved);

        var file = new CharacterFile(outputPath);
        file.Save(cm.Character);

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
                if (!ShouldAttemptSelection(selection.Element, out string? skipReason))
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

    private static bool ShouldAttemptSelection(ResolvedElement element, out string? skipReason)
    {
        skipReason = null;
        if (!string.Equals(element.TypeName, "Spell", StringComparison.OrdinalIgnoreCase))
            return true;

        SpellcastingInformation? spellInfo = CharacterManager.Current
            .GetSpellcastingInformations()
            .FirstOrDefault(x => !x.IsExtension);
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
        character.PortraitFilename = EnsureImportedPortraitPlaceholder();

        character.AgeField.Content    = result.Age;
        character.HeightField.Content = result.Height;
        character.WeightField.Content = result.Weight;

        try
        {
            character.BackgroundStory.Content = result.Backstory;
        }
        catch { }

        try
        {
            character.FillableBackgroundCharacteristics.Traits.Content = result.PersonalityTraits;
            character.FillableBackgroundCharacteristics.Ideals.Content = result.Ideals;
            character.FillableBackgroundCharacteristics.Bonds.Content  = result.Bonds;
            character.FillableBackgroundCharacteristics.Flaws.Content  = result.Flaws;
        }
        catch { }

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
        SetBaseScore(character.Abilities.Strength,     result.Strength);
        SetBaseScore(character.Abilities.Dexterity,    result.Dexterity);
        SetBaseScore(character.Abilities.Constitution, result.Constitution);
        SetBaseScore(character.Abilities.Intelligence, result.Intelligence);
        SetBaseScore(character.Abilities.Wisdom,       result.Wisdom);
        SetBaseScore(character.Abilities.Charisma,     result.Charisma);

        static void SetBaseScore(dynamic ability, int targetFinal)
        {
            int currentFinal = targetFinal;
            int currentBase = 1;

            try { currentFinal = (int)ability.FinalScore; } catch { }
            try { currentBase = (int)ability.BaseScore; } catch { }

            int delta = targetFinal - currentFinal;
            ability.BaseScore = Math.Max(1, currentBase + delta);
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
        IReadOnlyList<ResolvedElement> resolvedElements)
    {
        SpellcastingInformation? spellInfo = CharacterManager.Current
            .GetSpellcastingInformations()
            .FirstOrDefault(x => !x.IsExtension);
        if (spellInfo == null || !spellInfo.Prepare)
            return;

        var byName = resolvedElements
            .Where(e => string.Equals(e.TypeName, "Spell", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().AuroraId, StringComparer.OrdinalIgnoreCase);

        foreach (ParsedSpell spell in sheet.Spells.Where(s => s.IsPrepared))
        {
            if (!byName.TryGetValue(spell.Name, out string? id))
                continue;

            SpellcastingSectionContext.Current?.SetPrepareSpell(spellInfo, id);
        }
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
            "Race" => 0,
            "Sub Race" => 1,
            "Class" => 2,
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

        foreach (PendingImportSelection selection in pending.Where(p => !p.Applied).Take(20))
        {
            result.ImportDiagnostics.Add(
                $"Unapplied {selection.Element.TypeName}: {selection.Element.Name} — {selection.LastFailureReason ?? "No diagnostic available."}");
        }
    }

    private sealed class PendingImportSelection
    {
        public PendingImportSelection(ResolvedElement element) => Element = element;

        public ResolvedElement Element { get; }
        public bool Applied { get; set; }
        public string? LastFailureReason { get; set; }

        public bool MatchesType(string typeName) =>
            string.Equals(Element.TypeName, typeName, StringComparison.OrdinalIgnoreCase);
    }
}
