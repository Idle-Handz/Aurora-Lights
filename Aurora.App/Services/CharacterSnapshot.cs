using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;

namespace Aurora.App.Services;

/// <summary>
/// Snapshot of character display data, captured once at load time and stored
/// per-tab. Editable (non-calculated) properties use <c>set</c> so the UI can
/// mutate them in-place; the tab's IsDirty flag is set by the page on any change.
/// Calculated properties (stats derived from registered elements) use <c>init</c>
/// and are never written back by the UI.
/// </summary>
public sealed class CharacterSnapshot
{
    // ── Identity (mixed: Name/PlayerName/etc. editable; Race/Class/etc. from elements) ──
    public string Name       { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int    Level      { get; init; }
    public int    Experience { get; set; }
    public string Race       { get; init; } = "";
    public string Class      { get; init; } = "";
    public string Archetype  { get; init; } = "";
    public string Background { get; init; } = "";
    public string Alignment  { get; set; } = "";
    public string Deity      { get; set; } = "";
    public string Dragonmark { get; init; } = "";

    // ── Combat (recalculated when items are equipped) ──
    public int ArmorClass  { get; set; }
    public int MaxHp       { get; set; }
    public int Speed       { get; set; }
    public int Initiative  { get; set; }
    public int Proficiency { get; init; }

    // ── Computed collections (calculated, read-only) ──
    public IReadOnlyList<AbilityEntry>     Abilities    { get; init; } = [];
    public IReadOnlyList<SavingThrowEntry> SavingThrows { get; init; } = [];
    public IReadOnlyList<SkillEntry>       Skills       { get; init; } = [];

    // ── Narrative / appearance (all editable) ──
    public string Backstory { get; set; } = "";
    public string Age       { get; set; } = "";
    public string Height    { get; set; } = "";
    public string Weight    { get; set; } = "";
    public string Eyes      { get; set; } = "";
    public string Hair      { get; set; } = "";
    public string Skin      { get; set; } = "";
    public string Gender    { get; set; } = "";
    public string Notes1    { get; set; } = "";
    public string Notes2    { get; set; } = "";

    // ── Build (calculated, read-only) ──
    public string BackgroundFeatureName        { get; init; } = "";
    public string BackgroundFeatureDescription { get; init; } = "";
    public string AdditionalFeatures           { get; init; } = "";

    // ── Affiliations (editable) ──
    public string Organisation { get; set; } = "";
    public string Allies       { get; set; } = "";
    public string Trinket      { get; set; } = "";

    // ── Equipment (coins editable; item list refreshed on equip/attune) ──
    public IReadOnlyList<EquipmentItemEntry> InventoryItems { get; set; } = [];
    public IReadOnlyList<AttackEntry>        Attacks        { get; set; } = [];
    public long   CoinCopper              { get; set; }
    public long   CoinSilver             { get; set; }
    public long   CoinElectrum           { get; set; }
    public long   CoinGold               { get; set; }
    public long   CoinPlatinum           { get; set; }
    public int    AttunedCount           { get; set; }
    public int    AttunedMax             { get; set; }
    public string InventoryEquipmentText { get; set; } = "";
    public string InventoryTreasureText  { get; set; } = "";
    public string InventoryQuestText     { get; set; } = "";

    // ── Spellcasting (stats calculated; IsPrepared per spell is editable) ──
    public bool   HasSpellcasting        { get; init; }
    /// <summary>True for Cleric/Druid/Paladin/Wizard/Artificer; false for Sorcerer/Bard/Warlock/Ranger.</summary>
    public bool   IsSpellcasterPrepared  { get; init; }
    public string SpellcastingClass      { get; init; } = "";
    public string SpellcastingAbility    { get; init; } = "";
    public string SpellcastingDC         { get; init; } = "";
    public string SpellcastingAttack     { get; init; } = "";
    public IReadOnlyList<string>          Cantrips     { get; init; } = [];
    public IReadOnlyList<SpellLevelEntry> SpellLevels  { get; init; } = [];
    public IReadOnlyList<SpellcastingSectionEntry> SpellcastingSections { get; init; } = [];
    /// <summary>Maximum number of spells the character may have prepared (0 for known casters).</summary>
    public int MaxPrepared { get; init; }
    /// <summary>Count of spells currently prepared by choice (excludes always-prepared).</summary>
    public int PreparedCount => SpellLevels.SelectMany(l => l.Spells).Count(s => s.IsPrepared && !s.IsAlwaysPrepared);

    // ── Proficiencies and languages (calculated) ──
    public IReadOnlyList<string> Languages           { get; init; } = [];
    public IReadOnlyList<string> ArmorProficiencies  { get; init; } = [];
    public IReadOnlyList<string> WeaponProficiencies { get; init; } = [];
    public IReadOnlyList<string> ToolProficiencies   { get; init; } = [];

    // ── Race features and alternate movement (calculated) ──
    public IReadOnlyList<FeatureEntry> RaceFeatures { get; init; } = [];
    public int SpeedFly    { get; init; }
    public int SpeedClimb  { get; init; }
    public int SpeedSwim   { get; init; }
    public int SpeedBurrow { get; init; }

    // ── Companion (calculated; null when no companion is active) ──
    public CompanionSnapshot? Companion { get; init; }
    public bool HasCompanion => Companion is not null;

    /// <summary>Captures all display-relevant data from the live Character object.</summary>
    public static CharacterSnapshot From(Character c)
    {
        if (c is null) throw new ArgumentNullException(nameof(c));

        // Spellcasting: SpellcastingCollection is never populated in MAUI (it relies on
        // SpellContentViewModel which is WPF-only). Use SpellcastingInformation instead.
        var cm = CharacterManager.Current;
        var allSpellInfos = cm.GetSpellcastingInformations()
            .GroupBy(x => x.UniqueIdentifier)
            .Select(g => g.First())
            .ToList();
        var spellInfos = allSpellInfos
            .Where(x => !x.IsExtension)
            .ToList();
        var spellSections = CollectSpellcastingSections(cm, spellInfos, allSpellInfos);
        var primarySpellSection = spellSections.FirstOrDefault();

        return new CharacterSnapshot
        {
            Name        = c.Name       ?? "",
            PlayerName  = c.PlayerName ?? "",
            Level       = c.Level,
            Experience  = c.Experience,
            Race        = c.Race       ?? "",
            Class       = c.Class      ?? "",
            Archetype   = c.Archetype  ?? "",
            Background  = c.Background ?? "",
            Alignment   = c.Alignment  ?? "",
            Deity       = c.Deity      ?? "",
            Dragonmark  = c.Dragonmark ?? "",

            ArmorClass  = c.ArmorClass,
            MaxHp       = c.MaxHp,
            Speed       = c.Speed,
            Initiative  = c.Initiative,
            Proficiency = c.Proficiency,

            Abilities = c.Abilities.GetCollection()
                .Select(a =>
                {
                    var save = c.SavingThrows.GetCollection()
                        .FirstOrDefault(s => string.Equals(
                            s.KeyAbility?.Abbreviation, a.Abbreviation,
                            StringComparison.OrdinalIgnoreCase));
                    return new AbilityEntry(
                        a.Abbreviation ?? "",
                        a.FinalScore,
                        a.ModifierString ?? "",
                        save?.FinalBonusModifierString ?? "+0",
                        save?.IsProficient ?? false);
                })
                .ToList(),

            SavingThrows = c.SavingThrows.GetCollection()
                .Select(s => new SavingThrowEntry(
                    s.KeyAbility?.Abbreviation ?? "",
                    s.FinalBonusModifierString ?? "",
                    s.IsProficient))
                .ToList(),

            Skills = c.Skills.GetCollection()
                .Select(s => new SkillEntry(
                    s.Name ?? "",
                    s.FinalBonus,
                    s.IsProficient,
                    s.IsExpertise(c.Proficiency),
                    s.KeyAbility?.Abbreviation ?? ""))
                .ToList(),

            Backstory = c.Backstory ?? "",
            Age       = c.Age       ?? "",
            Height    = c.Height    ?? "",
            Weight    = c.Weight    ?? "",
            Eyes      = c.Eyes      ?? "",
            Hair      = c.Hair      ?? "",
            Skin      = c.Skin      ?? "",
            Gender    = c.Gender    ?? "",
            Notes1    = c.Notes1    ?? "",
            Notes2    = c.Notes2    ?? "",

            BackgroundFeatureName        = c.BackgroundFeatureName?.OriginalContent        ?? "",
            BackgroundFeatureDescription = c.BackgroundFeatureDescription?.OriginalContent ?? "",
            AdditionalFeatures           = c.AdditionalFeatures ?? "",

            Organisation = c.OrganisationName ?? "",
            Allies       = c.Allies           ?? "",
            Trinket      = c.Trinket?.Content ?? c.Trinket?.OriginalContent ?? "",

            CoinCopper              = c.Inventory.Coins.Copper,
            CoinSilver              = c.Inventory.Coins.Silver,
            CoinElectrum            = c.Inventory.Coins.Electrum,
            CoinGold                = c.Inventory.Coins.Gold,
            CoinPlatinum            = c.Inventory.Coins.Platinum,
            AttunedCount            = c.Inventory.AttunedItemCount,
            AttunedMax              = c.Inventory.MaxAttunedItemCount,
            InventoryEquipmentText  = c.Inventory.Equipment  ?? "",
            InventoryTreasureText   = c.Inventory.Treasure   ?? "",
            InventoryQuestText      = c.Inventory.QuestItems ?? "",

            InventoryItems = c.Inventory.Items
                .Where(i => i.IncludeInEquipmentPageInventory)
                .Select(i => new EquipmentItemEntry(
                    i.DisplayName ?? i.Name ?? "",
                    i.Amount,
                    i.IsStackable,
                    i.IsEquipped,
                    i.EquippedLocation ?? "",
                    i.IsAttunable,
                    i.IsAttuned,
                    i.DisplayWeight ?? "",
                    i.DisplayPrice  ?? "",
                    i.IsEquippable,
                    i.Identifier))
                .ToList(),

            Attacks = c.AttacksSection.Items
                .Where(a => a.IsDisplayed)
                .Select(a => new AttackEntry(
                    a.Name.Content ?? "",
                    a.DisplayCalculatedAttack ?? a.Attack.Content ?? "",
                    a.DisplayCalculatedDamage ?? a.Damage.Content ?? "",
                    a.Range.Content ?? ""))
                .ToList(),

            HasSpellcasting        = spellSections.Count > 0,
            IsSpellcasterPrepared  = primarySpellSection?.IsPreparedCaster ?? false,
            MaxPrepared            = primarySpellSection?.MaxPrepared ?? 0,
            SpellcastingClass      = primarySpellSection?.Name ?? "",
            SpellcastingAbility    = primarySpellSection?.AbilityName ?? "",
            SpellcastingDC         = primarySpellSection?.SpellcastingDc ?? "",
            SpellcastingAttack     = primarySpellSection?.SpellcastingAttack ?? "",
            Cantrips               = primarySpellSection?.Cantrips.Select(s => s.Name).ToList() ?? [],
            SpellLevels            = primarySpellSection?.SpellLevels ?? [],
            SpellcastingSections   = spellSections,

            Languages           = CollectLanguages(),
            ArmorProficiencies  = CollectArmorProficiencies(),
            WeaponProficiencies = CollectWeaponProficiencies(),
            ToolProficiencies   = CollectToolProficiencies(),
            RaceFeatures        = CollectRaceFeatures(),
            SpeedFly    = CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue("speed:fly"),
            SpeedClimb  = CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue("speed:climb"),
            SpeedSwim   = CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue("speed:swim"),
            SpeedBurrow = CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue("speed:burrow"),
            Companion   = cm.Status.HasCompanion ? BuildCompanionSnapshot(c) : null,
        };
    }

    private static CompanionSnapshot? BuildCompanionSnapshot(Character c)
    {
        try
        {
            var comp = c.Companion;
            var el   = comp.Element;
            if (el is null) return null;

            string typeLine = $"{el.Size} {el.CreatureType?.ToLower() ?? ""}, {el.Alignment?.ToLower() ?? ""}".Trim(' ', ',');

            var cm = CharacterManager.Current;
            var stats = comp.Statistics;

            var abilities = new[]
            {
                new CompanionAbilityEntry("STR", comp.Abilities.Strength.FinalScore, comp.Abilities.Strength.ModifierString),
                new CompanionAbilityEntry("DEX", comp.Abilities.Dexterity.FinalScore, comp.Abilities.Dexterity.ModifierString),
                new CompanionAbilityEntry("CON", comp.Abilities.Constitution.FinalScore, comp.Abilities.Constitution.ModifierString),
                new CompanionAbilityEntry("INT", comp.Abilities.Intelligence.FinalScore, comp.Abilities.Intelligence.ModifierString),
                new CompanionAbilityEntry("WIS", comp.Abilities.Wisdom.FinalScore, comp.Abilities.Wisdom.ModifierString),
                new CompanionAbilityEntry("CHA", comp.Abilities.Charisma.FinalScore, comp.Abilities.Charisma.ModifierString),
            };

            var traits    = CollectCompanionFeatures(el.Traits,    "Companion Trait");
            var actions   = CollectCompanionFeatures(el.Actions,   "Companion Action");
            var reactions = CollectCompanionFeatures(el.Reactions, "Companion Reaction");

            return new CompanionSnapshot(
                Name:        comp.CompanionName.Content ?? comp.CompanionName.OriginalContent ?? el.Name ?? "",
                TypeLine:    typeLine,
                ArmorClass:  stats.ArmorClass > 0 ? stats.ArmorClass.ToString() : comp.ArmorClass.OriginalContent ?? "",
                MaxHp:       stats.MaxHp > 0 ? stats.MaxHp.ToString() : comp.MaxHp.OriginalContent ?? "",
                Speed:       stats.Speed > 0 ? stats.Speed.ToString() : comp.Speed.OriginalContent ?? "",
                Initiative:  stats.Initiative >= 0 ? $"+{stats.Initiative}" : stats.Initiative.ToString(),
                Proficiency: stats.Proficiency,
                Abilities:   abilities,
                Traits:      traits,
                Actions:     actions,
                Reactions:   reactions);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<FeatureEntry> CollectCompanionFeatures(
        IEnumerable<string>? ids, string elementType)
    {
        if (ids is null) return [];
        try
        {
            var lookup = DataManager.Current.ElementsCollection
                .Where(x => x.Type.Equals(elementType, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(x => x.Id ?? "", x => x);

            return ids
                .Select(id => lookup.TryGetValue(id, out var e)
                    ? new FeatureEntry(e.Name ?? id, e.Description ?? "")
                    : new FeatureEntry(id, ""))
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .ToList();
        }
        catch { return []; }
    }

    private static IReadOnlyList<string> CollectLanguages()
    {
        var organizer = new ElementsOrganizer(CharacterManager.Current.GetElements());
        return organizer.GetLanguages(includeDuplicates: false)
            .Select(l => l.Name ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    private static IReadOnlyList<string> CollectArmorProficiencies()
    {
        var organizer = new ElementsOrganizer(CharacterManager.Current.GetElements());
        var profs = organizer.GetArmorProficiencies(includeDuplicates: false).ToList();

        // Check which category-level proficiencies are present.
        // These cover all individual armor pieces within that category.
        var categoryOrder = new[] { "Light Armor", "Medium Armor", "Heavy Armor", "Shields" };
        var presentCategories = new HashSet<string>();
        foreach (var cat in categoryOrder)
        {
            if (profs.Any(p => p.Name != null &&
                               p.Name.Contains($"({cat})", StringComparison.OrdinalIgnoreCase)))
                presentCategories.Add(cat);
        }

        var result = new List<string>();

        // Emit category names in canonical order
        foreach (var cat in categoryOrder)
        {
            if (presentCategories.Contains(cat))
                result.Add(cat);
        }

        // Add any individual pieces not covered by a granted category
        foreach (var prof in profs)
        {
            // Skip category elements themselves
            if (categoryOrder.Any(cat =>
                prof.Name?.Contains($"({cat})", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            bool covered =
                (presentCategories.Contains("Light Armor")  && prof.Supports?.Contains("Light Armor")  == true) ||
                (presentCategories.Contains("Medium Armor") && prof.Supports?.Contains("Medium Armor") == true) ||
                (presentCategories.Contains("Heavy Armor")  && prof.Supports?.Contains("Heavy Armor")  == true) ||
                (presentCategories.Contains("Shields")      && prof.Supports?.Contains("Shield")       == true);

            if (!covered)
            {
                var name = ExtractParenName(prof.Name);
                if (!string.IsNullOrEmpty(name) && !result.Contains(name))
                    result.Add(name);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> CollectWeaponProficiencies()
    {
        var organizer = new ElementsOrganizer(CharacterManager.Current.GetElements());
        var profs = organizer.GetWeaponProficiencies(includeDuplicates: false).ToList();

        // Ordered from broadest to narrowest; broader categories suppress narrower ones.
        var categoryOrder = new[]
        {
            "Simple Weapons", "Simple Melee Weapons", "Simple Ranged Weapons",
            "Martial Weapons", "Martial Melee Weapons", "Martial Ranged Weapons",
        };
        var presentCategories = new HashSet<string>();
        foreach (var cat in categoryOrder)
        {
            if (profs.Any(p => p.Name != null &&
                               p.Name.Contains($"({cat})", StringComparison.OrdinalIgnoreCase)))
                presentCategories.Add(cat);
        }

        var result = new List<string>();

        // Simple weapons: prefer the broad category; fall back to sub-categories
        if (presentCategories.Contains("Simple Weapons"))
        {
            result.Add("Simple Weapons");
        }
        else
        {
            if (presentCategories.Contains("Simple Melee Weapons"))   result.Add("Simple Melee Weapons");
            if (presentCategories.Contains("Simple Ranged Weapons"))  result.Add("Simple Ranged Weapons");
        }

        // Martial weapons: same logic
        if (presentCategories.Contains("Martial Weapons"))
        {
            result.Add("Martial Weapons");
        }
        else
        {
            if (presentCategories.Contains("Martial Melee Weapons"))  result.Add("Martial Melee Weapons");
            if (presentCategories.Contains("Martial Ranged Weapons")) result.Add("Martial Ranged Weapons");
        }

        // Add any individual weapons not covered by a granted category
        bool hasAllSimple   = presentCategories.Contains("Simple Weapons");
        bool hasSimpleMelee = hasAllSimple || presentCategories.Contains("Simple Melee Weapons");
        bool hasSimpleRanged= hasAllSimple || presentCategories.Contains("Simple Ranged Weapons");
        bool hasAllMartial  = presentCategories.Contains("Martial Weapons");
        bool hasMartialMelee= hasAllMartial || presentCategories.Contains("Martial Melee Weapons");
        bool hasMartialRanged=hasAllMartial || presentCategories.Contains("Martial Ranged Weapons");

        foreach (var prof in profs)
        {
            if (categoryOrder.Any(cat =>
                prof.Name?.Contains($"({cat})", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            bool covered =
                (hasSimpleMelee   && prof.Supports?.Contains("Simple")  == true && prof.Supports?.Contains("Melee")   == true) ||
                (hasSimpleRanged  && prof.Supports?.Contains("Simple")  == true && prof.Supports?.Contains("Ranged")  == true) ||
                (hasMartialMelee  && prof.Supports?.Contains("Martial") == true && prof.Supports?.Contains("Melee")   == true) ||
                (hasMartialRanged && prof.Supports?.Contains("Martial") == true && prof.Supports?.Contains("Ranged")  == true);

            if (!covered)
            {
                var name = ExtractParenName(prof.Name);
                if (!string.IsNullOrEmpty(name) && !result.Contains(name))
                    result.Add(name);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> CollectToolProficiencies()
    {
        var organizer = new ElementsOrganizer(CharacterManager.Current.GetElements());
        return organizer.GetToolProficiencies(includeDuplicates: false)
            .Select(p => ExtractParenName(p.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>Extracts the text between the first '(' and last ')' in a name.</summary>
    private static string ExtractParenName(string? name)
    {
        if (name is null) return "";
        var start = name.IndexOf('(');
        var end   = name.LastIndexOf(')');
        if (start >= 0 && end > start)
            return name[(start + 1)..end].Trim();
        return name.Trim();
    }

    private static IReadOnlyList<FeatureEntry> CollectRaceFeatures()
    {
        var organizer = new ElementsOrganizer(CharacterManager.Current.GetElements());
        return organizer.Elements
            .Where(e => e.Type == "Race" || e.Type == "Sub Race" || e.Type == "Race Variant" ||
                        e.Type == "Racial Trait" || e.Type == "Vision")
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Where(e => !e.Name.StartsWith("Ability Score Increase") &&
                        !e.Name.StartsWith("Ability Score Improvement"))
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .Select(e =>
            {
                var desc = "";
                try
                {
                    if (!string.IsNullOrWhiteSpace(e.Description))
                        desc = ElementDescriptionGenerator.GeneratePlainDescription(e.Description).Trim();
                }
                catch { }
                return new FeatureEntry(e.Name!, desc);
            })
            .ToList();
    }

    private static IReadOnlyList<SpellcastingSectionEntry> CollectSpellcastingSections(
        CharacterManager cm,
        IReadOnlyList<SpellcastingInformation> spellInfos,
        IReadOnlyList<SpellcastingInformation> allSpellInfos)
    {
        var sections = new List<SpellcastingSectionEntry>();
        bool filterKnownBySupports = spellInfos.Count > 1;

        foreach (var info in spellInfos)
        {
            string spellDC = "";
            string spellAttack = "";
            try
            {
                int dc = cm.StatisticsCalculator.StatisticValues.GetValue(info.GetSpellcasterSpellSaveStatisticName());
                int atk = cm.StatisticsCalculator.StatisticValues.GetValue(info.GetSpellcasterSpellAttackStatisticName());
                spellDC = dc.ToString();
                spellAttack = atk >= 0 ? $"+{atk}" : $"{atk}";
            }
            catch { }

            bool isPreparedCaster = info.Prepare;
            bool isSpellbookCaster = isPreparedCaster
                && cm.SelectionRules.Any(r => r.Attributes.Type == "Spell")
                && string.IsNullOrEmpty(info.InitialSupportedSpellsExpression?.Supports);
            var extensionSupports = allSpellInfos
                .Where(candidate => candidate.IsExtension
                    && string.Equals(candidate.Name, info.Name, StringComparison.OrdinalIgnoreCase))
                .SelectMany(candidate => candidate.ExtendedSupportedSpellsExpressions
                    .Where(expression => expression.Known)
                    .Select(expression => (expression.Supports, expression.IsId)))
                .Concat(info.ExtendedSupportedSpellsExpressions
                    .Where(expression => expression.Known)
                    .Select(expression => (expression.Supports, expression.IsId)))
                .ToList();

            int maxPrepared = 0;
            try
            {
                if (isPreparedCaster)
                    maxPrepared = cm.StatisticsCalculator.StatisticValues.GetValue(info.GetPrepareAmountStatisticName());
            }
            catch { }

            sections.Add(new SpellcastingSectionEntry
            {
                UniqueIdentifier = info.UniqueIdentifier ?? "",
                SourceId = info.ElementHeader?.Id ?? "",
                Name = info.Name ?? "",
                HasExtensions = extensionSupports.Count > 0,
                AbilityName = info.AbilityName ?? "",
                SpellcastingDc = spellDC,
                SpellcastingAttack = spellAttack,
                IsPreparedCaster = isPreparedCaster,
                MaxPrepared = maxPrepared,
                Cantrips = CollectCantrips(info, filterKnownBySupports, isSpellbookCaster, extensionSupports),
                SpellLevels = CollectSpellLevels(
                    isPreparedCaster,
                    info.Name ?? "",
                    info.InitialSupportedSpellsExpression?.Supports ?? "",
                    GetPreparedIds(info.Name ?? ""),
                    isSpellbookCaster,
                    filterKnownBySupports,
                    extensionSupports)
            });
        }

        return sections;
    }

    /// <summary>Gets prepared spell IDs captured by the active client during XML load.</summary>
    private static IReadOnlyCollection<string> GetPreparedIds(string spellcastingName)
        => SpellcastingSectionContext.Current?.GetPreparedIds(spellcastingName) ?? Array.Empty<string>();

    /// <summary>
    /// Collects cantrips from the character's registered elements.
    /// Cantrips are always acquired via SelectRule/GrantRule regardless of caster type.
    /// </summary>
    private static IReadOnlyList<SpellEntry> CollectCantrips(
        SpellcastingInformation info,
        bool filterBySupports,
        bool isSpellbookCaster,
        IReadOnlyList<(string Supports, bool IsId)> extensionSupports)
    {
        var cantrips = CharacterManager.Current.GetElements()
            .Where(e => e.Type == "Spell" && GetSpellLevel(e) == 0);

        string supportsExpr = info.InitialSupportedSpellsExpression?.Supports ?? "";
        if (filterBySupports)
        {
            if (!string.IsNullOrWhiteSpace(supportsExpr))
                cantrips = cantrips.Where(e => e.Supports?.Contains(supportsExpr) == true);
            else if (!string.IsNullOrWhiteSpace(info.Name))
                cantrips = cantrips.Where(e => e.Supports?.Contains(info.Name) == true);
        }

        // If support-based filtering strips everything out, fall back to the global
        // registered cantrip list so single-source and unusual grants still show up.
        var entries = cantrips
            .Select(e => new SpellEntry
            {
                Name = e.Name ?? "",
                Id = e.Id ?? "",
                IsPrepared = true,
                IsAlwaysPrepared = true,
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.Name)
            .ToList();

        foreach (var extra in ResolveExtendedSpells(extensionSupports, maxLevel: 0))
        {
            if (entries.All(entry => !entry.Id.Equals(extra.Id, StringComparison.OrdinalIgnoreCase)))
            {
                entries.Add(new SpellEntry
                {
                    Name = extra.Name,
                    Id = extra.Id,
                    IsPrepared = true,
                    IsAlwaysPrepared = true,
                });
            }
        }

        entries = entries.OrderBy(s => s.Name).ToList();

        if (entries.Count == 0 && filterBySupports && !isSpellbookCaster)
        {
            entries = CharacterManager.Current.GetElements()
                .Where(e => e.Type == "Spell" && GetSpellLevel(e) == 0)
                .Select(e => new SpellEntry
                {
                    Name = e.Name ?? "",
                    Id = e.Id ?? "",
                    IsPrepared = true,
                    IsAlwaysPrepared = true,
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(s => s.Name)
                .ToList();
        }

        return entries;
    }

    /// <summary>
    /// Collects leveled spells grouped by level.
    /// - Prepared casters (Cleric/Druid/Wizard/Paladin/Artificer): full class spell list from
    ///   DataManager filtered by the class supports expression; IsPrepared reflects the state
    ///   loaded from the character XML via the active spellcasting handler.
    /// - Known casters (Sorcerer/Bard/Ranger/Warlock): only spells the character actually has
    ///   registered; all are considered prepared (always available).
    /// </summary>
    private static IReadOnlyList<SpellLevelEntry> CollectSpellLevels(
        bool isPreparedCaster,
        string spellcastingName,
        string supportsExpr,
        IReadOnlyCollection<string> preparedIds,
        bool isSpellbookCaster = false,
        bool filterKnownBySupports = false,
        IReadOnlyList<(string Supports, bool IsId)>? extensionSupports = null)
    {
        extensionSupports ??= [];

        // Get slot counts for each level.
        int[] totalSlots = new int[10];
        try
        {
            var cm   = CharacterManager.Current;
            var info = cm.GetSpellcastingInformations().FirstOrDefault(x =>
                !x.IsExtension && string.Equals(x.Name, spellcastingName, StringComparison.OrdinalIgnoreCase));
            if (info != null)
            {
                for (int n = 1; n <= 9; n++)
                    totalSlots[n] = cm.StatisticsCalculator.StatisticValues.GetValue(info.GetSlotStatisticName(n));
            }
        }
        catch { }

        // Highest slot level this character can cast at.
        int maxSlot = 0;
        for (int n = 9; n >= 1; n--) { if (totalSlots[n] > 0) { maxSlot = n; break; } }

        // Spellbook casters (Wizard): supportsExpr is empty (no class-list filtering), spells come
        // from selection rules. Use the prepared-caster path with the spellbook flag so that only
        // registered spells are shown with individual prepare checkboxes.
        if (isPreparedCaster && isSpellbookCaster)
            return CollectPreparedCasterSpellLevels("", preparedIds, totalSlots, maxSlot, isSpellbookCaster: true, extensionSupports);

        // Full-list prepared casters (Cleric, Druid, Paladin, Artificer, etc.): filter the entire
        // class spell list by the supports expression and show all of them with prepare checkboxes.
        if (isPreparedCaster && !string.IsNullOrEmpty(supportsExpr))
            return CollectPreparedCasterSpellLevels(supportsExpr, preparedIds, totalSlots, maxSlot, isSpellbookCaster: false, extensionSupports);

        // Known caster: spells the character has selected/been granted — all always available.
        var knownSpells = CharacterManager.Current.GetElements()
            .Where(e => e.Type == "Spell");

        if (filterKnownBySupports)
        {
            if (!string.IsNullOrWhiteSpace(supportsExpr))
                knownSpells = knownSpells.Where(e => e.Supports?.Contains(supportsExpr) == true);
            else if (!string.IsNullOrWhiteSpace(spellcastingName))
                knownSpells = knownSpells.Where(e => e.Supports?.Contains(spellcastingName) == true);
        }

        var knownSpellList = knownSpells
            .Select(e => (Name: e.Name ?? "", Id: e.Id ?? "", Level: GetSpellLevel(e), Source: e.Source ?? ""))
            .Where(s => s.Level > 0 && !string.IsNullOrEmpty(s.Name))
            .ToList();

        knownSpellList.AddRange(ResolveExtendedSpells(extensionSupports, maxLevel: 9));

        var spellsByLevel = knownSpellList
            .GroupBy(s => s.Level)
            .ToDictionary(g => g.Key, g =>
                g.GroupBy(s => s.Id).Select(g2 => g2.First()).OrderBy(s => s.Name).ToList());

        var result = new List<SpellLevelEntry>();
        for (int level = 1; level <= 9; level++)
        {
            spellsByLevel.TryGetValue(level, out var spells);
            var entries = (spells ?? [])
                .Select(s => new SpellEntry { Name = s.Name, Id = s.Id, IsPrepared = true, IsAlwaysPrepared = true })
                .ToList();
            if (entries.Count > 0 || totalSlots[level] > 0)
                result.Add(new SpellLevelEntry(level, entries, totalSlots[level]));
        }
        return result;
    }

    /// <summary>
    /// Builds the full class spell list for a prepared caster, filtered from DataManager.
    /// Also includes any character-registered spells not already covered (e.g. domain spells).
    /// </summary>
    private static IReadOnlyList<SpellLevelEntry> CollectPreparedCasterSpellLevels(
        string supportsExpr,
        IReadOnlyCollection<string> preparedIds,
        int[] totalSlots,
        int maxSlot,
        bool isSpellbookCaster = false,
        IReadOnlyList<(string Supports, bool IsId)>? extensionSupports = null)
    {
        extensionSupports ??= [];
        int effectiveMax = maxSlot > 0 ? maxSlot : 9;

        // Track which registered spells are always-prepared (granted via a grant rule
        // that sets the "prepared" setter, i.e. IsAlwaysPrepared() == true).
        var alwaysPreparedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in CharacterManager.Current.GetElements().Where(e => e.Type == "Spell"))
        {
            try
            {
                dynamic d = e;
                if ((bool)d.Aquisition.WasGranted && (bool)d.Aquisition.GrantRule.IsAlwaysPrepared())
                    alwaysPreparedIds.Add((string)d.Id);
            }
            catch { }
        }

        // Build source restriction sets — mirror the WPF SpellcasterSelectionControlViewModel logic.
        // Spells whose ID is explicitly restricted, or whose Source is from a disabled pack, are
        // excluded unless they are already prepared (in which case they stay visible so the user
        // knows to swap them out, matching the WPF behaviour).
        var sm = CharacterManager.Current.SourcesManager;
        // Only honour per-element restrictions from the character file itself.
        // GetUndefinedRestrictedSourceNames() relies on ApplyRestrictions() which is a WPF-only
        // call that never runs here — without it the method marks every source as undefined,
        // filtering out the entire class spell list. Content in the DB is already enabled content.
        var restrictedIds = new HashSet<string>(sm.GetRestrictedElementIds(), StringComparer.OrdinalIgnoreCase);

        bool IsRestricted(string id, string source) => restrictedIds.Contains(id);

        List<(string Name, string Id, int Level, string Source)> allSpellList;

        if (isSpellbookCaster)
        {
            // Spellbook casters (Wizard): only spells the character has registered
            // (selected into their spellbook during level-up). The full class list from
            // DataManager is NOT available to prepare from — only the spellbook contents.
            allSpellList = CharacterManager.Current.GetElements()
                .Where(e => e.Type == "Spell")
                .Select(e => (Name: e.Name ?? "", Id: e.Id ?? "", Level: GetSpellLevel(e), Source: e.Source ?? ""))
                .Where(s => s.Level > 0 && s.Level <= effectiveMax && !string.IsNullOrEmpty(s.Name))
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .OrderBy(s => s.Name)
                .ToList();
        }
        else
        {
            // Full-list prepared casters (Cleric, Druid, Paladin, Artificer): the entire
            // class spell list from DataManager, plus any domain/subclass spells granted
            // directly onto the character.
            var spellBase = DataManager.Current.ElementsCollection
                .Where(e => e.Type == "Spell");

            IEnumerable<object> filteredSpells;
            try
            {
                dynamic interp = new Builder.Presentation.Services.ExpressionInterpreter();
                var interpreted = ((IEnumerable<object>)interp.EvaluateSupportsExpression(supportsExpr, spellBase.Cast<object>())).ToList();
                // Fall back to direct Contains check when the interpreter returns nothing —
                // it can succeed but yield an empty result for expressions it doesn't handle.
                filteredSpells = interpreted.Count > 0
                    ? interpreted
                    : spellBase.Where(e => e.Supports != null && e.Supports.Contains(supportsExpr)).Cast<object>();
            }
            catch
            {
                filteredSpells = spellBase.Where(e => e.Supports != null && e.Supports.Contains(supportsExpr)).Cast<object>();
            }

            var classSpells = filteredSpells
                .Select(e => (
                    Name:   (string?)(((dynamic)e).Name)   ?? "",
                    Id:     (string?)(((dynamic)e).Id)     ?? "",
                    Level:  GetSpellLevel(e),
                    Source: (string?)(((dynamic)e).Source) ?? ""))
                .Where(s => s.Level > 0 && s.Level <= effectiveMax && !string.IsNullOrEmpty(s.Name))
                .ToList();

            classSpells.AddRange(ResolveExtendedSpells(extensionSupports, effectiveMax));

            var classSpellIds = new HashSet<string>(classSpells.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);

            var extraSpells = CharacterManager.Current.GetElements()
                .Where(e => e.Type == "Spell")
                .Select(e => (Name: e.Name ?? "", Id: e.Id ?? "", Level: GetSpellLevel(e), Source: e.Source ?? ""))
                .Where(s => s.Level > 0 && s.Level <= effectiveMax
                            && !string.IsNullOrEmpty(s.Name)
                            && !classSpellIds.Contains(s.Id))
                .ToList();

            allSpellList = classSpells.Concat(extraSpells)
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .OrderBy(s => s.Name)
                .ToList();
        }

        // Deduplicate by Name within each level — keep one entry per name, preferring the
        // prepared one (or the first if none are prepared). This collapses PHB vs PHB 2024
        // duplicates so the user sees each spell once in the prepare list.
        var byLevel = allSpellList
            // Apply source restrictions: drop restricted spells unless already prepared.
            .Where(s => !IsRestricted(s.Id, s.Source) || preparedIds.Contains(s.Id) || alwaysPreparedIds.Contains(s.Id))
            .GroupBy(s => s.Level)
            .ToDictionary(g => g.Key, g =>
                g.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                 .Select(nameGroup =>
                 {
                     var preferred = nameGroup.FirstOrDefault(s =>
                         preparedIds.Contains(s.Id) || alwaysPreparedIds.Contains(s.Id));
                     return string.IsNullOrEmpty(preferred.Id) ? nameGroup.First() : preferred;
                 })
                 .OrderBy(s => s.Name)
                 .ToList());

        var result = new List<SpellLevelEntry>();
        for (int level = 1; level <= 9; level++)
        {
            byLevel.TryGetValue(level, out var spells);
            var entries = (spells ?? [])
                .Select(s => new SpellEntry
                {
                    Name             = s.Name,
                    Id               = s.Id,
                    IsAlwaysPrepared = alwaysPreparedIds.Contains(s.Id),
                    IsPrepared       = alwaysPreparedIds.Contains(s.Id) || preparedIds.Contains(s.Id),
                })
                .ToList();
            if (entries.Count > 0 || totalSlots[level] > 0)
                result.Add(new SpellLevelEntry(level, entries, totalSlots[level]));
        }
        return result;
    }

    private static List<(string Name, string Id, int Level, string Source)> ResolveExtendedSpells(
        IReadOnlyList<(string Supports, bool IsId)> extensionSupports,
        int maxLevel)
    {
        if (extensionSupports.Count == 0) return [];

        var spells = new List<(string Name, string Id, int Level, string Source)>();
        var spellBase = DataManager.Current.ElementsCollection.Where(e => e.Type == "Spell");
        var interpreter = new Builder.Presentation.Services.ExpressionInterpreter();

        foreach (var expression in extensionSupports)
        {
            if (string.IsNullOrWhiteSpace(expression.Supports))
                continue;

            if (expression.IsId)
            {
                var byId = DataManager.Current.ElementsCollection.GetElement(expression.Supports);
                if (byId != null)
                {
                    int level = GetSpellLevel(byId);
                    if (level <= maxLevel)
                        spells.Add((byId.Name ?? "", byId.Id ?? "", level, byId.Source ?? ""));
                }
                continue;
            }

            try
            {
                spells.AddRange(interpreter.EvaluateSupportsExpression(expression.Supports, spellBase.Cast<object>())
                    .Cast<object>()
                    .Select(e => (
                        Name: GetDynamicString(e, "Name"),
                        Id: GetDynamicString(e, "Id"),
                        Level: GetSpellLevel(e),
                        Source: GetDynamicString(e, "Source")))
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.Level <= maxLevel));
            }
            catch
            {
                spells.AddRange(spellBase
                    .Where(e => e.Supports?.Contains(expression.Supports) == true)
                    .Select(e => (e.Name ?? "", e.Id ?? "", GetSpellLevel(e), e.Source ?? ""))
                    .Where(s => !string.IsNullOrWhiteSpace(s.Item1) && s.Item3 <= maxLevel));
            }
        }

        return spells
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>Gets the spell level via dynamic dispatch (avoids Builder.Data.Elements import).</summary>
    private static int GetSpellLevel(object e)
    {
        try { return (int)((dynamic)e).Level; }
        catch { return 0; }
    }

    private static string GetDynamicString(object e, string propertyName)
    {
        try
        {
            var value = ((dynamic)e).GetType().GetProperty(propertyName)?.GetValue((dynamic)e, null);
            return value?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Refreshes all equipment- and combat-related fields from the live character after
    /// an equip or attune toggle. Text edits and spell prepared state are preserved.
    /// </summary>
    public void RefreshEquipmentFromCharacter(Character c)
    {
        c.Inventory.CalculateAttunedItemCount();

        InventoryItems = c.Inventory.Items
            .Where(i => i.IncludeInEquipmentPageInventory)
            .Select(i => new EquipmentItemEntry(
                i.DisplayName ?? i.Name ?? "",
                i.Amount,
                i.IsStackable,
                i.IsEquipped,
                i.EquippedLocation ?? "",
                i.IsAttunable,
                i.IsAttuned,
                i.DisplayWeight ?? "",
                i.DisplayPrice  ?? "",
                i.IsEquippable,
                i.Identifier))
            .ToList();

        AttunedCount = c.Inventory.AttunedItemCount;
        AttunedMax   = c.Inventory.MaxAttunedItemCount;

        Attacks = c.AttacksSection.Items
            .Where(a => a.IsDisplayed)
            .Select(a => new AttackEntry(
                a.Name.Content ?? "",
                a.DisplayCalculatedAttack ?? a.Attack.Content ?? "",
                a.DisplayCalculatedDamage ?? a.Damage.Content ?? "",
                a.Range.Content ?? ""))
            .ToList();

        ArmorClass = c.ArmorClass;
        Initiative = c.Initiative;
        Speed      = c.Speed;
        MaxHp      = c.MaxHp;
    }
}

// ── Value types ────────────────────────────────────────────────────────────────

public sealed record AbilityEntry(
    string Abbreviation,
    int    FinalScore,
    string ModifierString,
    string SaveBonusString,
    bool   SaveIsProficient);

public sealed record FeatureEntry(string Name, string Description);

public sealed record SavingThrowEntry(string AbilityAbbreviation, string BonusString, bool IsProficient);

public sealed record SkillEntry(
    string Name,
    int    FinalBonus,
    bool   IsProficient,
    bool   IsExpertise,
    string AbilityAbbreviation);

public sealed record EquipmentItemEntry(
    string Name,
    int    Amount,
    bool   IsStackable,
    bool   IsEquipped,
    string EquippedLocation,
    bool   IsAttunable,
    bool   IsAttuned,
    string DisplayWeight,
    string DisplayPrice,
    bool   IsEquippable = false,
    string Identifier   = "");

public sealed record AttackEntry(
    string Name,
    string Attack,
    string Damage,
    string Range);

/// <summary>
/// Name and Id are set once at snapshot time (init); IsPrepared is editable by the user.
/// </summary>
public sealed class SpellEntry
{
    public string Name            { get; init; } = "";
    public string Id              { get; init; } = "";
    public bool   IsPrepared      { get; set; }
    /// <summary>
    /// True when the spell cannot be unprepared: either it is always-prepared via a
    /// grant rule (domain/subclass spells for prepared casters) or it is a known spell
    /// for known-caster classes (Artificer, Ranger, etc.). The prepare checkbox is
    /// disabled for these entries.
    /// </summary>
    public bool   IsAlwaysPrepared { get; init; }
}

public sealed class SpellcastingSectionEntry
{
    public string UniqueIdentifier { get; init; } = "";
    public string SourceId { get; init; } = "";
    public string Name { get; init; } = "";
    public bool HasExtensions { get; init; }
    public string AbilityName { get; init; } = "";
    public string SpellcastingDc { get; init; } = "";
    public string SpellcastingAttack { get; init; } = "";
    public bool IsPreparedCaster { get; init; }
    public int MaxPrepared { get; init; }
    public int PreparedCount => SpellLevels.SelectMany(l => l.Spells).Count(s => s.IsPrepared && !s.IsAlwaysPrepared);
    public IReadOnlyList<SpellEntry> Cantrips { get; init; } = [];
    public IReadOnlyList<SpellLevelEntry> SpellLevels { get; init; } = [];
}

public sealed record CompanionAbilityEntry(string Abbreviation, int FinalScore, string ModifierString);

public sealed record CompanionSnapshot(
    string Name,
    string TypeLine,
    string ArmorClass,
    string MaxHp,
    string Speed,
    string Initiative,
    int    Proficiency,
    IReadOnlyList<CompanionAbilityEntry> Abilities,
    IReadOnlyList<FeatureEntry> Traits,
    IReadOnlyList<FeatureEntry> Actions,
    IReadOnlyList<FeatureEntry> Reactions);

public sealed record SpellLevelEntry(int Level, IReadOnlyList<SpellEntry> Spells, int TotalSlots = 0)
{
    /// <summary>Session-only tracking: how many slots have been expended. Not persisted.</summary>
    public int UsedSlots { get; set; }
}
