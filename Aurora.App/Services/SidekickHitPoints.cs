using System.Globalization;
using System.Text.RegularExpressions;
using Aurora.Components.Models;

namespace Aurora.App.Services;

public sealed class SidekickHitPointState
{
    // Base HP/hit dice already include this level. Older profiles start from their saved level.
    public int BaseLevel { get; set; } = 1;
    public string Method { get; set; } = "Average";
    public Dictionary<int, SidekickHitPointGain> Gains { get; set; } = new();
    public SidekickHitPointState Copy() => new()
    {
        BaseLevel = BaseLevel, Method = Method,
        Gains = Gains.ToDictionary(p => p.Key, p => new SidekickHitPointGain { Method = p.Value.Method, Roll = p.Value.Roll })
    };
}

public sealed class SidekickHitPointGain
{
    public string Method { get; set; } = "Average";
    public int? Roll { get; set; }
}

public static class SidekickHitPoints
{
    // Sidekick levels add the creature's own Hit Die, not a die chosen by its class:
    // https://www.dndbeyond.com/posts/1052-3-sidekicks-to-recruit-to-your-ragtag-party-of
    // Average is the optional rounded-up alternative requested for this app (d8 -> 5).
    public static void Initialize(SidekickProfile p)
    {
        if (p.Choices != null)
            p.Choices.HitPoints ??= new() { BaseLevel = SidekickChoices.Level(p) };
    }

    private static int Modifier(string value) => int.TryParse(value, out int score) ? (int)Math.Floor((score - 10) / 2d) : 0;
    private static string Base(SidekickProfile p, string key) => p.Choices!.Base.GetValueOrDefault(key, "");
    private static bool Dice(SidekickProfile p, out int count, out int sides)
    {
        string text = Base(p, "hit-dice");
        if (string.IsNullOrWhiteSpace(text)) text = Regex.Match(Base(p, "hp"), @"\b\d+\s*d(?:4|6|8|10|12|20)\b", RegexOptions.IgnoreCase).Value;
        var match = Regex.Match(text, @"^\s*(\d+)\s*d(4|6|8|10|12|20)(?:\s*[+-]\s*\d+)?\s*$", RegexOptions.IgnoreCase);
        count = sides = 0;
        return match.Success && int.TryParse(match.Groups[1].Value, out count) && count is > 0 and <= 1000
            && int.TryParse(match.Groups[2].Value, out sides);
    }
    private static bool BaseHp(SidekickProfile p, out int hp) =>
        int.TryParse(Regex.Match(Base(p, "hp"), @"^\s*(\d+)(?:\s*\([^)]*\))?\s*$").Groups[1].Value, out hp) && hp > 0;
    private static int Result(SidekickHitPointGain gain, int sides) => gain.Method == "Roll" ? gain.Roll ?? 0 : sides / 2 + 1;
    private static IEnumerable<KeyValuePair<int, SidekickHitPointGain>> Active(SidekickProfile p) =>
        p.Choices!.HitPoints!.Gains.Where(g => g.Key > p.Choices.HitPoints.BaseLevel && g.Key <= SidekickChoices.Level(p)).OrderBy(g => g.Key);

    internal static void Apply(SidekickProfile p, Action<string, string> apply)
    {
        Initialize(p);
        var state = p.Choices!.HitPoints!;
        bool hasDice = Dice(p, out int count, out int sides);
        bool hasHp = BaseHp(p, out int hp);
        if (hasDice && hasHp)
        {
            for (int level = Math.Clamp(state.BaseLevel, 1, 20) + 1; level <= SidekickChoices.Level(p); level++)
                if (!state.Gains.ContainsKey(level))
                    state.Gains[level] = new() { Method = state.Method, Roll = state.Method == "Roll" ? Random.Shared.Next(1, sides + 1) : null };
        }
        int extraDice = hasDice && hasHp ? Active(p).Count() : 0;
        apply("hit-dice", hasDice ? $"{count + extraDice}d{sides}" : Base(p, "hit-dice"));
        if (!hasHp) return;
        int con = Modifier(p.Get("constitution"));
        long total = hp + (long)(con - Modifier(Base(p, "constitution"))) * (hasDice ? count : 0);
        if (hasDice) total += Active(p).Sum(g => Math.Max(1, Result(g.Value, sides) + con));
        apply("hp", Math.Clamp(total, 1, int.MaxValue).ToString(CultureInfo.InvariantCulture));
    }

    public static void Change(SidekickProfile p, string[] key, string value)
    {
        Initialize(p);
        var state = p.Choices!.HitPoints!;
        switch (key[1])
        {
            case "method" when value is "Average" or "Roll": state.Method = value; break;
            case "base-level" when int.TryParse(value, out int at) && at is >= 1 and <= 20: state.BaseLevel = at; break;
            case "level" when key.Length == 4 && int.TryParse(key[2], out int level) && state.Gains.TryGetValue(level, out var gain):
                if (key[3] == "method" && value is "Average" or "Roll")
                {
                    gain.Method = value;
                    if (value == "Roll" && gain.Roll == null && Dice(p, out _, out int sides)) gain.Roll = Random.Shared.Next(1, sides + 1);
                }
                else if (key[3] == "roll") gain.Roll = int.TryParse(value, out int roll) ? roll : null;
                break;
        }
    }

    public static IReadOnlyList<string> Errors(SidekickProfile p)
    {
        var state = p.Choices?.HitPoints;
        if (state == null) return [];
        var errors = new List<string>();
        if (state.BaseLevel is < 1 or > 20) errors.Add("The starting HP level must be between 1 and 20.");
        if (state.Method is not ("Average" or "Roll")) errors.Add("Choose Average or Roll for new levels.");
        if (Dice(p, out _, out int sides))
            foreach (var (level, gain) in Active(p))
                if (gain.Method is not ("Average" or "Roll") || gain.Method == "Roll" && (gain.Roll == null || gain.Roll < 1 || gain.Roll > sides))
                    errors.Add($"Level {level}: enter a roll from 1 to {sides}, or choose Average.");
        return errors;
    }

    public static SidekickHitPointsModel Model(SidekickProfile p)
    {
        Initialize(p);
        var state = p.Choices!.HitPoints!;
        bool ready = Dice(p, out _, out int sides) && BaseHp(p, out _);
        int con = Modifier(p.Get("constitution"));
        return new()
        {
            BaseLevel = state.BaseLevel, Method = state.Method, DieSize = sides,
            Help = ready
                ? $"Each new level adds 1d{sides} + Constitution (minimum 1 HP). Average uses {sides / 2 + 1} before Constitution."
                : "Enter Base Maximum HP and Base Hit dice (for example, 2d8) to calculate level gains.",
            Gains = ready ? Active(p).Select(g => new SidekickHitPointRow(g.Key, g.Value.Method, g.Value.Roll,
                $"{Result(g.Value, sides)} {con.ToString("+0;-0;+0", CultureInfo.InvariantCulture)} Con = +{Math.Max(1, Result(g.Value, sides) + con)} HP")).ToList() : []
        };
    }
}
