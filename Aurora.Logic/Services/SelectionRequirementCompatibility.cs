namespace Builder.Presentation.Services;

/// <summary>
/// Adds cross-edition identifiers that are mechanically equivalent when validating
/// selection requirements. This keeps older firearm-proficiency grants compatible
/// with the 2024 weapon-mastery entries that require the newer proficiency IDs.
/// </summary>
public static class SelectionRequirementCompatibility
{
    public const string GunnerFeatId = "ID_WOTC_TCOE_FEAT_GUNNER";
    public const string FirearmsProficiencyId = "ID_WOTC_DMG_PROFICIENCY_WEAPON_FIREARMS";
    public const string LegacyPistolProficiencyId =
        "ID_WOTC_DMG_PROFICIENCY_WEAPON_RENAISSANCE_FIREARMS_PISTOL";
    public const string LegacyMusketProficiencyId =
        "ID_WOTC_DMG_PROFICIENCY_WEAPON_RENAISSANCE_FIREARMS_MUSKET";
    public const string Pistol2024ProficiencyId =
        "ID_WOTC_PHB24_PROFICIENCY_WEAPON_PROFICIENCY_PISTOL";
    public const string Musket2024ProficiencyId =
        "ID_WOTC_PHB24_PROFICIENCY_WEAPON_PROFICIENCY_MUSKET";

    public static IReadOnlySet<string> ExpandForSelectionValidation(IEnumerable<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(elementIds);

        var expanded = elementIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool hasBroadFirearmProficiency =
            expanded.Contains(GunnerFeatId) ||
            expanded.Contains(FirearmsProficiencyId);

        if (hasBroadFirearmProficiency || expanded.Contains(LegacyPistolProficiencyId))
            expanded.Add(Pistol2024ProficiencyId);

        if (hasBroadFirearmProficiency || expanded.Contains(LegacyMusketProficiencyId))
            expanded.Add(Musket2024ProficiencyId);

        return expanded;
    }
}
