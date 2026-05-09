namespace Aurora.PdfImport;

/// <summary>
/// Maps D&amp;D Beyond source abbreviations to full package names used in Aurora element IDs.
/// </summary>
public static class SourceAbbreviations
{
    /// <summary>
    /// Full name keyed by the abbreviation that appears in D&amp;D Beyond PDFs
    /// (in "* Feature • ABBREV page" and spell PAGE REF column).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ToFullName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PHB"]      = "Player's Handbook",
            ["PHB-2024"] = "Player's Handbook 2024",
            ["BR"]       = "Basic Rules",
            ["SRD"]      = "Systems Reference Document",
            ["TCoE"]     = "Tasha's Cauldron of Everything",
            ["XGtE"]     = "Xanathar's Guide to Everything",
            ["MToF"]     = "Mordenkainen's Tome of Foes",
            ["VGtM"]     = "Volo's Guide to Monsters",
            ["EE"]       = "Elemental Evil Player's Companion",
            ["SCAG"]     = "Sword Coast Adventurer's Guide",
            ["GGtR"]     = "Guildmasters' Guide to Ravnica",
            ["WGtE"]     = "Wayfinder's Guide to Eberron",
            ["ERftLW"]   = "Eberron: Rising from the Last War",
            ["EGtW"]     = "Explorer's Guide to Wildemount",
            ["MOoT"]     = "Mythic Odysseys of Theros",
            ["CoS"]      = "Curse of Strahd",
            ["IDRotF"]   = "Icewind Dale: Rime of the Frostmaiden",
            ["TCE"]      = "Tasha's Cauldron of Everything",   // alternate abbreviation
            ["FToD"]     = "Fizban's Treasury of Dragons",
            ["SCoC"]     = "Strixhaven: A Curriculum of Chaos",
            ["MotM"]     = "Monsters of the Multiverse",
            ["BGDIA"]    = "Baldur's Gate: Descent into Avernus",
            ["DMG"]      = "Dungeon Master's Guide",
        };

    /// <summary>
    /// Extracts the source abbreviation from a page-ref string like "PHB 275" or "PHB-2024 271" or "EE 150".
    /// Returns null if the format is not recognised.
    /// </summary>
    public static string? ParseAbbrev(string pageRef)
    {
        if (string.IsNullOrWhiteSpace(pageRef)) return null;
        int space = pageRef.LastIndexOf(' ');
        string abbrev = space > 0 ? pageRef[..space].Trim() : pageRef.Trim();
        return ToFullName.ContainsKey(abbrev) ? abbrev : null;
    }
}
