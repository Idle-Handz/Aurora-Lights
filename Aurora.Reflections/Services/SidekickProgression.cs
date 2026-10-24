using System.Globalization;

namespace Aurora.Reflections.Services;

/// <summary>
/// Published 2020 sidekick progression, Tasha's Cauldron of Everything, pp. 142–147.
/// These are independent creature profiles, never player-character class grants.
/// Feature descriptions below are concise summaries; creature statistics and choices remain editable.
/// </summary>
public static class SidekickProgression
{
    public const string Source = "Tasha's Cauldron of Everything (2020), pp. 142–147";
    public static IReadOnlyList<string> Classes { get; } = ["Expert", "Warrior", "Spellcaster", "Custom"];
    public static IReadOnlyList<string> MartialRoles { get; } = ["Attacker", "Defender"];
    public static IReadOnlyList<string> SpellcasterRoles { get; } = ["Mage", "Healer", "Prodigy"];
    public static IReadOnlyList<string> Schools { get; } =
        ["Abjuration", "Conjuration", "Divination", "Enchantment", "Evocation", "Illusion", "Necromancy", "Transmutation"];

    private static readonly int[][] SpellSlots =
    [
        [2, 0, 0, 0, 0], [2, 0, 0, 0, 0], [3, 0, 0, 0, 0], [3, 0, 0, 0, 0],
        [4, 2, 0, 0, 0], [4, 2, 0, 0, 0], [4, 3, 0, 0, 0], [4, 3, 0, 0, 0],
        [4, 3, 2, 0, 0], [4, 3, 2, 0, 0], [4, 3, 3, 0, 0], [4, 3, 3, 0, 0],
        [4, 3, 3, 1, 0], [4, 3, 3, 1, 0], [4, 3, 3, 2, 0], [4, 3, 3, 2, 0],
        [4, 3, 3, 3, 1], [4, 3, 3, 3, 1], [4, 3, 3, 3, 2], [4, 3, 3, 3, 2]
    ];
    private static readonly int[] SpellsKnown = [1, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11];
    private static readonly string[] SlotLabels = ["1st", "2nd", "3rd", "4th", "5th"];
    private static readonly string[] SpellFields =
        ["spellcasting-ability", "spell-save-dc", "spell-attack", "cantrips-known", "spells-known", "spell-slots"];

    public static string NormalizeClass(string? value) =>
        Classes.FirstOrDefault(c => c.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "Custom";

    public static bool IsAutomatic(SidekickProfile profile) => !profile.IsCompanion && profile.ProgressionClass is "Expert" or "Warrior" or "Spellcaster";

    public static bool IsCustomized(SidekickProfile profile, string key) =>
        profile.Overrides.Contains(key);

    public static bool IsVisible(SidekickProfile profile, SidekickField field) => field.Key switch
    {
        "owner-features" => profile.IsCompanion,
        "martial-role" => profile.ProgressionClass == "Warrior",
        "spellcasting-role" => profile.ProgressionClass == "Spellcaster",
        "empowered-school" => profile.ProgressionClass == "Spellcaster"
            && (int.TryParse(profile.Get("level"), out int level) && level >= 14 || profile.Get(field.Key).Length > 0),
        "class-features" => IsAutomatic(profile) || profile.Get(field.Key).Length > 0,
        "spell-slots" => true,
        _ when SpellFields.Contains(field.Key) => profile.ProgressionClass == "Spellcaster" || profile.Get(field.Key).Length > 0,
        _ => true
    };

    public static string TemplateDescription(string role)
    {
        if (role == "Custom") return "A fully editable sidekick. Enter your own creature statistics, features, actions, and spells. You can also select a published class progression in the editor.";
        var profile = new SidekickProfile { ProgressionClass = role };
        Refresh(profile);
        return $"{role} features and proficiency update automatically from level 1 through 20."
            + (role == "Spellcaster" ? " Includes Mage, Healer, and Prodigy roles, spell slots, and spells-known limits." : "")
            + " All fields can be customized. Supply the base creature's statistics and make its proficiency, ability, and spell choices.\n\n"
            + profile.Get("class-features");
    }

    public static void UseLevelDefault(SidekickProfile profile, string key)
    {
        if (!IsAutomatic(profile) || !profile.LevelDefaults.TryGetValue(key, out var value)) return;
        profile.Fields[key] = value;
        profile.Overrides.Remove(key);
        Refresh(profile);
    }

    public static void Refresh(SidekickProfile profile)
    {
        // A custom progression freezes existing values. Invalid in-progress input must not wipe them.
        if (!IsAutomatic(profile) || !int.TryParse(profile.Get("level"), out int level) || level is < 1 or > 20) return;

        void Apply(string key, string value) => ApplyLevelDefault(profile, key, value);

        Apply("proficiency", (2 + (level - 1) / 4).ToString(CultureInfo.InvariantCulture));
        SidekickChoices.ApplyAbilities(profile, Apply);
        if (profile.ProgressionClass == "Spellcaster")
        {
            string ability = profile.Get("spellcasting-role") switch
            {
                "Healer" => "Wisdom", "Prodigy" => "Charisma", _ => "Intelligence"
            };
            Apply("spellcasting-ability", ability);
            // Use effective values so customized proficiency/ability still feeds automatic spell totals.
            bool hasTotals = int.TryParse(profile.Get("proficiency"), out int proficiency)
                & int.TryParse(profile.Get(profile.Get("spellcasting-ability").ToLowerInvariant()), out int score);
            int modifier = (int)Math.Floor((score - 10) / 2d);
            Apply("spell-save-dc", hasTotals ? (8 + proficiency + modifier).ToString(CultureInfo.InvariantCulture) : "");
            Apply("spell-attack", hasTotals ? (proficiency + modifier).ToString("+0;-0;0", CultureInfo.InvariantCulture) : "");
            RefreshSpellChoiceLimits(profile);
        }
        else
        {
            foreach (string key in SpellFields) Apply(key, "");
        }
        SidekickChoices.ApplyStatistics(profile, Apply);
        Apply("class-features", Features(profile, level));
    }

    private static void ApplyLevelDefault(SidekickProfile profile, string key, string value)
    {
        string current = profile.Get(key);
        bool followsDefault = profile.LevelDefaults.TryGetValue(key, out var previous)
            ? current == previous
            : current.Length == 0 || current == SidekickService.Fields.First(f => f.Key == key).DefaultValue;
        if (!followsDefault) profile.Overrides.Add(key);
        if (!profile.Overrides.Contains(key)) profile.Fields[key] = value;
        profile.LevelDefaults[key] = value;
    }

    // Choice previews need current spell limits and eligibility without generating HP gains.
    // Share the progression defaults and override handling with the actual level update.
    internal static void RefreshSpellChoiceLimits(SidekickProfile profile)
    {
        if (!IsAutomatic(profile) || profile.ProgressionClass != "Spellcaster"
            || !int.TryParse(profile.Get("level"), out int level) || level is < 1 or > 20) return;
        ApplyLevelDefault(profile, "cantrips-known", level >= 10 ? "4" : level >= 4 ? "3" : "2");
        ApplyLevelDefault(profile, "spells-known", SpellsKnown[level - 1].ToString(CultureInfo.InvariantCulture));
        ApplyLevelDefault(profile, "spell-slots", string.Join("; ", SpellSlots[level - 1].Select((count, index) =>
            (count, index)).Where(slot => slot.count > 0).Select(slot => $"{SlotLabels[slot.index]}: {slot.count}")));
    }

    private static string Features(SidekickProfile profile, int level)
    {
        var features = new List<(int Level, string Name, string Text)>();
        void Add(int at, string name, string text)
        {
            if (level >= at) features.Add((at, name, text));
        }
        void Improvements(params int[] levels)
        {
            foreach (int at in levels)
                Add(at, "Ability Score Improvement", "Choose +2 to one ability or +1 to two abilities (maximum 20). Use the ability improvement selectors; the selected bonuses update your statistics.");
        }
        switch (profile.ProgressionClass)
        {
            case "Expert":
                Add(1, "Bonus Proficiencies", "Choose one saving throw: Dexterity, Intelligence, or Charisma; choose any five skills. Gain light armor proficiency. A humanoid, or a creature whose stat block includes a simple or martial weapon, also gains simple weapons and two tool proficiencies of your choice.");
                Add(1, "Helpful", "Help can be used as a bonus action.");
                Add(2, "Cunning Action", "On your turn, Dash, Disengage, or Hide can be used as a bonus action.");
                Add(3, "Expertise", "Choose two proficient skills; double the proficiency bonus for their ability checks.");
                Improvements(4, 8, 10, 12, 16, 19);
                Add(6, "Coordinated Strike", "When Helpful aids an attack, the enemy can be up to 30 feet away. Your next hit against that enemy before your current turn ends deals an extra 2d6 damage of the attack's type.");
                Add(7, "Evasion", "For Dexterity saves that normally halve damage, take no damage on a success and half on a failure. This does not apply while incapacitated.");
                Add(11, $"Inspiring Help ({(level >= 20 ? "2d6" : "1d6")})", "The creature you Help adds this roll to its d20 roll. When helping an attack, it may instead add the roll to damage against one target it hits.");
                Add(14, "Reliable Talent", "For ability checks that include your full proficiency bonus, treat a d20 result below 10 as 10.");
                Add(15, "Expertise (two more skills)", "Choose two additional proficient skills to receive double proficiency on ability checks.");
                Add(18, "Sharp Mind", "Choose an additional saving throw proficiency: Intelligence, Wisdom, or Charisma.");
                break;
            case "Warrior":
                Add(1, "Bonus Proficiencies", "Choose one saving throw: Strength, Dexterity, or Constitution; choose two skills from Acrobatics, Animal Handling, Athletics, Intimidation, Nature, Perception, and Survival. Gain all armor proficiencies. A humanoid, or a creature whose stat block includes a simple or martial weapon, also gains shields and simple and martial weapons.");
                Add(1, $"Martial Role — {profile.Get("martial-role")}", profile.Get("martial-role") == "Defender"
                    ? "As a reaction, impose disadvantage on an attack by a creature you can see within 5 feet of you, when its target is someone else."
                    : "Add +2 to all attack rolls. The automatic weapon attack totals include this bonus.");
                Add(2, $"Second Wind ({(level >= 20 ? "2 uses" : "1 use")})", $"As a bonus action, regain 1d10 + {level} HP. Recover uses after a short or long rest.");
                Add(3, "Improved Critical", "Attack rolls score a critical hit on a natural 19 or 20.");
                Improvements(4, 8, 12, 14, 16, 19);
                Add(6, $"Extra Attack ({(level >= 15 ? "3 attacks" : "2 attacks")})", "Make this many attacks when taking the Attack action on your turn. Use this or the creature's Multiattack, never both on the same turn.");
                Add(7, "Battle Readiness", "Roll initiative with advantage.");
                Add(10, "Improved Defense", "Increase Armor Class by 1. The automatic Armor Class total includes this bonus.");
                Add(11, $"Indomitable ({(level >= 18 ? "2 uses" : "1 use")})", "Reroll a failed saving throw and use the new result. Recover uses after a long rest.");
                break;
            case "Spellcaster":
                Add(1, "Bonus Proficiencies", "Choose one saving throw: Wisdom, Intelligence, or Charisma; choose two skills from Arcana, History, Insight, Investigation, Medicine, Performance, Persuasion, and Religion. Gain light armor proficiency. A humanoid, or a creature whose stat block includes a simple or martial weapon, also gains simple weapons.");
                string listAndFocus = profile.Get("spellcasting-role") switch
                {
                    "Healer" => "Choose spells from the cleric and druid lists. You may use a holy symbol as a focus.",
                    "Prodigy" => "Choose spells from the bard and warlock lists. You may use an arcane focus or a musical instrument as a focus.",
                    _ => "Choose spells from the wizard list. You may use an arcane focus."
                };
                Add(1, $"Spellcasting — {profile.Get("spellcasting-role")}", listAndFocus
                    + " This replaces the creature's Spellcasting trait. Choose known spells of levels you have slots for, plus the listed number of cantrips. On gaining a level, you may replace one known spell or cantrip with an eligible choice. Spells are known, not prepared. All spell slots recover after a long rest.");
                Improvements(4, 8, 12, 16, 18);
                Add(6, "Potent Cantrips", "Add your spellcasting ability modifier to cantrip damage.");
                string school = string.IsNullOrWhiteSpace(profile.Get("empowered-school"))
                    ? "Choose one school of magic." : $"Chosen school: {profile.Get("empowered-school")}.";
                Add(14, "Empowered Spells", school + " When casting a spell from that school using a spell slot, add your spellcasting ability modifier to its damage or healing.");
                Add(20, "Focused Casting", "Taking damage cannot break your concentration on a spell.");
                break;
        }
        return string.Join("\n\n", features.OrderBy(f => f.Level).Select(f => $"Level {f.Level} — {f.Name}\n{f.Text}"));
    }
}
