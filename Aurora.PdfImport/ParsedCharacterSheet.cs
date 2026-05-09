namespace Aurora.PdfImport;

/// <summary>
/// Raw data extracted from a D&amp;D Beyond PDF export, before any element inference.
/// All fields are nullable — missing/blank fields on the sheet stay null.
/// </summary>
public sealed class ParsedCharacterSheet
{
    // ── Header ───────────────────────────────────────────────────────────────
    public string? CharacterName  { get; set; }
    public string? PlayerName     { get; set; }
    public string? ClassName      { get; set; }  // e.g. "Blood Hunter (archived)"
    public int?    ClassLevel      { get; set; }
    public string? Species         { get; set; }  // e.g. "Human", "Custom Lineage"
    public string? Background      { get; set; }  // e.g. "Haunted One", "Custom Background"
    public string? ExperiencePoints { get; set; } // "900" or "(Milestone)"

    // ── Ability scores ───────────────────────────────────────────────────────
    public int? Strength     { get; set; }
    public int? Dexterity    { get; set; }
    public int? Constitution { get; set; }
    public int? Intelligence { get; set; }
    public int? Wisdom       { get; set; }
    public int? Charisma     { get; set; }

    // ── Combat ───────────────────────────────────────────────────────────────
    public int? MaxHitPoints      { get; set; }
    public int? ArmorClass        { get; set; }
    public int? Initiative        { get; set; }
    public int? ProficiencyBonus  { get; set; }
    public string? Speed          { get; set; }   // "40 ft. (Walking)"
    public string? HitDice        { get; set; }   // "8d10"

    // ── Saving throw proficiencies (true = proficient) ───────────────────────
    public bool SaveStrength     { get; set; }
    public bool SaveDexterity    { get; set; }
    public bool SaveConstitution { get; set; }
    public bool SaveIntelligence { get; set; }
    public bool SaveWisdom       { get; set; }
    public bool SaveCharisma     { get; set; }

    // ── Skill proficiencies ──────────────────────────────────────────────────
    public HashSet<string> ProficientSkills { get; set; } = [];  // e.g. "Acrobatics", "Arcana"

    // ── Passive scores ───────────────────────────────────────────────────────
    public int? PassivePerception    { get; set; }
    public int? PassiveInsight       { get; set; }
    public int? PassiveInvestigation { get; set; }

    // ── Proficiencies & training (raw text blocks) ───────────────────────────
    public List<string> ArmorProficiencies   { get; set; } = [];
    public List<string> WeaponProficiencies  { get; set; } = [];
    public List<string> ToolProficiencies    { get; set; } = [];
    public List<string> Languages            { get; set; } = [];
    public List<string> Senses              { get; set; } = [];

    // ── Features & feats ─────────────────────────────────────────────────────
    public List<ParsedFeature> Features { get; set; } = [];
    public List<ParsedFeature> Feats    { get; set; } = [];

    // ── Spells ───────────────────────────────────────────────────────────────
    public string? SpellcastingClass  { get; set; }
    public string? SpellcastingAbility { get; set; }
    public int?    SpellSaveDc        { get; set; }
    public int?    SpellAttackBonus   { get; set; }
    public List<ParsedSpell> Spells   { get; set; } = [];

    // ── Equipment ────────────────────────────────────────────────────────────
    public List<ParsedItem> Equipment { get; set; } = [];

    // ── Appearance / biography ───────────────────────────────────────────────
    public string? Gender            { get; set; }
    public string? Age               { get; set; }
    public string? Size              { get; set; }
    public string? Height            { get; set; }
    public string? Weight            { get; set; }
    public string? Alignment         { get; set; }
    public string? Faith             { get; set; }
    public string? SkinColor         { get; set; }
    public string? EyeColor          { get; set; }
    public string? HairColor         { get; set; }
    public string? PersonalityTraits { get; set; }
    public string? Ideals            { get; set; }
    public string? Bonds             { get; set; }
    public string? Flaws             { get; set; }
    public string? Backstory         { get; set; }

    // ── Portrait ─────────────────────────────────────────────────────────────
    public string? PortraitUrl       { get; set; }  // remote URL from decorations.avatarUrl
    public string? PortraitLocalPath { get; set; }  // downloaded local file path (set by service)

    // ── Choices resolved from D&D Beyond choiceDefinitions ───────────────────
    // Names that didn't map to a known proficiency category; the inference
    // engine tries them against multiple element types (Class Feature, etc.).
    public List<string> ImportChoices { get; set; } = [];

    // ── Import metadata ──────────────────────────────────────────────────────
    public string? TemplateVersion { get; set; }  // "2018" etc.

    // ── Parser diagnostics (written to %TEMP%\aurora_pdf_debug.txt and here) ─
    public string? ParseDiagnostics    { get; set; }

    /// <summary>True when the source file doesn't match a recognised D&amp;D Beyond layout.</summary>
    public bool IsUnsupportedFormat { get; set; }
}

public sealed class ParsedFeature
{
    public string  Name       { get; set; } = "";
    public string? Source     { get; set; }  // e.g. "TCoE", "PHB"
    public string? SourcePage { get; set; }  // e.g. "12"
    public string? Description { get; set; }
    public bool    IsSubFeature { get; set; } // true for "|" prefix lines
}

public sealed class ParsedSpell
{
    public string  Name      { get; set; } = "";
    public string? Source    { get; set; }  // e.g. "Artificer", "Svirfneblin Magic", "Aberrant Dragonmark"
    public int?    Level     { get; set; }  // null = cantrip
    public string? SaveOrAtk { get; set; }
    public string? PageRef   { get; set; }  // e.g. "PHB 275", "PHB-2024 271"
    public bool    IsPrepared { get; set; }
}

public sealed class ParsedItem
{
    public string  Name     { get; set; } = "";
    public int     Quantity { get; set; } = 1;
    public string? Weight   { get; set; }
    public bool    Attuned  { get; set; }
}
