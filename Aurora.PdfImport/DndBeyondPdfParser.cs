using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Aurora.PdfImport;

/// <summary>
/// Extracts a <see cref="ParsedCharacterSheet"/> from a D&amp;D Beyond 2018-template PDF export.
/// </summary>
public static class DndBeyondPdfParser
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static ParsedCharacterSheet Parse(string pdfPath)
    {
        using var doc = PdfDocument.Open(pdfPath);
        return ParsePdfDocument(doc);
    }

    public static ParsedCharacterSheet Parse(Stream stream)
    {
        using var doc = PdfDocument.Open(stream);
        return ParsePdfDocument(doc);
    }

    /// <summary>
    /// Parses a character sheet from OCR results produced by platform-specific rendering.
    /// Coordinates in <paramref name="ocrPages"/> must be in PDF points (1/72 inch), top-left origin.
    /// </summary>
    public static ParsedCharacterSheet ParseFromOcr(IReadOnlyList<OcrPage> ocrPages)
    {
        var pages = ocrPages
            .Select(p => p.Words
                .Select(w => new PdfWord(w.Text, w.Left, w.Top, w.Right, w.Bottom))
                .OrderBy(w => w.Top).ThenBy(w => w.Left)
                .ToList())
            .ToList();

        return ParseWordPages(pages);
    }

    // ── Per-page dispatch ─────────────────────────────────────────────────────

    private static ParsedCharacterSheet ParsePdfDocument(PdfDocument doc)
    {
        // Build a word list per page with normalised coordinates.
        // PdfPig y=0 is bottom-left; we flip to top-left (y=0 at top).
        var pages = new List<List<PdfWord>>();
        foreach (var page in doc.GetPages())
        {
            double pageHeight = page.Height;
            var words = page.GetWords()
                .Select(w => new PdfWord(
                    w.Text,
                    w.BoundingBox.Left,
                    pageHeight - w.BoundingBox.Top,
                    w.BoundingBox.Right,
                    pageHeight - w.BoundingBox.Bottom))
                .OrderBy(w => w.Top)
                .ThenBy(w => w.Left)
                .ToList();
            pages.Add(words);
        }

        return ParseWordPages(pages);
    }

    // Returns true when page-1 words contain the three template labels that appear on
    // every D&D Beyond character sheet, confirming this is a recognisable format.
    private static bool IsDndBeyondFormat(List<PdfWord> page1Words)
    {
        bool hasClass = false, hasLevel = false, hasCharacter = false;
        foreach (var w in page1Words)
        {
            if (string.Equals(w.Text, "CLASS",     StringComparison.OrdinalIgnoreCase)) hasClass     = true;
            if (string.Equals(w.Text, "LEVEL",     StringComparison.OrdinalIgnoreCase)) hasLevel     = true;
            if (string.Equals(w.Text, "CHARACTER", StringComparison.OrdinalIgnoreCase)) hasCharacter = true;
            if (hasClass && hasLevel && hasCharacter) return true;
        }
        return false;
    }

    private static ParsedCharacterSheet ParseWordPages(List<List<PdfWord>> pages)
    {
        var sheet = new ParsedCharacterSheet();

        if (pages.Count == 0) return sheet;

        // Confirm this looks like a D&D Beyond sheet before doing any real work.
        // A genuine sheet always has CLASS, LEVEL, and CHARACTER as template labels.
        if (!IsDndBeyondFormat(pages[0]))
        {
            sheet.IsUnsupportedFormat = true;
            return sheet;
        }

        // Detect template version from copyright footer on page 1.
        sheet.TemplateVersion = DetectTemplateVersion(pages[0]);

        // Page 1 – header, stats, skills, combat, proficiencies, senses, actions/weapons.
        ParsePage1(pages[0], sheet);

        // Page 2 – features & traits (primary), equipment.
        if (pages.Count >= 2) ParseFeaturesPage(pages[1], sheet);

        // Page 3+ – additional features & traits (overflow), equipment overflow.
        for (int i = 2; i < pages.Count; i++)
        {
            string pageText = string.Join(" ", pages[i].Select(w => w.Text));
            if (pageText.Contains("ADDITIONAL FEATURES", StringComparison.OrdinalIgnoreCase))
                ParseFeaturesPage(pages[i], sheet);
            else if (pageText.Contains("SPELLCASTING", StringComparison.OrdinalIgnoreCase))
                ParseSpellPage(pages[i], sheet);
            else if (pageText.Contains("CHARACTER APPEARANCE", StringComparison.OrdinalIgnoreCase))
                ParseBioPage(pages[i], sheet);
        }

        return sheet;
    }

    // ── Known template label words (used to filter out labels when searching for values) ──

    private static readonly HashSet<string> _templateLabelWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SPECIES", "CLASS", "LEVEL", "PLAYER", "NAME", "CHARACTER", "BACKGROUND",
        "EXPERIENCE", "POINTS", "PASSIVE", "PERCEPTION", "INSIGHT", "INVESTIGATION",
        "SENSES", "WEAPON", "ATTACKS", "CANTRIPS", "INITIATIVE", "STRENGTH",
        "DEXTERITY", "CONSTITUTION", "INTELLIGENCE", "WISDOM", "CHARISMA",
        "SAVING", "THROWS", "MODIFIERS", "SKILLS", "PROFICIENCIES", "TRAINING",
        "DEFENSES", "HIT", "DICE", "DEATH", "SAVES", "SUCCESSES", "FAILURES",
        "SPEED", "ARMOR", "PROFICIENCY", "BONUS", "ABILITY", "SAVE", "DC",
        "HEROIC", "INSPIRATION", "NOTES", "ACTIONS", "ENCUMBERED", "PUSH", "DRAG",
        "LIFT", "WEIGHT", "CARRIED", "CURRENT", "TEMP", "MAX", "&", "HP", "Total",
        "RACE",  // older template uses RACE instead of SPECIES
    };

    // Known D&D class names used as last-resort class+level extraction.
    private static readonly string[] _knownClassNames =
    [
        "Blood Hunter", // must come before single-word names (longer match first)
        "Artificer", "Barbarian", "Bard", "Cleric", "Druid", "Fighter", "Monk",
        "Paladin", "Ranger", "Rogue", "Sorcerer", "Warlock", "Wizard",
    ];

    // ── Page 1: header + stats ────────────────────────────────────────────────

    private static void ParsePage1(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // Parse the header fields using line-grouping rather than directional positional search.
        // Groups words by y-coordinate (within a tolerance) into "lines", then finds the
        // value line adjacent to each label line — handles both above-label and below-label layouts.
        ParseHeader(words, sheet);

        // Ability scores: each score is a large number inside a labelled box.
        sheet.Strength     = ExtractAbilityScore(words, "STRENGTH");
        sheet.Dexterity    = ExtractAbilityScore(words, "DEXTERITY");
        sheet.Constitution = ExtractAbilityScore(words, "CONSTITUTION");
        sheet.Intelligence = ExtractAbilityScore(words, "INTELLIGENCE");
        sheet.Wisdom       = ExtractAbilityScore(words, "WISDOM");
        sheet.Charisma     = ExtractAbilityScore(words, "CHARISMA");

        // Combat numbers.
        // ExtractInt("Max HP") works when the PDF has embedded text (PdfPig path);
        // for OCR the words are separate ("Max", "HP", number), so fall back to a
        // centre-column positional search.
        sheet.MaxHitPoints     = ExtractInt(words, "Max HP",          rightOf: false, below: true)
                              ?? ExtractMaxHpFromOcr(words);
        sheet.ArmorClass       = ExtractIntNear(words, "CLASS",       regionTop: 80,  regionBottom: 200);
        sheet.Initiative       = ExtractIntNear(words, "INITIATIVE",  regionTop: 80,  regionBottom: 220);
        sheet.ProficiencyBonus = ExtractIntNear(words, "PROFICIENCY BONUS", regionTop: 200, regionBottom: 500);
        sheet.HitDice          = ExtractTextNear(words, "HIT DICE",   regionTop: 250, regionBottom: 430);
        sheet.Speed            = ExtractTextNear(words, "SPEED",      regionTop: 350, regionBottom: 580);

        // Saving throws: proficient ones have a filled bullet (•) or filled circle.
        // D&D Beyond renders the bullet as a separate glyph to the left of the stat name.
        sheet.SaveStrength     = IsSaveProficient(words, "Strength",     savingThrowRegion: true);
        sheet.SaveDexterity    = IsSaveProficient(words, "Dexterity",    savingThrowRegion: true);
        sheet.SaveConstitution = IsSaveProficient(words, "Constitution", savingThrowRegion: true);
        sheet.SaveIntelligence = IsSaveProficient(words, "Intelligence", savingThrowRegion: true);
        sheet.SaveWisdom       = IsSaveProficient(words, "Wisdom",       savingThrowRegion: true);
        sheet.SaveCharisma     = IsSaveProficient(words, "Charisma",     savingThrowRegion: true);

        // Skills: same proficiency-dot approach.
        foreach (string skill in AllSkills)
            if (IsSkillProficient(words, skill))
                sheet.ProficientSkills.Add(skill);

        // Passive scores.
        sheet.PassivePerception    = ExtractPassive(words, "PASSIVE PERCEPTION");
        sheet.PassiveInsight       = ExtractPassive(words, "PASSIVE INSIGHT");
        sheet.PassiveInvestigation = ExtractPassive(words, "PASSIVE INVESTIGATION");

        // Proficiencies & training block (right side of page 1).
        ParseProficienciesBlock(words, sheet);

        // Senses (below passive scores on left).
        ParseSenses(words, sheet);
    }

    // ── Header parsing (line-grouping approach) ───────────────────────────────

    private static void ParseHeader(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // Group ALL page words into visual lines.
        // 10pt tolerance handles slight baseline variations in D&D Beyond's fonts.
        var lines = GroupIntoLines(words, tolerance: 10.0);

        // Anchor: find the label line that contains "CLASS" and "LEVEL".
        // "&" is skipped — it may be encoded as a ligature or different Unicode character.
        int anchorIdx = FindLineWithAllTokens(lines, ["CLASS", "&", "LEVEL"]);
        if (anchorIdx < 0) anchorIdx = FindLineWithAllTokens(lines, ["CLASS", "LEVEL"]);
        if (anchorIdx < 0)
        {
            // Last-resort anchor: find the first occurrence of "LEVEL" in the upper half of lines.
            for (int i = 0; i < lines.Count / 2 + 1; i++)
            {
                if (lines[i].Any(w => string.Equals(w.Text, "LEVEL", StringComparison.OrdinalIgnoreCase)))
                { anchorIdx = i; break; }
            }
        }

        // Search within ±12 lines of the anchor (header is typically 2–4 lines).
        int lo = anchorIdx >= 0 ? Math.Max(0, anchorIdx - 12) : 0;
        int hi = anchorIdx >= 0 ? Math.Min(lines.Count - 1, anchorIdx + 12) : Math.Min(lines.Count - 1, 30);
        var headerLines = lines.Skip(lo).Take(hi - lo + 1).ToList();

        sheet.CharacterName    = ExtractValueNearLabel(headerLines, ["CHARACTER", "NAME"]);
        sheet.PlayerName       = ExtractValueNearLabel(headerLines, ["PLAYER", "NAME"]);
        // Newer templates use "SPECIES"; older ones use "RACE".
        sheet.Species          = ExtractValueNearLabel(headerLines, ["SPECIES"])
                              ?? ExtractValueNearLabel(headerLines, ["RACE"]);
        sheet.Background       = ExtractValueNearLabel(headerLines, ["BACKGROUND"]);
        sheet.ExperiencePoints = ExtractValueNearLabel(headerLines, ["EXPERIENCE", "POINTS"]);

        string? classLevel = ExtractValueNearLabel(headerLines, ["CLASS", "&", "LEVEL"]);
        if (string.IsNullOrWhiteSpace(classLevel))
            classLevel = ExtractValueNearLabel(headerLines, ["CLASS", "LEVEL"]);

        // Regex fallback on header-region text, restricted to known class names.
        if (string.IsNullOrWhiteSpace(classLevel))
        {
            string headerText = string.Join(" ", headerLines.SelectMany(l => l).Select(w => w.Text));
            classLevel = FindClassLevelByKnownNames(headerText);
        }

        // Last-resort: scan full page for a known class name followed by a level digit.
        if (string.IsNullOrWhiteSpace(classLevel))
        {
            string allText = string.Join(" ", words.Select(w => w.Text));
            classLevel = FindClassLevelByKnownNames(allText);
        }

        ParseClassLevel(classLevel, sheet);

        // Write diagnostics so we can see exactly what the parser found.
        sheet.ParseDiagnostics = WriteDiagnosticFile(words, lines, anchorIdx, headerLines, sheet);
    }

    // ── Known-class-name regex search ────────────────────────────────────────

    private static string? FindClassLevelByKnownNames(string text)
    {
        // Iterate longest names first so "Blood Hunter" is matched before "Hunter" (if ever present).
        foreach (string name in _knownClassNames)
        {
            var m = Regex.Match(text,
                $@"\b{Regex.Escape(name)}\s+(\d+)\b",
                RegexOptions.IgnoreCase);
            if (m.Success)
                return $"{name} {m.Groups[1].Value}";
        }
        return null;
    }

    // ── Line-grouping helpers ─────────────────────────────────────────────────

    private static int FindLineWithAllTokens(List<List<PdfWord>> lines, string[] tokens)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var texts = lines[i].Select(w => w.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (tokens.All(t => texts.Contains(t))) return i;
        }
        return -1;
    }

    /// <summary>
    /// Groups words into visual lines using fixed line centres (the first word's Top).
    /// Using a fixed centre prevents running-average drift that can split tokens from
    /// the same visual line (e.g. "CLASS", "&", "LEVEL") into separate buckets.
    /// Returns lines sorted top-to-bottom, each line sorted left-to-right.
    /// </summary>
    private static List<List<PdfWord>> GroupIntoLines(List<PdfWord> words, double tolerance)
    {
        var centres = new List<double>();
        var lines   = new List<List<PdfWord>>();

        foreach (var word in words.OrderBy(w => w.Top).ThenBy(w => w.Left))
        {
            // Find the closest existing line centre (fixed — not updated after creation).
            int bestIdx  = -1;
            double bestD = double.MaxValue;
            for (int i = 0; i < centres.Count; i++)
            {
                double d = Math.Abs(centres[i] - word.Top);
                if (d < bestD) { bestD = d; bestIdx = i; }
            }

            if (bestIdx >= 0 && bestD <= tolerance)
                lines[bestIdx].Add(word);
            else
            {
                centres.Add(word.Top);
                lines.Add([word]);
            }
        }

        return lines
            .Select((l, i) => (Words: l.OrderBy(w => w.Left).ToList(), Y: centres[i]))
            .OrderBy(x => x.Y)
            .Select(x => x.Words)
            .ToList();
    }

    /// <summary>
    /// Finds the specific label words in <paramref name="lines"/> for all <paramref name="labelTokens"/>,
    /// then returns the text of the nearest adjacent line within that field's horizontal range.
    /// </summary>
    private static string? ExtractValueNearLabel(List<List<PdfWord>> lines, string[] labelTokens)
    {
        int labelIdx = -1;
        List<PdfWord>? labelWords = null;

        for (int i = 0; i < lines.Count; i++)
        {
            var matched = FindLabelWordsInLine(lines[i], labelTokens);
            if (matched != null) { labelIdx = i; labelWords = matched; break; }
        }
        if (labelIdx < 0 || labelWords == null) return null;

        // Try above first (D&D Beyond values sit above their labels).
        // Check -2 before +1 so we don't accidentally grab the next content section below.
        foreach (int offset in new[] { -1, -2, 1, 2 })
        {
            int idx = labelIdx + offset;
            if (idx < 0 || idx >= lines.Count) continue;
            string value = ExtractValueFromLine(lines[idx], labelWords);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    /// <summary>
    /// Returns the specific <see cref="PdfWord"/> instances from <paramref name="line"/>
    /// that match all <paramref name="tokens"/> (case-sensitive), or null if any token is missing.
    /// Template labels are UPPERCASE; using ordinal comparison prevents title-case value words
    /// (e.g. "Background") from being mistaken for labels (e.g. "BACKGROUND").
    /// </summary>
    private static List<PdfWord>? FindLabelWordsInLine(List<PdfWord> line, string[] tokens)
    {
        var result = new List<PdfWord>();
        foreach (var token in tokens)
        {
            var word = line.FirstOrDefault(w => string.Equals(w.Text, token, StringComparison.Ordinal));
            if (word == null) return null;
            result.Add(word);
        }
        return result;
    }

    /// <summary>
    /// Extracts value words from <paramref name="valueLine"/> that fall within the
    /// tight horizontal bounding box of <paramref name="labelWords"/> (±40 pt margin).
    /// Filters out the label tokens themselves so they aren't treated as values.
    /// </summary>
    private static string ExtractValueFromLine(List<PdfWord> valueLine, List<PdfWord> labelWords)
    {
        double labelLeft  = labelWords.Min(w => w.Left);
        double labelRight = labelWords.Max(w => w.Right);
        // Case-sensitive: "BACKGROUND" ≠ "Background", so a background value named
        // "Custom Background" isn't accidentally filtered as a label token.
        var labelTexts = labelWords.Select(w => w.Text).ToHashSet(StringComparer.Ordinal);

        var valueWords = valueLine
            .Where(w => !labelTexts.Contains(w.Text)
                     && w.Left  <= labelRight + 40
                     && w.Right >= labelLeft  - 40)
            .OrderBy(w => w.Left)
            .ToList();

        return string.Join(" ", valueWords.Select(w => w.Text)).Trim();
    }

    // ── Diagnostic output ─────────────────────────────────────────────────────

    private static string WriteDiagnosticFile(
        List<PdfWord> words,
        List<List<PdfWord>> lines,
        int anchorIdx,
        List<List<PdfWord>> headerLines,
        ParsedCharacterSheet sheet)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Aurora PDF Parser Diagnostics ===");
        sb.AppendLine($"Page-1 words: {words.Count}  |  Grouped lines: {lines.Count}");
        sb.AppendLine($"Anchor ('CLASS & LEVEL' line) index: {anchorIdx}");
        sb.AppendLine($"Header region: {headerLines.Count} lines");
        sb.AppendLine($"Extracted → Name='{sheet.CharacterName}' Class='{sheet.ClassName}'" +
                      $" Level={sheet.ClassLevel} Species='{sheet.Species}' Background='{sheet.Background}'");
        sb.AppendLine();
        sb.AppendLine("--- First 30 grouped lines (text @ Left,Top) ---");
        for (int i = 0; i < Math.Min(30, lines.Count); i++)
        {
            var tokens = lines[i].Select(w => $"'{w.Text}'@({w.Left:F0},{w.Top:F0})");
            sb.AppendLine($"  [{i:00}] {string.Join("  ", tokens)}");
        }
        sb.AppendLine();
        sb.AppendLine("--- Header region lines ---");
        for (int i = 0; i < headerLines.Count; i++)
            sb.AppendLine($"  [{i:00}] {string.Join(" | ", headerLines[i].Select(w => w.Text))}");

        string diag = sb.ToString();

        try
        {
            string path = Path.Combine(Path.GetTempPath(), "aurora_pdf_debug.txt");
            File.WriteAllText(path, diag);
        }
        catch { /* non-fatal */ }

        // Return a compact summary for the UI.
        return $"Anchor line: {anchorIdx}  |  Header lines: {headerLines.Count}  |  " +
               $"Name='{sheet.CharacterName}'  Class='{sheet.ClassName}'  Level={sheet.ClassLevel}  " +
               $"Species='{sheet.Species}'  Background='{sheet.Background}'" +
               $"\nFull details: %TEMP%\\aurora_pdf_debug.txt";
    }

    // ── Class & level parsing ─────────────────────────────────────────────────

    private static void ParseClassLevel(string? raw, ParsedCharacterSheet sheet)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        // Match "ClassName N" where N is digits at end; class name may include "(archived)" etc.
        var m = Regex.Match(raw.Trim(), @"^(.+?)\s+(\d+)$");
        if (m.Success)
        {
            sheet.ClassName  = m.Groups[1].Value.Trim();
            sheet.ClassLevel = int.Parse(m.Groups[2].Value);
        }
        else
        {
            sheet.ClassName = raw.Trim();
        }
    }

    // ── Proficiencies block ───────────────────────────────────────────────────

    private static void ParseProficienciesBlock(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // The proficiencies block contains "=== ARMOR ===", "=== WEAPONS ===", etc.
        // We read the text of the right-side panel and split on section headers.
        // leftMin=650 keeps armor/weapon/tool/language items (x≥777) while
        // cutting out "BONUS" (x≈598) which leaks from the PROFICIENCY BONUS label.
        string block = JoinWordsInRegion(words, leftMin: 650, leftMax: 950, topMin: 0, topMax: 800);

        sheet.ArmorProficiencies  = ExtractSectionList(block, "ARMOR");
        sheet.WeaponProficiencies = ExtractSectionList(block, "WEAPONS");
        sheet.ToolProficiencies   = ExtractSectionList(block, "TOOLS");
        sheet.Languages           = ExtractSectionList(block, "LANGUAGES");
    }

    private static List<string> ExtractSectionList(string block, string header)
    {
        // OCR produces either "=== HEADER ===" or "=== HEADER" (no trailing ===) or plain "HEADER".
        // Stop at the next "===" marker or at the next ALL-CAPS section header on its own line.
        var match = Regex.Match(block,
            $@"(?:===\s*)?{Regex.Escape(header)}\s*(?:===\s*)?(.*?)(?:(?====)|(?=\n[A-Z]{{3,}}\b)|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success) return [];

        string content = match.Groups[1].Value.Trim();
        return [.. content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    // ── Senses ────────────────────────────────────────────────────────────────

    private static void ParseSenses(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // Senses appear below PASSIVE INVESTIGATION, above SENSES label.
        string block = JoinWordsInRegion(words, leftMin: 0, leftMax: 320, topMin: 580, topMax: 760);
        if (!string.IsNullOrWhiteSpace(block))
        {
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string l = line.Trim();
                if (!string.IsNullOrEmpty(l) && !l.Equals("SENSES", StringComparison.OrdinalIgnoreCase))
                    sheet.Senses.Add(l);
            }
        }
    }

    // ── Features & traits page ────────────────────────────────────────────────

    private static void ParseFeaturesPage(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // Collect the text of the large features box (below header, above equipment).
        // The features box runs roughly y 160–550 on page 2.
        string block = JoinWordsInRegion(words, leftMin: 0, leftMax: 950, topMin: 155, topMax: 560);
        ParseFeaturesText(block, sheet);

        // Equipment table is in the lower half.
        string equipBlock = JoinWordsInRegion(words, leftMin: 0, leftMax: 950, topMin: 555, topMax: 900);
        ParseEquipmentBlock(equipBlock, sheet);
    }

    private static void ParseFeaturesText(string text, ParsedCharacterSheet sheet)
    {
        // Split into lines for pattern matching.
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        bool inFeats = false;
        ParsedFeature? current = null;
        var descBuf = new StringBuilder();

        void FlushCurrent()
        {
            if (current == null) return;
            current.Description = descBuf.ToString().Trim();
            descBuf.Clear();
            if (inFeats) sheet.Feats.Add(current);
            else         sheet.Features.Add(current);
            current = null;
        }

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Section switches.
            if (Regex.IsMatch(line, @"===\s*FEATS\s*===", RegexOptions.IgnoreCase))
            {
                FlushCurrent();
                inFeats = true;
                continue;
            }
            if (Regex.IsMatch(line, @"===\s*\w+.*?FEATURES\s*===", RegexOptions.IgnoreCase)
             || Regex.IsMatch(line, @"===\s*\w+.*?TRAITS\s*===",   RegexOptions.IgnoreCase))
            {
                FlushCurrent();
                inFeats = false;
                continue;
            }

            // Top-level feature: "* Feature Name • SOURCE page *"
            var topFeature = Regex.Match(line, @"^\*\s*(.+?)(?:\s*•\s*(\S+)\s*(\d+))?\s*\*?$");
            if (topFeature.Success && !line.StartsWith("|"))
            {
                FlushCurrent();
                current = new ParsedFeature
                {
                    Name        = topFeature.Groups[1].Value.Trim(),
                    Source      = topFeature.Groups[2].Success ? topFeature.Groups[2].Value.Trim() : null,
                    SourcePage  = topFeature.Groups[3].Success ? topFeature.Groups[3].Value.Trim() : null,
                    IsSubFeature = false,
                };
                continue;
            }

            // Sub-feature: "| Sub Name • SOURCE" or "| Sub Name • N Action"
            var subFeature = Regex.Match(line, @"^\|\s*(.+?)(?:\s*•\s*(.+))?$");
            if (subFeature.Success)
            {
                FlushCurrent();
                current = new ParsedFeature
                {
                    Name        = subFeature.Groups[1].Value.Trim(),
                    Source      = subFeature.Groups[2].Success ? subFeature.Groups[2].Value.Trim() : null,
                    IsSubFeature = true,
                };
                continue;
            }

            // Otherwise it's description text for the current feature.
            if (current != null)
            {
                if (descBuf.Length > 0) descBuf.Append(' ');
                descBuf.Append(line);
            }
        }

        FlushCurrent();
    }

    // ── Equipment block ───────────────────────────────────────────────────────

    private static void ParseEquipmentBlock(string block, ParsedCharacterSheet sheet)
    {
        // Rows are: NAME  QTY  WEIGHT  [NAME  QTY  WEIGHT]
        // We look for lines where the name is not a header keyword.
        var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NAME", "QTY", "WEIGHT", "EQUIPMENT", "ADDITIONAL", "ATTUNED", "MAGIC", "ITEMS",
            "WEIGHT", "CARRIED", "ENCUMBERED", "PUSH", "DRAG", "LIFT", "CP", "GP", "SP", "EP", "PP",
        };

        foreach (string rawLine in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Tokenise the line: NAME QTY WEIGHT [NAME QTY WEIGHT]
            // Typical: "Longsword 1 3 lb."
            // Weight may be "--" for weightless items.
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;

            // Try to parse pairs of (name, qty, weight) from the token stream.
            ParseItemsFromTokens(tokens, skipWords, sheet);
        }
    }

    private static void ParseItemsFromTokens(string[] tokens, HashSet<string> skipWords, ParsedCharacterSheet sheet)
    {
        int i = 0;
        while (i < tokens.Length)
        {
            // Skip header/label tokens.
            if (skipWords.Contains(tokens[i])) { i++; continue; }

            // Accumulate name tokens (stop when we hit a digit = qty, or "--").
            var nameParts = new List<string>();
            while (i < tokens.Length && !Regex.IsMatch(tokens[i], @"^\d+$") && tokens[i] != "--")
            {
                if (skipWords.Contains(tokens[i])) { i++; break; }
                nameParts.Add(tokens[i]);
                i++;
            }

            if (nameParts.Count == 0) { i++; continue; }
            string name = string.Join(" ", nameParts);

            // Next token: qty (integer) or "--".
            int qty = 1;
            if (i < tokens.Length && Regex.IsMatch(tokens[i], @"^\d+$"))
            {
                qty = int.Parse(tokens[i]);
                i++;
            }

            // Next tokens: weight value + "lb." or "--".
            string? weight = null;
            if (i < tokens.Length)
            {
                if (tokens[i] == "--") { weight = "--"; i++; }
                else if (Regex.IsMatch(tokens[i], @"^\d+(\.\d+)?$"))
                {
                    weight = tokens[i];
                    i++;
                    if (i < tokens.Length && tokens[i].StartsWith("lb", StringComparison.OrdinalIgnoreCase))
                        i++;
                }
            }

            if (!string.IsNullOrWhiteSpace(name) && !skipWords.Contains(name))
            {
                sheet.Equipment.Add(new ParsedItem { Name = name, Quantity = qty, Weight = weight });
            }
        }
    }

    // ── Spell page ────────────────────────────────────────────────────────────

    private static void ParseSpellPage(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        // Header: spellcasting class, ability, save DC, attack bonus (top strip).
        sheet.SpellcastingClass   ??= ExtractTextNear(words, "SPELLCASTING", regionTop: 80, regionBottom: 160);
        sheet.SpellcastingAbility ??= ExtractTextNear(words, "ABILITY",      regionTop: 80, regionBottom: 165);
        sheet.SpellSaveDc         ??= ExtractIntNear(words,  "SAVE DC",      regionTop: 80, regionBottom: 165);
        sheet.SpellAttackBonus    ??= ExtractIntNear(words,  "ATTACK",       regionTop: 80, regionBottom: 165);

        // Spell table runs below the header.
        string tableText = JoinWordsInRegion(words, leftMin: 0, leftMax: 950, topMin: 160, topMax: 820);
        ParseSpellTable(tableText, sheet);
    }

    private static void ParseSpellTable(string text, ParsedCharacterSheet sheet)
    {
        int? currentLevel = null;

        foreach (string rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Level header: "=== CANTRIPS ===" or "=== 1st LEVEL ==="
            var levelHeader = Regex.Match(line, @"===\s*(CANTRIPS|\d+(?:st|nd|rd|th)\s+LEVEL)\s*===", RegexOptions.IgnoreCase);
            if (levelHeader.Success)
            {
                string lh = levelHeader.Groups[1].Value.Trim();
                currentLevel = lh.StartsWith("CANT", StringComparison.OrdinalIgnoreCase)
                    ? (int?)null
                    : int.Parse(Regex.Match(lh, @"\d+").Value);
                continue;
            }

            // Slot count line: "3 Slots OOO" — skip.
            if (Regex.IsMatch(line, @"^\d+\s+Slots", RegexOptions.IgnoreCase)) continue;

            // Spell row: starts with open circle "O" (the prep indicator), then name.
            // Format: [O] SpellName [Source] [Save/Atk] [Time] [Range] [Comp] [Duration] [PageRef] [Notes]
            // The name is always immediately after the circle token.
            // Source appears in blue (same text stream), then the remaining columns.
            var spellRow = Regex.Match(line, @"^O\s+(.+)$");
            if (!spellRow.Success) continue;

            string rest = spellRow.Groups[1].Value.Trim();

            // The page ref is the last recognisable "ABBREV NNN" or "ABBREV-YEAR NNN" token.
            // Pull it from the end working backwards.
            string? pageRef   = null;
            string? spellName = null;
            string? source    = null;

            var pageRefMatch = Regex.Match(rest, @"\b([A-Z][A-Za-z\-]+\s+\d+)\s*(?:[^\s].*)?$");
            if (pageRefMatch.Success)
            {
                pageRef = pageRefMatch.Groups[1].Value.Trim();
                rest = rest[..pageRefMatch.Index].Trim();
            }

            // What remains: SpellName [R]  Source  Save/Atk  Time  Range  Comp  Duration
            // Source is typically one or two words of title-case that match a known class/feat name.
            // We split by whitespace and try to find where the name ends and source begins.
            // Simple heuristic: name is everything up to the first token that is all-uppercase letters
            // with a save type (DEX, CON, WIS, STR, INT, CHA) or a source abbreviation.
            // More reliable: split at the first "  " (double-space) gap from positional data,
            // but since we've linearised, use a known-source set.

            // Split remainder into tokens.
            var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Known sources that appear in spell table SOURCE column.
            var nameTokens   = new List<string>();
            var sourceTokens = new List<string>();
            bool foundSource = false;

            for (int i = 0; i < parts.Count; i++)
            {
                string p = parts[i];

                // "[R]" ritual tag — not part of the searchable spell name; skip it.
                if (p == "[R]") { continue; }

                // Save/attack column (DEX 14, CON 11, WIS 14, --): marks end of source.
                if (Regex.IsMatch(p, @"^(DEX|CON|WIS|STR|INT|CHA)$") && i + 1 < parts.Count && Regex.IsMatch(parts[i + 1], @"^\d+$"))
                    break;
                if (p == "--" && foundSource) break;

                // Time column (1A, 1BA, 1R, 11m, 1A+10m): marks end of source.
                if (Regex.IsMatch(p, @"^\d+(?:A|BA|R|m)")) break;

                if (!foundSource)
                {
                    // If the accumulated tokens form a known source name, mark it.
                    // Known class/feat sources: Artificer, Svirfneblin Magic, Aberrant Dragonmark, etc.
                    // Since sources can be multi-word, we tentatively build source until a time token.
                    if (nameTokens.Count > 0 && IsLikelySourceStart(p))
                    {
                        foundSource = true;
                        sourceTokens.Add(p);
                    }
                    else
                    {
                        nameTokens.Add(p);
                    }
                }
                else
                {
                    sourceTokens.Add(p);
                }
            }

            spellName = string.Join(" ", nameTokens).Trim();
            source    = sourceTokens.Count > 0 ? string.Join(" ", sourceTokens).Trim() : null;

            if (!string.IsNullOrWhiteSpace(spellName))
            {
                sheet.Spells.Add(new ParsedSpell
                {
                    Name       = spellName,
                    Source     = source,
                    Level      = currentLevel,
                    PageRef    = pageRef,
                    IsPrepared = false, // open circle = not prepared (filled = prepared, but 2018 template rarely fills them)
                });
            }
        }
    }

    private static bool IsLikelySourceStart(string token)
    {
        // Source names start with a capital letter and are not common English words
        // that would appear in a spell name. This is a heuristic.
        if (string.IsNullOrEmpty(token) || !char.IsUpper(token[0])) return false;

        // Known class/source words that signal the start of the Source column.
        var knownSourceStarters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Artificer", "Svirfneblin", "Aberrant", "Bard", "Cleric", "Druid",
            "Fighter", "Monk", "Paladin", "Ranger", "Rogue", "Sorcerer",
            "Warlock", "Wizard", "Blood", "Order", "Domain", "Oath",
            "Subclass", "Race", "Feat", "Innate",
            // NOTE: "Magic" was here but it broke "Detect Magic", "Magic Missile", etc.
            // "Svirfneblin Magic" is already caught because "Svirfneblin" appears first.
        };
        return knownSourceStarters.Contains(token);
    }

    // ── Bio page ──────────────────────────────────────────────────────────────

    private static void ParseBioPage(List<PdfWord> words, ParsedCharacterSheet sheet)
    {
        string full = JoinWords(words);

        sheet.Gender    = ExtractLabeledValue(words, "GENDER",    aboveLabel: true);
        sheet.Age       = ExtractLabeledValue(words, "AGE",       aboveLabel: true);
        sheet.Size      = ExtractLabeledValue(words, "SIZE",      aboveLabel: true);
        sheet.Height    = ExtractLabeledValue(words, "HEIGHT",    aboveLabel: true);
        sheet.Weight    = ExtractLabeledValue(words, "WEIGHT",    aboveLabel: true);
        sheet.Alignment = ExtractLabeledValue(words, "ALIGNMENT", aboveLabel: true);
        sheet.Faith     = ExtractLabeledValue(words, "FAITH",     aboveLabel: true);
        sheet.SkinColor = ExtractLabeledValue(words, "SKIN",      aboveLabel: true);
        sheet.EyeColor  = ExtractLabeledValue(words, "EYES",      aboveLabel: true);
        sheet.HairColor = ExtractLabeledValue(words, "HAIR",      aboveLabel: true);

        sheet.PersonalityTraits ??= ExtractBoxContent(words, "PERSONALITY TRAITS");
        sheet.Ideals            ??= ExtractBoxContent(words, "IDEALS");
        sheet.Bonds             ??= ExtractBoxContent(words, "BONDS");
        sheet.Flaws             ??= ExtractBoxContent(words, "FLAWS");
        sheet.Backstory         ??= ExtractBoxContent(words, "CHARACTER BACKSTORY");
    }

    // ── Ability score helpers ─────────────────────────────────────────────────

    private static int? ExtractAbilityScore(List<PdfWord> words, string abilityLabel)
    {
        // The ability score boxes are on the far left of the page (x < 160).
        // The saving-throw section uses the same stat names (lowercase) but at x ≈ 245.
        // We must restrict to the left column to avoid picking up saving-throw labels.
        var labelWord = words.FirstOrDefault(w =>
            string.Equals(w.Text, abilityLabel, StringComparison.OrdinalIgnoreCase)
            && w.Left < 160);
        if (labelWord == null) return null;

        // In the D&D Beyond layout the label sits at the TOP of the ability box
        // and the score number appears just below it.
        // Look in a narrow horizontal band centered on the label, below it.
        var candidates = words
            .Where(w => w.Left >= labelWord.Left - 35
                     && w.Right <= labelWord.Right + 35
                     && w.Top > labelWord.Top
                     && w.Top <= labelWord.Top + 80)
            .OrderBy(w => w.Top)
            .ToList();

        foreach (var c in candidates)
        {
            string t = c.Text.TrimStart('+');
            if (int.TryParse(t, out int v) && v is >= 1 and <= 30) return v;
        }

        // Fallback: try above the label (some template versions place the score above).
        candidates = words
            .Where(w => w.Left >= labelWord.Left - 35
                     && w.Right <= labelWord.Right + 35
                     && w.Top < labelWord.Top
                     && w.Top >= labelWord.Top - 80)
            .OrderByDescending(w => w.Top)
            .ToList();

        foreach (var c in candidates)
        {
            string t = c.Text.TrimStart('+');
            if (int.TryParse(t, out int v) && v is >= 1 and <= 30) return v;
        }

        return null;
    }

    // ── Saving throw / skill proficiency helpers ──────────────────────────────

    private static bool IsSaveProficient(List<PdfWord> words, string statName, bool savingThrowRegion)
    {
        // D&D Beyond renders a filled bullet "●" or similar glyph to the left of proficient entries.
        // In the PDF text stream the bullet is typically a non-ASCII character or the Unicode
        // "BLACK CIRCLE" (U+25CF) preceding the stat name.
        // We look for the stat name in the saving throws region (left ~160–320, top ~180–420)
        // and check if the word immediately to its left is a filled circle.
        var statWord = words
            .Where(w => string.Equals(w.Text.Trim(), statName, StringComparison.OrdinalIgnoreCase)
                     && w.Left > 140 && w.Left < 340
                     && w.Top > 150 && w.Top < 430)
            .FirstOrDefault();
        if (statWord == null) return false;

        return HasFilledBulletBefore(words, statWord);
    }

    private static bool IsSkillProficient(List<PdfWord> words, string skillName)
    {
        // Skills appear in a region roughly x:140–340, y:430–720.
        var skillWord = words
            .Where(w => string.Equals(w.Text.Trim(), skillName, StringComparison.OrdinalIgnoreCase)
                     && w.Left > 140 && w.Left < 360
                     && w.Top > 420 && w.Top < 750)
            .FirstOrDefault();
        if (skillWord == null) return false;

        return HasFilledBulletBefore(words, skillWord);
    }

    private static bool HasFilledBulletBefore(List<PdfWord> words, PdfWord target)
    {
        // Look for a word immediately to the left of target on the same line.
        var before = words
            .Where(w => w.Right <= target.Left + 4
                     && w.Right >= target.Left - 25
                     && Math.Abs(w.Top - target.Top) < 6)
            .OrderByDescending(w => w.Right)
            .FirstOrDefault();
        if (before == null) return false;

        // Filled bullet glyphs include ●, ✓, filled circle variants.
        // D&D Beyond uses a custom font so the glyph may render as a non-letter.
        return IsFilledCircleGlyph(before.Text);
    }

    private static bool IsFilledCircleGlyph(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // Unicode filled circles / bullets used by D&D Beyond.
        return text.Any(c => c == '●' || c == '•' || c == '●' || c == '•'
                          || c == '◉' || c == '✓' || c == '✔');
    }

    // ── Passive score helpers ─────────────────────────────────────────────────

    private static int? ExtractPassive(List<PdfWord> words, string label)
    {
        // The passive score is the number to the left of the label text.
        var labelWords = words
            .Where(w => w.Text.Contains(label.Split(' ')[1], StringComparison.OrdinalIgnoreCase)
                     && w.Left < 350)
            .FirstOrDefault();
        if (labelWords == null) return null;

        var numWord = words
            .Where(w => w.Right <= labelWords.Left + 5
                     && w.Right >= labelWords.Left - 60
                     && Math.Abs(w.Top - labelWords.Top) < 10)
            .OrderByDescending(w => w.Right)
            .FirstOrDefault();

        if (numWord != null && int.TryParse(numWord.Text, out int v)) return v;
        return null;
    }

    // ── Generic positional helpers ────────────────────────────────────────────

    /// <summary>Returns the text value that appears immediately above a given label word.</summary>
    private static string? ExtractLabeledValue(List<PdfWord> words, string label, bool aboveLabel)
    {
        // Find the label word(s).
        var labelWords = FindLabel(words, label);
        if (labelWords.Count == 0) return null;

        double labelTop   = labelWords.Min(w => w.Top);
        double labelLeft  = labelWords.Min(w => w.Left);
        double labelRight = labelWords.Max(w => w.Right);

        if (aboveLabel)
        {
            // Value is in the row above the label within a similar horizontal span.
            var valueWords = words
                .Where(w => w.Top < labelTop
                         && w.Top >= labelTop - 50
                         && w.Right >= labelLeft - 20
                         && w.Left <= labelRight + 20)
                .OrderBy(w => w.Left)
                .ToList();
            return valueWords.Count > 0 ? string.Join(" ", valueWords.Select(w => w.Text)) : null;
        }

        return null;
    }

    private static int? ExtractIntNear(List<PdfWord> words, string label, double regionTop, double regionBottom)
    {
        var labelWord = words.FirstOrDefault(w =>
            w.Text.Contains(label, StringComparison.OrdinalIgnoreCase)
            && w.Top >= regionTop && w.Top <= regionBottom);
        if (labelWord == null) return null;

        var candidates = words
            .Where(w => Math.Abs(w.Left - labelWord.Left) < 80
                     && w.Top >= regionTop && w.Top <= regionBottom)
            .ToList();

        foreach (var c in candidates.OrderBy(w => Math.Abs(w.Top - labelWord.Top)))
        {
            string t = c.Text.TrimStart('+');
            if (int.TryParse(t, out int v)) return v;
        }
        return null;
    }

    private static string? ExtractTextNear(List<PdfWord> words, string label, double regionTop, double regionBottom)
    {
        var labelWord = words.FirstOrDefault(w =>
            w.Text.Contains(label, StringComparison.OrdinalIgnoreCase)
            && w.Top >= regionTop && w.Top <= regionBottom);
        if (labelWord == null) return null;

        var nearby = words
            .Where(w => Math.Abs(w.Left - labelWord.Left) < 100
                     && w.Top >= regionTop && w.Top <= regionBottom
                     && !w.Text.Equals(labelWord.Text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w.Top)
            .ThenBy(w => w.Left)
            .Take(6)
            .ToList();

        return nearby.Count > 0 ? string.Join(" ", nearby.Select(w => w.Text)) : null;
    }

    /// <summary>
    /// Fallback HP extraction for OCR imports where "Max HP" comes as separate words.
    /// Finds "Max" or "MAXIMUM" in the centre column (x ≈ 300–750 DIP, y ≈ 80–600 DIP) and
    /// looks for a positive integer in the same neighbourhood.
    /// </summary>
    private static int? ExtractMaxHpFromOcr(List<PdfWord> words)
    {
        var maxWord = words.FirstOrDefault(w =>
            (string.Equals(w.Text, "Max",     StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.Text, "MAXIMUM", StringComparison.OrdinalIgnoreCase))
            && w.Left >= 300 && w.Right <= 800
            && w.Top  >= 80  && w.Top   <= 600);
        if (maxWord == null) return null;

        double cx = (maxWord.Left + maxWord.Right) / 2;
        var candidates = words
            .Where(w => w.Top  >= maxWord.Top
                     && w.Top  <= maxWord.Top + 120
                     && Math.Abs((w.Left + w.Right) / 2 - cx) < 100)
            .OrderBy(w => w.Top);

        foreach (var c in candidates)
        {
            string t = c.Text.TrimStart('+');
            if (int.TryParse(t, out int v) && v is >= 1 and <= 999) return v;
        }
        return null;
    }

    private static int? ExtractInt(List<PdfWord> words, string label, bool rightOf, bool below)
    {
        var labelWord = words.FirstOrDefault(w =>
            w.Text.Contains(label, StringComparison.OrdinalIgnoreCase));
        if (labelWord == null) return null;

        IEnumerable<PdfWord> candidates;
        if (below)
            candidates = words.Where(w => w.Top > labelWord.Top && w.Top < labelWord.Top + 60
                                       && Math.Abs((w.Left + w.Right) / 2 - (labelWord.Left + labelWord.Right) / 2) < 60);
        else
            candidates = words.Where(w => w.Left > labelWord.Right && w.Left < labelWord.Right + 80
                                       && Math.Abs(w.Top - labelWord.Top) < 10);

        foreach (var c in candidates.OrderBy(w => w.Top).ThenBy(w => w.Left))
        {
            string t = c.Text.TrimStart('+');
            if (int.TryParse(t, out int v)) return v;
        }
        return null;
    }

    private static string? ExtractBoxContent(List<PdfWord> words, string boxLabel)
    {
        var labelWord = words.FirstOrDefault(w =>
            w.Text.Contains(boxLabel.Split(' ').Last(), StringComparison.OrdinalIgnoreCase));
        if (labelWord == null) return null;

        // Content is above the label in the same horizontal band.
        var contentWords = words
            .Where(w => w.Top < labelWord.Top
                     && w.Top >= labelWord.Top - 200
                     && w.Left >= labelWord.Left - 10
                     && w.Right <= labelWord.Right + 200)
            .OrderBy(w => w.Top)
            .ThenBy(w => w.Left)
            .ToList();

        return contentWords.Count > 0 ? string.Join(" ", contentWords.Select(w => w.Text)) : null;
    }

    // ── Word joining helpers ──────────────────────────────────────────────────

    private static string JoinWords(List<PdfWord> words) =>
        string.Join(" ", words.Select(w => w.Text));

    private static string JoinWordsInRegion(List<PdfWord> words,
        double leftMin, double leftMax, double topMin, double topMax)
    {
        var filtered = words
            .Where(w => w.Left >= leftMin && w.Right <= leftMax
                     && w.Top  >= topMin  && w.Top  <= topMax)
            .OrderBy(w => w.Top)
            .ToList();

        // Cluster into visual lines: words within 6pt vertically belong to the same line.
        // Sort each cluster by Left so OCR words with slightly different y-values (e.g.
        // "=== LANGUAGES ===" where LANGUAGES is 3pt higher than the === markers) still
        // join in correct left-to-right order.
        var lines = new List<List<PdfWord>>();
        foreach (var w in filtered)
        {
            if (lines.Count == 0 || w.Top - lines[^1][0].Top > 6)
                lines.Add([w]);
            else
                lines[^1].Add(w);
        }

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(string.Join(" ", line.OrderBy(w => w.Left).Select(w => w.Text)));
        }
        return sb.ToString();
    }

    private static List<PdfWord> FindLabel(List<PdfWord> words, string label)
    {
        // Multi-word labels like "CLASS & LEVEL" may be split across words.
        string[] parts = label.Split(' ');
        if (parts.Length == 1)
            return words.Where(w => string.Equals(w.Text, label, StringComparison.OrdinalIgnoreCase)).ToList();

        // Find first word of label then collect adjacent words.
        for (int i = 0; i < words.Count; i++)
        {
            if (!string.Equals(words[i].Text, parts[0], StringComparison.OrdinalIgnoreCase)) continue;
            if (i + parts.Length - 1 >= words.Count) continue;
            bool match = true;
            for (int j = 1; j < parts.Length; j++)
            {
                if (!string.Equals(words[i + j].Text, parts[j], StringComparison.OrdinalIgnoreCase))
                { match = false; break; }
            }
            if (match) return words.Skip(i).Take(parts.Length).ToList();
        }
        return [];
    }

    private static string? ExtractBeforeLabel(string text, string label)
    {
        int idx = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx <= 0) return null;
        // Grab the last non-empty token before the label.
        string before = text[..idx].TrimEnd();
        int lastSpace = before.LastIndexOf(' ');
        return lastSpace >= 0 ? before[(lastSpace + 1)..].Trim() : before.Trim();
    }

    // ── Template version detection ────────────────────────────────────────────

    private static string? DetectTemplateVersion(List<PdfWord> words)
    {
        string text = JoinWords(words);
        var m = Regex.Match(text, @"©\s*(\d{4})\s*D&D Beyond", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── Skill name list ───────────────────────────────────────────────────────

    private static readonly string[] AllSkills =
    [
        "Acrobatics", "Animal Handling", "Arcana", "Athletics",
        "Deception", "History", "Insight", "Intimidation",
        "Investigation", "Medicine", "Nature", "Perception",
        "Performance", "Persuasion", "Religion", "Sleight of Hand",
        "Stealth", "Survival",
    ];
}

// ── Internal word model ───────────────────────────────────────────────────────

internal sealed record PdfWord(string Text, double Left, double Top, double Right, double Bottom);
