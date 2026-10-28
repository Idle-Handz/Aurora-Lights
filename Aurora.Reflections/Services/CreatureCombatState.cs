using System.Text.RegularExpressions;
using System.Text.Json;

namespace Aurora.Reflections.Services;

/// <summary>Independent, character-owned combat tracking for either kind of companion.</summary>
public sealed class CreatureCombatState
{
    public List<string> Conditions { get; set; } = [];
    public int Exhaustion { get; set; }
    public int HitDiceUsed { get; set; }
    public Dictionary<int, int> SpellSlotsUsed { get; set; } = [];
    public Dictionary<string, int> FeatureUses { get; set; } = [];
    public List<CustomResource> Resources { get; set; } = [];
    public CreatureCombatState Copy() => new()
    {
        Conditions = [.. Conditions], Exhaustion = Exhaustion, HitDiceUsed = HitDiceUsed,
        SpellSlotsUsed = new(SpellSlotsUsed), FeatureUses = new(FeatureUses),
        Resources = Resources.Select(r => new CustomResource
            { Id = r.Id, Name = r.Name, Max = r.Max, Used = r.Used, ResetOn = r.ResetOn }).ToList()
    };
}

public static class CreatureCombat
{
    public static IReadOnlyList<string> Conditions { get; } =
    [
        "Blinded", "Charmed", "Deafened", "Frightened", "Grappled", "Incapacitated", "Invisible",
        "Paralyzed", "Petrified", "Poisoned", "Prone", "Restrained", "Stunned", "Unconscious"
    ];

    public static int? MaximumHp(SidekickProfile profile)
    {
        var match = Regex.Match(profile.Get("hp"), @"^\s*(\d+)(?:\s*\([^)]*\))?\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value, out int hp) ? hp : null;
    }
    public static int CurrentHp(SidekickProfile profile) => int.TryParse(profile.Get("current-hp"), out int hp)
        ? Math.Clamp(hp, 0, MaximumHp(profile) ?? int.MaxValue) : MaximumHp(profile) ?? 0;
    public static int TemporaryHp(SidekickProfile profile) => int.TryParse(profile.Get("temporary-hp"), out int hp) ? Math.Max(0, hp) : 0;
    public static int HitDice(SidekickProfile profile)
    {
        var match = Regex.Match(profile.Get("hit-dice"), @"^\s*(\d+)\s*d(?:4|6|8|10|12|20)\b", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out int dice) ? dice : 0;
    }

    public static SortedDictionary<int, int> Slots(SidekickProfile profile)
    {
        var result = new SortedDictionary<int, int>();
        if (string.IsNullOrWhiteSpace(profile.Get("spell-slots"))) return result;
        foreach (string part in profile.Get("spell-slots").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(part, @"^([1-9])(?:st|nd|rd|th)?\s*:\s*(\d{1,2})$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException("Edit spell slots using counts such as 1st: 4; 2nd: 2 (levels 1–9, up to 99 each).");
            int level = int.Parse(match.Groups[1].Value);
            if (!result.TryAdd(level, int.Parse(match.Groups[2].Value)))
                throw new InvalidOperationException("Enter each spell slot level only once.");
        }
        return result;
    }

    public static IReadOnlyList<CustomResource> Resources(SidekickProfile profile)
    {
        var resources = profile.Combat.Resources.ToList();
        void Add(string id, string name, int max, ResetOn reset)
        {
            if (resources.Any(r => r.Id == id)) return;
            resources.Add(new CustomResource { Id = id, Name = name, Max = max,
                Used = profile.Combat.FeatureUses.GetValueOrDefault(id), ResetOn = reset });
        }
        if (profile.IsCompanion && profile.Get("generated-resources").Length > 0)
        {
            foreach (var resource in JsonSerializer.Deserialize<List<CustomResource>>(profile.Get("generated-resources")) ?? [])
                Add(resource.Id, resource.Name, resource.Max, resource.ResetOn);
        }
        if (!profile.IsCompanion && profile.ProgressionClass == "Warrior" && int.TryParse(profile.Get("level"), out int level))
        {
            if (level >= 2) Add("sidekick:second-wind", "Second Wind", level >= 20 ? 2 : 1, ResetOn.ShortRest);
            if (level >= 11) Add("sidekick:indomitable", "Indomitable", level >= 18 ? 2 : 1, ResetOn.LongRest);
        }
        return resources;
    }

    public static void Damage(SidekickProfile profile, int amount)
    {
        RequireHp(profile);
        if (amount < 0) throw new InvalidOperationException("Damage must be zero or more.");
        int absorbed = Math.Min(TemporaryHp(profile), amount);
        profile.Set("temporary-hp", (TemporaryHp(profile) - absorbed).ToString());
        profile.Set("current-hp", Math.Max(0, CurrentHp(profile) - (amount - absorbed)).ToString());
    }
    public static void Heal(SidekickProfile profile, int amount)
    {
        int max = RequireHp(profile);
        if (amount < 0) throw new InvalidOperationException("Healing must be zero or more.");
        profile.Set("current-hp", Math.Min(max, (long)CurrentHp(profile) + amount).ToString());
    }
    public static void GrantTemporaryHp(SidekickProfile profile, int amount)
    {
        if (amount < 0) throw new InvalidOperationException("Temporary HP must be zero or more.");
        profile.Set("temporary-hp", Math.Max(TemporaryHp(profile), amount).ToString());
    }
    public static void SetCondition(SidekickProfile profile, string condition, bool active)
    {
        if (!Conditions.Contains(condition)) throw new InvalidOperationException("Choose a condition from the list.");
        profile.Combat.Conditions.RemoveAll(c => c == condition);
        if (active) profile.Combat.Conditions.Add(condition);
    }
    public static void AdjustSlot(SidekickProfile profile, int level, int delta)
    {
        int max = Slots(profile).GetValueOrDefault(level);
        if (max <= 0) throw new InvalidOperationException("This creature has no slots at that level.");
        int used = profile.Combat.SpellSlotsUsed.GetValueOrDefault(level);
        profile.Combat.SpellSlotsUsed[level] = (int)Math.Clamp((long)used + delta, 0, max);
    }
    public static void AdjustResource(SidekickProfile profile, string id, int delta)
    {
        var resource = Resources(profile).FirstOrDefault(r => r.Id == id)
            ?? throw new InvalidOperationException("That resource is no longer available.");
        int used = (int)Math.Clamp((long)resource.Used + delta, 0, resource.Max);
        if (profile.Combat.Resources.FirstOrDefault(r => r.Id == id) is { } custom) custom.Used = used;
        else profile.Combat.FeatureUses[id] = used;
    }
    public static void SpendHitDie(SidekickProfile profile, int healing)
    {
        if (HitDice(profile) <= profile.Combat.HitDiceUsed) throw new InvalidOperationException("No hit dice remain.");
        Heal(profile, healing);
        profile.Combat.HitDiceUsed++;
    }

    // 2014 resting rules, matching the app's existing Session rules and Tasha's sidekicks:
    // https://www.dndbeyond.com/sources/dnd/basic-rules-2014/adventuring#Resting
    public static void Rest(SidekickProfile profile, bool longRest)
    {
        if (longRest)
        {
            profile.Set("current-hp", RequireHp(profile).ToString());
            profile.Set("temporary-hp", "0");
            profile.Combat.Exhaustion = Math.Max(0, profile.Combat.Exhaustion - 1);
            profile.Combat.HitDiceUsed = Math.Max(0, profile.Combat.HitDiceUsed - Math.Max(1, HitDice(profile) / 2));
        }
        string reset = profile.Get("slot-recovery");
        if (reset == "Short or long rest" || longRest && reset == "Long rest") profile.Combat.SpellSlotsUsed.Clear();
        foreach (var resource in Resources(profile))
            if (resource.ResetOn == ResetOn.ShortRest || longRest && resource.ResetOn == ResetOn.LongRest)
            {
                if (profile.Combat.Resources.FirstOrDefault(r => r.Id == resource.Id) is { } custom) custom.Used = 0;
                else profile.Combat.FeatureUses[resource.Id] = 0;
            }
        // Conditions have durations independent of rest. Never silently clear them or infer death saves.
    }
    private static int RequireHp(SidekickProfile profile) => MaximumHp(profile)
        ?? throw new InvalidOperationException("Edit Maximum HP to a number, optionally followed by hit dice in parentheses, before tracking HP.");
}
