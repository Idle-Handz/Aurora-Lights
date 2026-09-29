using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Aurora.Components.Models;

public sealed record CompendiumEntryModel(
    string Id,
    string Name,
    string Type,
    string Source,
    string Summary,
    string DescriptionHtml,
    string SearchText,
    int? SpellLevel,
    string SpellSchool,
    IReadOnlyList<string> SpellClasses,
    string ItemRarity,
    bool RequiresAttunement,
    string DisplayWeight,
    string DisplayPrice,
    string ItemDamage,
    string ItemRange,
    string ItemProperties,
    string CreatureType,
    string CreatureSize,
    string ChallengeText,
    string SpellCastingTime,
    string SpellRange,
    string SpellComponents,
    string SpellDuration,
    bool SpellIsConcentration,
    bool SpellIsRitual,
    bool HasComputedDetail)
{
    public string SpellLevelLabel => SpellLevel switch
    {
        null => string.Empty,
        0 => "Cantrip",
        int level => level.ToString(CultureInfo.InvariantCulture)
    };

    public bool IsItemLike => CompendiumFilter.IsItemLike(Type);
    public bool HasItemDetails =>
        !string.IsNullOrWhiteSpace(ItemRarity) ||
        RequiresAttunement ||
        !string.IsNullOrWhiteSpace(DisplayWeight) ||
        !string.IsNullOrWhiteSpace(DisplayPrice) ||
        !string.IsNullOrWhiteSpace(ItemDamage) ||
        !string.IsNullOrWhiteSpace(ItemRange) ||
        !string.IsNullOrWhiteSpace(ItemProperties);
    public bool IsCompanionLike => Type.StartsWith("Companion", StringComparison.OrdinalIgnoreCase);
    public string CompanionAlignment { get; init; } = string.Empty;
    public string CompanionArmorClass { get; init; } = string.Empty;
    public string CompanionHitPoints { get; init; } = string.Empty;
    public string CompanionSpeed { get; init; } = string.Empty;
    public string CompanionStrength { get; init; } = string.Empty;
    public string CompanionDexterity { get; init; } = string.Empty;
    public string CompanionConstitution { get; init; } = string.Empty;
    public string CompanionIntelligence { get; init; } = string.Empty;
    public string CompanionWisdom { get; init; } = string.Empty;
    public string CompanionCharisma { get; init; } = string.Empty;
    public string CompanionSkills { get; init; } = string.Empty;
    public string CompanionResistances { get; init; } = string.Empty;
    public string CompanionImmunities { get; init; } = string.Empty;
    public string CompanionConditionImmunities { get; init; } = string.Empty;
    public string CompanionSenses { get; init; } = string.Empty;
    public string CompanionLanguages { get; init; } = string.Empty;
    public string CompanionProficiencyBonus { get; init; } = string.Empty;
    public IReadOnlyList<CompendiumLinkedEntryModel> CompanionTraits { get; init; } = [];
    public IReadOnlyList<CompendiumLinkedEntryModel> CompanionActions { get; init; } = [];
    public IReadOnlyList<CompendiumLinkedEntryModel> CompanionReactions { get; init; } = [];
    public IReadOnlyList<CompendiumLinkedEntryModel> InformationDetails { get; init; } = [];
    public bool HasCompanionStatDetails =>
        !string.IsNullOrWhiteSpace(CreatureType) ||
        !string.IsNullOrWhiteSpace(CreatureSize) ||
        !string.IsNullOrWhiteSpace(ChallengeText) ||
        !string.IsNullOrWhiteSpace(CompanionAlignment) ||
        !string.IsNullOrWhiteSpace(CompanionArmorClass) ||
        !string.IsNullOrWhiteSpace(CompanionHitPoints) ||
        !string.IsNullOrWhiteSpace(CompanionSpeed) ||
        !string.IsNullOrWhiteSpace(CompanionSkills) ||
        !string.IsNullOrWhiteSpace(CompanionResistances) ||
        !string.IsNullOrWhiteSpace(CompanionImmunities) ||
        !string.IsNullOrWhiteSpace(CompanionConditionImmunities) ||
        !string.IsNullOrWhiteSpace(CompanionSenses) ||
        !string.IsNullOrWhiteSpace(CompanionLanguages) ||
        !string.IsNullOrWhiteSpace(CompanionProficiencyBonus) ||
        HasCompanionAbilityDetails;
    public bool HasCompanionAbilityDetails =>
        !string.IsNullOrWhiteSpace(CompanionStrength) ||
        !string.IsNullOrWhiteSpace(CompanionDexterity) ||
        !string.IsNullOrWhiteSpace(CompanionConstitution) ||
        !string.IsNullOrWhiteSpace(CompanionIntelligence) ||
        !string.IsNullOrWhiteSpace(CompanionWisdom) ||
        !string.IsNullOrWhiteSpace(CompanionCharisma);
    public bool HasCompanionLinkedDetails =>
        CompanionTraits.Count > 0 ||
        CompanionActions.Count > 0 ||
        CompanionReactions.Count > 0;
    public bool HasCompanionDetails => HasCompanionStatDetails || HasCompanionLinkedDetails;
    public bool HasSpellPropertyDetails =>
        !string.IsNullOrWhiteSpace(SpellCastingTime) ||
        !string.IsNullOrWhiteSpace(SpellRange) ||
        !string.IsNullOrWhiteSpace(SpellComponents) ||
        !string.IsNullOrWhiteSpace(SpellDuration);
    public bool HasSpellDetails => HasSpellPropertyDetails || SpellIsConcentration || SpellIsRitual;
    public string SearchKey { get; init; } = CompendiumFilter.NormalizeSearchKey(SearchText);

    /// <summary>
    /// Replaces the searchable text and the key derived from it together. A with-expression does
    /// not re-run the key's initializer, so setting the text alone would leave the old key behind,
    /// and setting the key by hand is how the two drifted apart: entries holding a typographic
    /// apostrophe became unreachable by a query that had folded its own to a straight one.
    /// </summary>
    /// <summary>
    /// Rebuilds the search text from this entry's own searchable fields, plus whatever prose the
    /// caller holds that is not a field of its own - a plain-text description, the text of linked
    /// entries. Five places used to spell this list out by hand, two of them character for
    /// character, so a newly searchable field had to be added to all of them or an entry became
    /// findable in one state and not another.
    /// </summary>
    public CompendiumEntryModel WithSearchTextFrom(params string?[] prose) =>
        WithSearchText(string.Join(" ", prose.Concat(SearchableFields())
            .Where(part => !string.IsNullOrWhiteSpace(part))));

    private IEnumerable<string?> SearchableFields() =>
    [
        Name, Type, Source,
        SpellSchool, SpellCastingTime, SpellRange, SpellDuration, SpellComponents,
        string.Join(" ", SpellClasses),
        ItemRarity, DisplayWeight, DisplayPrice, ItemDamage, ItemRange, ItemProperties,
        CreatureType, CreatureSize, ChallengeText,
        CompanionAlignment, CompanionArmorClass, CompanionHitPoints, CompanionSpeed,
        CompanionStrength, CompanionDexterity, CompanionConstitution,
        CompanionIntelligence, CompanionWisdom, CompanionCharisma,
        CompanionSkills, CompanionResistances, CompanionImmunities,
        CompanionConditionImmunities, CompanionSenses, CompanionLanguages,
        CompanionProficiencyBonus
    ];

    public CompendiumEntryModel WithSearchText(string? text) => this with
    {
        SearchText = text ?? string.Empty,
        SearchKey = CompendiumFilter.NormalizeSearchKey(text)
    };
}

public sealed record CompendiumLinkedEntryModel(
    string Id,
    string Name,
    string Type,
    string DescriptionHtml);
