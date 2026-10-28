using System.Globalization;
using System.Text.RegularExpressions;
using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;

namespace Aurora.Reflections.Services;

public sealed class SidekickChoiceState
{
    public Dictionary<string, string> Base { get; set; } = new();
    public Dictionary<string, SidekickClassChoices> Classes { get; set; } = new();
    public SidekickHitPointState? HitPoints { get; set; }
    public SidekickChoiceState Copy() => new() { Base = new(Base), Classes = Classes.ToDictionary(p => p.Key, p => p.Value.Copy()), HitPoints = HitPoints?.Copy() };
}
public sealed class SidekickClassChoices
{
    public Dictionary<int, string[]> Improvements { get; set; } = new();
    public string SavingThrow { get; set; } = "";
    public string AdditionalSave { get; set; } = "";
    public List<string> Skills { get; set; } = [];
    public Dictionary<int, List<string>> Expertise { get; set; } = new();
    public List<string> Tools { get; set; } = [];
    public Dictionary<string, List<string>> Spells { get; set; } = new();
    public SidekickClassChoices Copy() => new()
    {
        Improvements = Improvements.ToDictionary(p => p.Key, p => p.Value.ToArray()), SavingThrow = SavingThrow,
        AdditionalSave = AdditionalSave, Skills = [.. Skills], Expertise = Expertise.ToDictionary(p => p.Key, p => p.Value.ToList()),
        Tools = [.. Tools], Spells = Spells.ToDictionary(p => p.Key, p => p.Value.ToList())
    };
}

public static class SidekickChoices
{
    public static readonly string[] Abilities = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];
    public static readonly (string Name, string Ability)[] Skills =
    [
        ("Acrobatics", "Dexterity"), ("Animal Handling", "Wisdom"), ("Arcana", "Intelligence"), ("Athletics", "Strength"),
        ("Deception", "Charisma"), ("History", "Intelligence"), ("Insight", "Wisdom"), ("Intimidation", "Charisma"),
        ("Investigation", "Intelligence"), ("Medicine", "Wisdom"), ("Nature", "Intelligence"), ("Perception", "Wisdom"),
        ("Performance", "Charisma"), ("Persuasion", "Charisma"), ("Religion", "Intelligence"), ("Sleight of Hand", "Dexterity"),
        ("Stealth", "Dexterity"), ("Survival", "Wisdom")
    ];
    private static readonly string[] Tools = ["Alchemist's supplies", "Brewer's supplies", "Calligrapher's supplies", "Carpenter's tools", "Cartographer's tools", "Cobbler's tools", "Cook's utensils", "Glassblower's tools", "Jeweler's tools", "Leatherworker's tools", "Mason's tools", "Painter's supplies", "Potter's tools", "Smith's tools", "Tinker's tools", "Weaver's tools", "Woodcarver's tools", "Disguise kit", "Forgery kit", "Herbalism kit", "Navigator's tools", "Poisoner's kit", "Thieves' tools", "Dice set", "Dragonchess set", "Playing card set", "Three-Dragon Ante set", "Bagpipes", "Drum", "Dulcimer", "Flute", "Lute", "Lyre", "Horn", "Pan flute", "Shawm", "Viol", "Vehicles (land)", "Vehicles (water)"];
    private static readonly string[] BaseKeys = ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma", "ac", "hp", "hit-dice", "initiative", "skills", "saves", "actions", "senses"];
    public static int Level(SidekickProfile p) => int.TryParse(p.Get("level"), out int n) ? Math.Clamp(n, 1, 20) : 1;
    public static IReadOnlyList<int> ImprovementLevels(string role) => role switch
    {
        "Expert" => [4, 8, 10, 12, 16, 19], "Warrior" => [4, 8, 12, 14, 16, 19], "Spellcaster" => [4, 8, 12, 16, 18], _ => []
    };
    private static int Number(string? s, int fallback = 0) => int.TryParse(s, out int n) ? n : fallback;
    private static int Modifier(int score) => (int)Math.Floor((score - 10) / 2d);
    private static string Signed(int n) => n.ToString("+0;-0;+0", CultureInfo.InvariantCulture);

    public static void Initialize(SidekickProfile profile, bool preserveExistingTotals = true)
    {
        if (!SidekickProgression.IsAutomatic(profile)) return;
        if (profile.Choices != null) { SidekickHitPoints.Initialize(profile); return; }
        profile.Choices = new();
        SidekickHitPoints.Initialize(profile);
        if (string.IsNullOrWhiteSpace(profile.Get("hit-dice")))
            profile.Fields["hit-dice"] = Regex.Match(profile.Get("hp"), @"\b\d+d(?:4|6|8|10|12|20)\b", RegexOptions.IgnoreCase).Value;
        foreach (string key in BaseKeys)
        {
            profile.Choices.Base[key] = profile.Get(key);
            if (preserveExistingTotals && key is ("ac" or "hp" or "actions" or "saves" or "skills" or "senses")
                && !string.IsNullOrWhiteSpace(profile.Get(key))) profile.Overrides.Add(key);
            profile.LevelDefaults.TryAdd(key, profile.Get(key));
        }
        profile.Choices.Base["initiative"] = (Number(profile.Get("initiative")) - Modifier(Number(profile.Get("dexterity"), 10))).ToString();
        profile.Choices.Base["proficiency"] = profile.Get("proficiency");
        profile.Choices.Base["armor-dexterity"] = "None";
        profile.Choices.Base["weapon-training"] = profile.Get("type").Equals("Humanoid", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No";
        profile.Choices.Base["attack-ability"] = "Strength";
        // Use the first weapon attack to suggest its ability; the user can change the explicit basis.
        var hit = Regex.Match(profile.Get("actions"), @"Weapon Attack:\s*([+-]\d+) to hit", RegexOptions.IgnoreCase);
        if (hit.Success && int.Parse(hit.Groups[1].Value) - Number(profile.Get("proficiency"), 2) == Modifier(Number(profile.Get("dexterity"), 10))
            && profile.Get("strength") != profile.Get("dexterity")) profile.Choices.Base["attack-ability"] = "Dexterity";
    }
    private static SidekickClassChoices Current(SidekickProfile profile)
    {
        Initialize(profile);
        if (!profile.Choices!.Classes.TryGetValue(profile.ProgressionClass, out var choice))
            profile.Choices.Classes[profile.ProgressionClass] = choice = new();
        return choice;
    }
    private static string Base(SidekickProfile p, string key) => p.Choices!.Base.GetValueOrDefault(key, "");
    private static string At(IReadOnlyList<string> values, int index) => index < values.Count ? values[index] : "";
    private static void Put(List<string> list, int index, string value)
    {
        while (list.Count <= index) list.Add("");
        list[index] = value;
    }
    public static IReadOnlyList<string> SkillOptions(string role) => role switch
    {
        "Warrior" => ["Acrobatics", "Animal Handling", "Athletics", "Intimidation", "Nature", "Perception", "Survival"],
        "Spellcaster" => ["Arcana", "History", "Insight", "Investigation", "Medicine", "Performance", "Persuasion", "Religion"],
        _ => Skills.Select(s => s.Name).ToArray()
    };
    private static IReadOnlyList<string> SaveOptions(string role) => role switch
    {
        "Expert" => ["Dexterity", "Intelligence", "Charisma"], "Warrior" => ["Strength", "Dexterity", "Constitution"],
        "Spellcaster" => ["Intelligence", "Wisdom", "Charisma"], _ => []
    };
    private static Dictionary<string, int> ParseBonuses(string text, IEnumerable<string> names) => names
        .Select(name => (name, match: Regex.Match(text, @"\b" + Regex.Escape(name) + @"\s+([+-]?\d+)\b", RegexOptions.IgnoreCase)))
        .Where(p => p.match.Success).ToDictionary(p => p.name, p => int.Parse(p.match.Groups[1].Value));
    private static HashSet<string> BaseSkills(SidekickProfile p) => ParseBonuses(Base(p, "skills"), Skills.Select(s => s.Name)).Keys.ToHashSet();
    private static HashSet<string> BaseSaves(SidekickProfile p) => Abilities.Where(a => Regex.IsMatch(Base(p, "saves"), @"\b(?:" + a + "|" + a[..3] + @")\s+[+-]?\d+", RegexOptions.IgnoreCase)).ToHashSet();

    public static void Change(SidekickProfile profile, SidekickChoiceChange change)
    {
        var choices = Current(profile);
        var key = change.Key.Split(':');
        switch (key[0])
        {
            case "base": profile.Choices!.Base[key[1]] = change.Value; break;
            case "hp": SidekickHitPoints.Change(profile, key, change.Value); break;
            case "asi":
                int at = int.Parse(key[1]);
                if (!choices.Improvements.TryGetValue(at, out var pair)) choices.Improvements[at] = pair = ["", ""];
                pair[int.Parse(key[2])] = change.Value; break;
            case "skill": Put(choices.Skills, int.Parse(key[1]), change.Value); break;
            case "save": choices.SavingThrow = change.Value; break;
            case "sharp-mind": choices.AdditionalSave = change.Value; break;
            case "expertise":
                int tier = int.Parse(key[1]);
                if (!choices.Expertise.TryGetValue(tier, out var skills)) choices.Expertise[tier] = skills = [];
                Put(skills, int.Parse(key[2]), change.Value); break;
            case "tool": Put(choices.Tools, int.Parse(key[1]), change.Value); break;
            case "cantrip": case "spell": Put(SpellSelections(profile, key[0] == "cantrip"), int.Parse(key[1]), change.Value); break;
            default: throw new InvalidOperationException("That sidekick choice is not supported.");
        }
        SidekickProgression.Refresh(profile);
    }

    // Called before spell totals so effective, possibly overridden ability scores feed spellcasting.
    internal static void ApplyAbilities(SidekickProfile p, Action<string, string> apply)
    {
        if (p.Choices == null) return;
        var choices = Current(p);
        var scores = Abilities.ToDictionary(a => a, a => Number(Base(p, a.ToLowerInvariant()), 10));
        foreach (int level in ImprovementLevels(p.ProgressionClass).Where(l => l <= Level(p)))
            foreach (string ability in choices.Improvements.GetValueOrDefault(level, []))
                if (scores.ContainsKey(ability)) scores[ability] = Math.Max(scores[ability], Math.Min(20, scores[ability] + 1));
        foreach (var (ability, score) in scores) apply(ability.ToLowerInvariant(), score.ToString());
    }

    internal static void ApplyStatistics(SidekickProfile p, Action<string, string> apply)
    {
        if (p.Choices == null) return;
        var choices = Current(p);
        int pb = Number(p.Get("proficiency"), 2), oldPb = Math.Max(1, Number(Base(p, "proficiency"), 2));
        int Mod(string a) => Modifier(Number(p.Get(a.ToLowerInvariant()), 10));
        int Old(string a) => Modifier(Number(Base(p, a.ToLowerInvariant()), 10));
        int Delta(string a) => Mod(a) - Old(a);
        apply("initiative", Signed(Number(Base(p, "initiative")) + Mod("Dexterity")));
        SidekickHitPoints.Apply(p, apply);
        var originalSkills = ParseBonuses(Base(p, "skills"), Skills.Select(s => s.Name));
        var chosenSkills = choices.Skills.Take(p.ProgressionClass == "Expert" ? 5 : 2).Where(SkillOptions(p.ProgressionClass).Contains).ToHashSet();
        var expertise = p.ProgressionClass == "Expert" ? choices.Expertise.Where(kv => kv.Key is 3 or 15 && kv.Key <= Level(p)).SelectMany(kv => kv.Value).ToHashSet() : [];
        var skillText = new List<string>();
        int perception = Mod("Wisdom");
        foreach (var (name, ability) in Skills)
        {
            bool original = originalSkills.TryGetValue(name, out int bonus);
            int multiplier = original && bonus - Old(ability) >= oldPb * 2 ? 2 : original || chosenSkills.Contains(name) ? 1 : 0;
            if (multiplier > 0 && expertise.Contains(name)) multiplier = 2;
            int residual = original ? bonus - Old(ability) - (bonus - Old(ability) >= oldPb * 2 ? 2 : 1) * oldPb : 0;
            int total = Mod(ability) + multiplier * pb + residual;
            skillText.Add(name + " " + Signed(total));
            if (name == "Perception") perception = total;
        }
        apply("skills", WithUnparsedNotes(string.Join(", ", skillText), Base(p, "skills"), Skills.Select(s => s.Name)));
        var baseSaves = BaseSaves(p);
        var saves = new List<string>();
        foreach (string ability in Abilities)
        {
            var match = Regex.Match(Base(p, "saves"), @"\b(?:" + ability + "|" + ability[..3] + @")\s+([+-]?\d+)", RegexOptions.IgnoreCase);
            int residual = match.Success ? int.Parse(match.Groups[1].Value) - Old(ability) - oldPb : 0;
            bool proficient = baseSaves.Contains(ability) || choices.SavingThrow == ability && SaveOptions(p.ProgressionClass).Contains(ability)
                || p.ProgressionClass == "Expert" && Level(p) >= 18 && choices.AdditionalSave == ability;
            saves.Add(ability + " " + Signed(Mod(ability) + residual + (proficient ? pb : 0)));
        }
        apply("saves", WithUnparsedNotes(string.Join(", ", saves), Base(p, "saves"), Abilities.SelectMany(a => new[] { a, a[..3] })));
        string senses = Regex.Replace(Base(p, "senses"), @"(?:,\s*)?passive Perception\s+\d+", "", RegexOptions.IgnoreCase).Trim(' ', ',');
        apply("senses", (senses.Length > 0 ? senses + ", " : "") + "passive Perception " + (10 + perception));
        if (int.TryParse(Regex.Match(Base(p, "ac"), @"^\d+").Value, out int ac))
        {
            int adjustment = Base(p, "armor-dexterity") switch
            { "Full Dexterity" => Delta("Dexterity"), "Dexterity (max 2)" => Math.Min(2, Mod("Dexterity")) - Math.Min(2, Old("Dexterity")), _ => 0 };
            apply("ac", (ac + adjustment + (p.ProgressionClass == "Warrior" && Level(p) >= 10 ? 1 : 0)).ToString());
        }
        string attackAbility = Base(p, "attack-ability");
        int attacker = p.ProgressionClass == "Warrior" && p.Get("martial-role") == "Attacker" ? 2 : 0;
        string attacks = Regex.Replace(Base(p, "actions"), @"(?:Melee|Ranged|Melee or Ranged) Weapon Attack:[^\r\n]*", m =>
        {
            var hit = Regex.Match(m.Value, @"Attack:\s*([+-]\d+) to hit", RegexOptions.IgnoreCase);
            string ability = attackAbility;
            if (hit.Success && attackAbility is "Strength" or "Dexterity")
            {
                var candidates = new[] { "Strength", "Dexterity" }.Where(a => Old(a) + oldPb == int.Parse(hit.Groups[1].Value)).ToArray();
                if (candidates.Length == 1) ability = candidates[0];
            }
            int delta = Abilities.Contains(ability) ? Delta(ability) : 0;
            return CompanionProgression.ApplyAttackBonuses(m.Value, pb - oldPb + delta + attacker, delta);
        }, RegexOptions.IgnoreCase);
        apply("actions", attacks);
        var proficiencies = new List<string> { p.ProgressionClass == "Warrior" ? "Light, medium, and heavy armor" : "Light armor" };
        if (Base(p, "weapon-training") == "Yes")
        {
            proficiencies.Add(p.ProgressionClass == "Warrior" ? "Shields; simple and martial weapons" : "Simple weapons");
            if (p.ProgressionClass == "Expert") proficiencies.AddRange(choices.Tools.Take(2).Where(t => t.Length > 0));
        }
        var trainedSkills = originalSkills.Keys.Concat(chosenSkills).Distinct().OrderBy(s => s).ToArray();
        if (trainedSkills.Length > 0) proficiencies.Add("Skills: " + string.Join(", ", trainedSkills));
        if (expertise.Count > 0) proficiencies.Add("Expertise: " + string.Join(", ", expertise.Intersect(trainedSkills).OrderBy(s => s)));
        var trainedSaves = baseSaves.Concat(new[] { choices.SavingThrow, p.ProgressionClass == "Expert" && Level(p) >= 18 ? choices.AdditionalSave : "" }).Where(s => s.Length > 0).Distinct().ToArray();
        if (trainedSaves.Length > 0) proficiencies.Add("Saving throws: " + string.Join(", ", trainedSaves));
        apply("proficiencies", string.Join("; ", proficiencies));
        apply("selected-spells", p.ProgressionClass == "Spellcaster" ? SpellSummary(p) : "");
    }

    private static string WithUnparsedNotes(string calculated, string original, IEnumerable<string> names)
    {
        foreach (string name in names.OrderByDescending(n => n.Length))
            original = Regex.Replace(original, @"\b" + Regex.Escape(name) + @"\s+[+-]?\d+\b", "", RegexOptions.IgnoreCase);
        original = Regex.Replace(original, @"[,;\s]+", " ").Trim();
        return original.Length == 0 ? calculated : calculated + "\n" + original;
    }

    public static List<string> SpellSelections(SidekickProfile p, bool cantrip)
    {
        var choices = Current(p);
        string key = p.Get("spellcasting-role") + (cantrip ? ":cantrips" : ":spells");
        if (!choices.Spells.TryGetValue(key, out var list)) choices.Spells[key] = list = [];
        return list;
    }
    public static int SpellCount(SidekickProfile p, bool cantrip) => Math.Clamp(Number(p.Get(cantrip ? "cantrips-known" : "spells-known")), 0, 99);
    public static IReadOnlyList<Spell> EligibleSpells(SidekickProfile p, bool cantrip, IEnumerable<Spell>? catalog = null, BuildSourceRestrictionSnapshot? sources = null)
    {
        if (p.ProgressionClass != "Spellcaster") return [];
        string[] lists = p.Get("spellcasting-role") switch { "Healer" => ["Cleric", "Druid"], "Prodigy" => ["Bard", "Warlock"], _ => ["Wizard"] };
        int max;
        try { max = CreatureCombat.Slots(p).Where(s => s.Value > 0).Select(s => s.Key).DefaultIfEmpty(0).Max(); }
        catch (InvalidOperationException) { max = 0; }
        sources ??= BuildSourceRestrictionSnapshot.Empty;
        return (catalog ?? DataManager.Current.ElementsCollection.OfType<Spell>()).Where(s => sources.Allows(s)
            && (cantrip ? s.Level == 0 : s.Level > 0 && s.Level <= max)
            && lists.Any(list => s.Supports.Contains(list, StringComparer.OrdinalIgnoreCase)
                || DbElementLoader.SpellAccessMap.TryGetValue(list, out var ids) && ids.Contains(s.Id)))
            .OrderBy(s => s.Level).ThenBy(s => s.Name).ThenBy(s => s.Source).ToList();
    }
    public static IReadOnlyList<ElementOption> SpellOptions(SidekickProfile p, bool cantrip, int slot)
    {
        var selections = SpellSelections(p, cantrip);
        string current = At(selections, slot);
        var selectedNames = selections.Take(SpellCount(p, cantrip)).Where((_, i) => i != slot)
            .Select(id => DataManager.Current.ElementsCollection.GetElement(id)?.Name).Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return EligibleSpells(p, cantrip, sources: BuildSourceRestrictionSnapshot.CaptureCurrent()).Select(s => new ElementOption(s.Id, s.Name, Describe(s), s.Source,
            SpellLevel: s.Level, School: s.MagicSchool, IsRitual: s.IsRitual, IsConcentration: s.IsConcentration,
            IsDisabled: selectedNames.Contains(s.Name), IsCurrentSelection: s.Id == current)).ToList();
    }
    private static string Describe(Spell s) => s.GetShortDescription() + "\n" + s.CastingTime + " · " + s.Range + " · " + s.Duration + "\n" + Plain(s.Description);
    private static string Plain(string html)
    {
        try { return ElementDescriptionGenerator.GeneratePlainDescription(html ?? "").Trim(); }
        catch { return html ?? ""; }
    }
    private static string SpellSummary(SidekickProfile p)
    {
        var spells = new List<string>();
        foreach (bool cantrip in new[] { true, false })
        {
            var selected = SpellSelections(p, cantrip).Take(SpellCount(p, cantrip)).Where(id => id.Length > 0).Distinct().ToList();
            if (selected.Count == 0) continue;
            var allowed = EligibleSpells(p, cantrip).ToDictionary(s => s.Id);
            foreach (string id in selected)
                if (allowed.TryGetValue(id, out var spell)) spells.Add(spell.Name + "\n" + Describe(spell));
        }
        return string.Join("\n\n", spells);
    }

    public static IReadOnlyList<string> Errors(SidekickProfile p)
    {
        if (!SidekickProgression.IsAutomatic(p) || p.Choices == null) return [];
        var errors = new List<string>();
        var choices = Current(p);
        var scores = Abilities.ToDictionary(a => a, a => Number(Base(p, a.ToLowerInvariant()), 10));
        if (Abilities.Any(a => !int.TryParse(Base(p, a.ToLowerInvariant()), out int n) || n is < 1 or > 30)) errors.Add("Base ability scores must be between 1 and 30.");
        foreach (int level in ImprovementLevels(p.ProgressionClass).Where(l => l <= Level(p)))
            foreach (string ability in choices.Improvements.GetValueOrDefault(level, []))
                if (ability.Length > 0 && (!scores.ContainsKey(ability) || ++scores[ability] > 20)) errors.Add($"The level {level} ability improvement cannot raise {ability} above 20.");
        var skills = choices.Skills.Take(p.ProgressionClass == "Expert" ? 5 : 2).Where(s => s.Length > 0).ToList();
        if (skills.Distinct().Count() != skills.Count || skills.Any(s => !SkillOptions(p.ProgressionClass).Contains(s) || BaseSkills(p).Contains(s)))
            errors.Add("Choose different new skill proficiencies from the class list.");
        if (choices.SavingThrow.Length > 0 && (!SaveOptions(p.ProgressionClass).Contains(choices.SavingThrow) || BaseSaves(p).Contains(choices.SavingThrow)))
            errors.Add("Choose a new saving-throw proficiency from the class list.");
        if (p.ProgressionClass == "Expert")
        {
            var expertise = choices.Expertise.Where(kv => kv.Key is 3 or 15 && kv.Key <= Level(p)).SelectMany(kv => kv.Value.Take(2)).Where(s => s.Length > 0).ToList();
            if (expertise.Distinct().Count() != expertise.Count || expertise.Any(s => !BaseSkills(p).Contains(s) && !skills.Contains(s))) errors.Add("Expertise must use different proficient skills.");
            if (Level(p) >= 18 && choices.AdditionalSave.Length > 0 && (!new[] { "Intelligence", "Wisdom", "Charisma" }.Contains(choices.AdditionalSave)
                || BaseSaves(p).Contains(choices.AdditionalSave) || choices.AdditionalSave == choices.SavingThrow)) errors.Add("Sharp Mind must grant a new Intelligence, Wisdom, or Charisma saving-throw proficiency.");
            var tools = choices.Tools.Take(2).Where(s => s.Length > 0).ToList();
            if (tools.Distinct().Count() != tools.Count) errors.Add("Choose two different tools.");
        }
        if (p.ProgressionClass == "Spellcaster") foreach (bool cantrip in new[] { true, false })
        {
            var selected = SpellSelections(p, cantrip).Take(SpellCount(p, cantrip)).Where(s => s.Length > 0).ToList();
            if (selected.Count == 0) continue;
            var allowed = EligibleSpells(p, cantrip).ToDictionary(s => s.Id);
            if (selected.Any(id => !allowed.ContainsKey(id))) errors.Add("Replace or clear spell choices that are unavailable for this role, level, or source selection.");
            if (selected.Where(allowed.ContainsKey).Select(id => allowed[id].Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Count(allowed.ContainsKey)) errors.Add("Choose each spell only once.");
        }
        errors.AddRange(SidekickHitPoints.Errors(p));
        return errors.Distinct().ToList();
    }

    public static IReadOnlyList<SidekickPendingChoice> Pending(SidekickProfile profile, int? ownerLevel = null)
    {
        if (!SidekickProgression.IsAutomatic(profile)) return [];
        // Preview the current owner level without changing the save or generating HP rolls.
        var preview = profile.Copy();
        if (preview.FollowOwnerLevel && ownerLevel is >= 1 and <= 20)
        {
            preview.Fields["level"] = ownerLevel.Value.ToString(CultureInfo.InvariantCulture);
            SidekickProgression.RefreshSpellChoiceLimits(preview);
        }
        return Model(preview).PendingChoices;
    }

    public static SidekickChoicesModel Model(SidekickProfile p)
    {
        var choice = Current(p);
        var rows = new List<SidekickChoiceRow>();
        void Row(string key, string label, string value, IEnumerable<string> options, string group) => rows.Add(new(key, label, value,
            options.Prepend("").Distinct().Select(v => new SidekickChoiceOption(v, v.Length == 0 ? "Choose…" : v)).ToList(), group));
        Row("base:armor-dexterity", "How Dexterity affects Armor Class", Base(p, "armor-dexterity"), ["None", "Full Dexterity", "Dexterity (max 2)"], "Base creature");
        Row("base:attack-ability", "Default ability for weapon attacks", Base(p, "attack-ability"), ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"], "Base creature");
        Row("base:weapon-training", "Humanoid or uses a simple/martial weapon", Base(p, "weapon-training"), ["Yes", "No"], "Base creature");
        foreach (int level in ImprovementLevels(p.ProgressionClass).Where(l => l <= Level(p)))
        {
            var pair = choice.Improvements.GetValueOrDefault(level, ["", ""]);
            for (int i = 0; i < 2; i++) Row($"asi:{level}:{i}", $"Level {level}: +1 ability ({i + 1}/2)", At(pair, i), Abilities, "Ability improvements");
        }
        var baseSkills = BaseSkills(p);
        Row("save", "Saving-throw proficiency", choice.SavingThrow, SaveOptions(p.ProgressionClass).Except(BaseSaves(p)), "Proficiencies");
        for (int i = 0; i < (p.ProgressionClass == "Expert" ? 5 : 2); i++)
            Row("skill:" + i, $"Skill proficiency {i + 1}", At(choice.Skills, i), SkillOptions(p.ProgressionClass).Except(baseSkills)
                .Where(s => s == At(choice.Skills, i) || !choice.Skills.Contains(s)), "Proficiencies");
        if (p.ProgressionClass == "Expert")
        {
            var proficient = baseSkills.Concat(choice.Skills.Where(s => s.Length > 0)).Distinct().OrderBy(s => s).ToArray();
            foreach (int level in new[] { 3, 15 }.Where(l => l <= Level(p)))
            {
                var selected = choice.Expertise.GetValueOrDefault(level, []);
                for (int i = 0; i < 2; i++) Row($"expertise:{level}:{i}", $"Level {level}: expertise {i + 1}", At(selected, i), proficient.Where(s => s == At(selected, i)
                    || !choice.Expertise.Where(kv => kv.Key <= Level(p)).SelectMany(kv => kv.Value).Contains(s)), "Expertise");
            }
            if (Level(p) >= 18) Row("sharp-mind", "Sharp Mind saving throw", choice.AdditionalSave, new[] { "Intelligence", "Wisdom", "Charisma" }.Except(BaseSaves(p)).Where(s => s != choice.SavingThrow), "Proficiencies");
            if (Base(p, "weapon-training") == "Yes") for (int i = 0; i < 2; i++) Row("tool:" + i, $"Tool proficiency {i + 1}", At(choice.Tools, i), Tools.Where(t => t == At(choice.Tools, i) || !choice.Tools.Contains(t)), "Proficiencies");
        }
        var pending = rows.Where(r => r.Group != "Base creature"
            && (string.IsNullOrWhiteSpace(r.Value) || !r.Options.Any(o => o.Value == r.Value)))
            .Select(r => new SidekickPendingChoice(r.Key, r.Label, r.Group)).ToList();
        void ClassChoice(string key, string label, IReadOnlyList<string> options)
        {
            if (!options.Contains(p.Get(key))) pending.Add(new("field:" + key, label, "Class choices"));
        }
        if (p.ProgressionClass == "Warrior") ClassChoice("martial-role", "Martial role", SidekickProgression.MartialRoles);
        if (p.ProgressionClass == "Spellcaster")
        {
            ClassChoice("spellcasting-role", "Spellcaster role", SidekickProgression.SpellcasterRoles);
            if (Level(p) >= 14) ClassChoice("empowered-school", "Empowered Spells school", SidekickProgression.Schools);
        }
        var spellRows = new List<SidekickSpellChoiceRow>();
        if (p.ProgressionClass == "Spellcaster") foreach (bool cantrip in new[] { true, false })
        {
            var selections = SpellSelections(p, cantrip);
            var allowed = selections.Take(SpellCount(p, cantrip)).Any(id => id.Length > 0)
                ? EligibleSpells(p, cantrip).Select(s => s.Id).ToHashSet() : [];
            for (int i = 0; i < SpellCount(p, cantrip); i++)
            {
                string id = At(selections, i);
                var spell = id.Length == 0 ? null : DataManager.Current.ElementsCollection.GetElement(id) as Spell;
                spellRows.Add(new((cantrip ? "cantrip:" : "spell:") + i, (cantrip ? "Cantrip " : "Known spell ") + (i + 1),
                    spell?.Name ?? (id.Length > 0 ? "Unavailable spell" : "Not chosen"), spell == null ? "" : spell.GetShortDescription() + " · " + spell.Source, id.Length > 0));
                if (!allowed.Contains(id)) pending.Add(new(spellRows[^1].Key, spellRows[^1].Label, cantrip ? "Cantrips" : "Known spells"));
            }
        }
        return new()
        {
            BaseFields = BaseKeys.Append("proficiency").Select(k => new SidekickBaseField("base:" + k, k == "initiative" ? "Initiative bonus (besides Dexterity)" : "Base " + (SidekickService.Fields.FirstOrDefault(f => f.Key == k)?.Label ?? k), Base(p, k))).ToList(),
            Rows = rows, Spells = spellRows, Notices = Errors(p), HitPoints = SidekickHitPoints.Model(p), PendingChoices = pending,
            Summary = $"AC {p.Get("ac")} · HP {p.Get("hp")} · Initiative {p.Get("initiative")} · Proficiency {p.Get("proficiency")}"
                + (p.ProgressionClass == "Spellcaster" ? $" · Spell attack {p.Get("spell-attack")} · Spell save DC {p.Get("spell-save-dc")}" : "")
        };
    }
}
