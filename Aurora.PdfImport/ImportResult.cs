namespace Aurora.PdfImport;

/// <summary>
/// Result returned by <see cref="CharacterInferenceEngine"/>.
/// Contains resolved element IDs and warnings for the import UI.
/// </summary>
public sealed class ImportResult
{
    /// <summary>All element selections that could be resolved to aurora IDs.</summary>
    public List<ResolvedElement> Elements { get; set; } = [];

    /// <summary>Items that could not be resolved — presented to the user after import.</summary>
    public List<ImportWarning> Warnings { get; set; } = [];

    /// <summary>Items where multiple DB matches were found; UI should let user choose.</summary>
    public List<ImportAmbiguity> Ambiguities { get; set; } = [];

    // ── Flat character data (written directly to the XML, not as elements) ───

    public string CharacterName  { get; set; } = "";
    public string PlayerName     { get; set; } = "";
    public string Gender         { get; set; } = "";
    public string Age            { get; set; } = "";
    public string Height         { get; set; } = "";
    public string Weight         { get; set; } = "";
    public string Eyes           { get; set; } = "";
    public string Skin           { get; set; } = "";
    public string Hair           { get; set; } = "";
    public string Alignment      { get; set; } = "";
    public string Backstory      { get; set; } = "";
    public string PersonalityTraits { get; set; } = "";
    public string Ideals         { get; set; } = "";
    public string Bonds          { get; set; } = "";
    public string Flaws          { get; set; } = "";

    public int Strength     { get; set; } = 8;
    public int Dexterity    { get; set; } = 8;
    public int Constitution { get; set; } = 8;
    public int Intelligence { get; set; } = 8;
    public int Wisdom       { get; set; } = 8;
    public int Charisma     { get; set; } = 8;

    public int Level { get; set; } = 1;

    // Display properties (for the file header).
    public string DisplayRace       { get; set; } = "";
    public string DisplayClass      { get; set; } = "";
    public string DisplayBackground { get; set; } = "";

    /// <summary>
    /// Short diagnostic string from the PDF parser — shown in the Review dialog
    /// to help diagnose extraction failures. Full details in %TEMP%\aurora_pdf_debug.txt.
    /// </summary>
    public string? ParseDiagnostics { get; set; }

    /// <summary>
    /// Runtime import diagnostics produced while applying resolved elements to a live Aurora character.
    /// </summary>
    public List<string> ImportDiagnostics { get; set; } = [];
}

public sealed record ResolvedElement
{
    public string AuroraId   { get; set; } = "";
    public string TypeName   { get; set; } = "";   // "Race", "Class", "Spell", "Feat", etc.
    public string Name       { get; set; } = "";
    public string? PackageName { get; set; }
    public int    Level      { get; set; } = 1;    // level at which this was selected
}

public sealed class ImportWarning
{
    public string Category { get; set; } = "";   // "Race", "Feat", "Spell", etc.
    public string Item     { get; set; } = "";   // the name that couldn't be resolved
    public string Reason   { get; set; } = "";
}

public sealed class ImportAmbiguity
{
    public string Category   { get; set; } = "";
    public string Item       { get; set; } = "";
    public List<ResolvedElement> Candidates { get; set; } = [];

    /// <summary>Set by the UI after the user picks one.</summary>
    public ResolvedElement? Chosen { get; set; }
}
