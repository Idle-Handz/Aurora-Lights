using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Data;
using Builder.Presentation;
using Builder.Data.Rules.Parsers;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;

namespace Aurora.App.Services;

/// <summary>Renders the active content rules using this companion's calculated values, not the primary creature's.</summary>
public static class CompanionProgression
{
    private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static string Signed(int value) => value.ToString("+0;-0;+0", CultureInfo.InvariantCulture);

    internal static void RefreshFields(Companion companion, Dictionary<string, string> fields)
    {
        var manager = CharacterManager.Current;
        var all = manager.GetElements().ToArray();
        var byId = all.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        IEnumerable<ElementBase> Ancestors(ElementBase element)
        {
            var visited = new HashSet<ElementBase>();
            while (visited.Add(element))
            {
                yield return element;
                var parent = all.FirstOrDefault(e => e.RuleElements.Contains(element));
                parent ??= byId.GetValueOrDefault(element.Aquisition.GetParentHeader()?.Id ?? "");
                if (parent == null) break;
                element = parent;
            }
        }
        var ancestors = Ancestors(companion.Element).ToArray();
        var owner = manager.ClassProgressionManagers.FirstOrDefault(p => p.ClassElement != null
            && ancestors.Any(a => a.Id == p.ClassElement.Id));
        var values = companion.Statistics.CalculatedValues;
        string className = owner?.ClassElement.Name.ToLowerInvariant() ?? "";
        int level = owner?.ProgressionLevel ?? 1;
        // Some legacy imports do not retain their acquisition chain. Explicit level references
        // still identify the correct class; never substitute total level for a class level.
        if (owner == null)
        {
            var reference = Regex.Match(companion.Element.HitPoints ?? "", @"your ([\w -]+) level", IgnoreCase);
            if (reference.Success && values.TryGetValue("level:" + reference.Groups[1].Value.ToLowerInvariant(), out int classLevel))
            {
                level = classLevel;
                className = reference.Groups[1].Value.ToLowerInvariant();
            }
        }
        fields["level"] = Math.Clamp(level, 1, 20).ToString();
        string Render(string text) => ResolveText(text, values, className);
        foreach (string key in new[] { "description", "saves", "skills", "senses", "languages", "immunities", "resistances", "vulnerabilities", "conditionImmunities", "conditionResistances", "conditionVulnerabilities" })
            fields[key] = Render(fields.GetValueOrDefault(key, ""));

        // Prefer the stat block's explicit PB formulas (including expertise) when present.
        // Other blocks use calculated skill/save bonuses supplied by their content rules.
        if (!Regex.IsMatch(companion.Element.Skills ?? "", @"\bPB\b", IgnoreCase))
            foreach (var skill in companion.Skills.GetCollection().Where(s => s.IsProficient || s.MiscBonus != 0))
                fields["skills"] = SetBonus(fields["skills"], skill.Name, skill.FinalBonus);
        if (!Regex.IsMatch(companion.Element.SavingThrows ?? "", @"\bPB\b", IgnoreCase))
            foreach (var save in companion.SavingThrows.GetCollection().Where(s => s.IsProficient || s.MiscBonus != 0))
                fields["saves"] = SetBonus(fields["saves"], save.KeyAbility.Name[..3], save.FinalBonus);
        // The original PHB Ranger's Companion content declares attack/AC/HP bonuses,
        // but omits its extra owner proficiency on proficient checks and saves.
        if (ancestors.Any(e => e.Id == "ID_WOTC_ARCHETYPEFEATURE_RANGER_RANGERS_COMPANION"))
        {
            int ownerProficiency = values.GetValueOrDefault("proficiency");
            fields["skills"] = ApplyProficientBonus(fields["skills"], companion.Element.Skills, ownerProficiency);
            fields["saves"] = ApplyProficientBonus(fields["saves"], companion.Element.SavingThrows, ownerProficiency);
        }
        var perception = Regex.Match(fields["skills"], @"\bPerception\s+([+-]?\d+)", IgnoreCase);
        if (perception.Success)
            fields["senses"] = Regex.Replace(fields["senses"], @"passive Perception\s+\d+", "passive Perception " + (10 + int.Parse(perception.Groups[1].Value)), IgnoreCase);

        if (!companion.Speed.IsUserInput)
        {
            string speed = Render(companion.Element.Speed ?? "");
            foreach (var (mode, amount) in new[] { ("", companion.Statistics.Speed), ("fly", companion.Statistics.SpeedFly),
                ("swim", companion.Statistics.SpeedSwim), ("climb", companion.Statistics.SpeedClimb), ("burrow", companion.Statistics.SpeedBurrow) })
            {
                if (amount <= 0) continue;
                string pattern = mode.Length == 0 ? @"^\s*\d+\s*(?:ft\.?)?" : @"\b" + mode + @"\s+\d+\s*ft\.?";
                string replacement = (mode.Length > 0 ? mode + " " : "") + amount + " ft.";
                speed = Regex.IsMatch(speed, pattern, IgnoreCase) ? Regex.Replace(speed, pattern, replacement, IgnoreCase)
                    : string.Join(", ", new[] { speed, replacement }.Where(s => s.Length > 0));
            }
            fields["speed"] = speed;
        }
        var hitDie = Regex.Match(companion.Element.HitPoints ?? "", @"hit dice\s*\[d(4|6|8|10|12|20)s?\]\s*equal to your ([\w -]+) level", IgnoreCase);
        if (hitDie.Success && values.TryGetValue("level:" + hitDie.Groups[2].Value.ToLowerInvariant(), out int dice))
            fields["hit-dice"] = dice + "d" + hitDie.Groups[1].Value;

        var resources = new List<CustomResource>();
        string Features(IEnumerable<string> ids, bool attacks = false) => string.Join("\n\n", ids.Select(id =>
        {
            var element = DataManager.Current.ElementsCollection.GetElement(id);
            if (element == null) return id;
            var sheet = AtLevel(element, level);
            string text = Plain(sheet?.Description ?? element.Description);
            if (attacks) text = ApplyAttackBonuses(text, companion.Statistics.AttackBonus, companion.Statistics.DamageBonus);
            string usage = Render(sheet?.HasUsage == true ? sheet.Usage : element.SheetDescription.Usage ?? "");
            var uses = Regex.Match(usage, @"^(\d+)\s*/\s*(Day|Long Rest|Short Rest|Short or Long Rest)$", IgnoreCase);
            if (uses.Success && int.TryParse(uses.Groups[1].Value, out int maximum) && maximum is > 0 and <= 99)
                resources.Add(new CustomResource { Id = "companion:" + element.Id, Name = element.Name, Max = maximum,
                    ResetOn = uses.Groups[2].Value.StartsWith("Short", StringComparison.OrdinalIgnoreCase) ? ResetOn.ShortRest : ResetOn.LongRest });
            return element.Name + (usage.Length > 0 ? " (" + usage + ")" : "") + "\n" + Render(text);
        }));
        fields["features"] = Features(companion.Element.Traits);
        fields["actions"] = Features(companion.Element.Actions, true);
        fields["reactions"] = Features(companion.Element.Reactions, true);
        fields["generated-resources"] = JsonSerializer.Serialize(resources.DistinctBy(r => r.Id));

        var branch = ancestors.FirstOrDefault(e => e.Type == "Archetype") ?? owner?.ClassElement;
        var scope = CompanionRuleScope.For(companion.Element, all);
        var names = new List<string> { companion.Element.Name, "companion" };
        if (companion.Element.Name.Contains("drake", StringComparison.OrdinalIgnoreCase)) names.Add("drake");
        if (companion.Element.Name.Contains("defender", StringComparison.OrdinalIgnoreCase)) names.Add("steel defender");
        if (companion.Element.CreatureType.Equals("Beast", StringComparison.OrdinalIgnoreCase)) names.AddRange(["beast", "animal companion", "primal companion"]);
        string creatureNames = string.Join("|", names.Distinct(StringComparer.OrdinalIgnoreCase).Select(Regex.Escape));
        fields["owner-features"] = branch == null ? "" : string.Join("\n\n", all.Where(e =>
                e.Type is "Class Feature" or "Archetype Feature" && e.SheetDescription.DisplayOnSheet && scope(e)
                && Ancestors(e).Any(a => a.Id == branch.Id)
                && (Regex.IsMatch(Plain(e.Description), @"\byour (?:" + creatureNames + @")\b", IgnoreCase)
                    || e.GetStatisticRules().Any(r => r.Attributes.Name.StartsWith("companion:", StringComparison.Ordinal))))
            .DistinctBy(e => e.Id).Select(e => e.Name + "\n" + Render(Plain(AtLevel(e, level)?.Description ?? e.Description))));
    }

    private static ElementSheetDescriptions.SheetDescription? AtLevel(ElementBase element, int level) =>
        element.SheetDescription.Where(d => d.Level <= level).OrderByDescending(d => d.Level).FirstOrDefault();

    private static string Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        try { return ElementDescriptionGenerator.GeneratePlainDescription(html).Trim(); }
        catch { return html; }
    }

    private static string SetBonus(string text, string name, int value)
    {
        string pattern = @"\b" + Regex.Escape(name) + @"\s+[+-]?\d+";
        string replacement = name + " " + Signed(value);
        return Regex.IsMatch(text, pattern, IgnoreCase) ? Regex.Replace(text, pattern, replacement, IgnoreCase)
            : string.Join(", ", new[] { text, replacement }.Where(s => s.Length > 0));
    }

    internal static string ApplyProficientBonus(string calculated, string? original, int ownerProficiency)
    {
        foreach (Match match in Regex.Matches(original ?? "", @"([A-Za-z][A-Za-z ]*)\s+([+-]\d+)(?=,|$)"))
        {
            string name = match.Groups[1].Value.Trim();
            int minimum = int.Parse(match.Groups[2].Value) + ownerProficiency;
            var current = Regex.Match(calculated, @"\b" + Regex.Escape(name) + @"\s+([+-]?\d+)", IgnoreCase);
            calculated = SetBonus(calculated, name, Math.Max(minimum, current.Success ? int.Parse(current.Groups[1].Value) : minimum));
        }
        return calculated;
    }

    internal static string ResolveText(string text, IReadOnlyDictionary<string, int> values, string ownerClass)
    {
        string Lookup(string key, string original) => values.TryGetValue(StatisticRuleParser.MapLegacyName(key.Trim()), out int value)
            ? value.ToString(CultureInfo.InvariantCulture) : original;
        text = Regex.Replace(text, @"\{\{(.*?)\}\}|\$\((.*?)\)", m => Lookup(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, m.Value));
        if (values.TryGetValue("proficiency", out int proficiency))
            text = Regex.Replace(text, @"\bPB\b|your proficiency bonus", proficiency.ToString(), IgnoreCase);
        text = Regex.Replace(text, @"your (Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma) modifier",
            m => Lookup(m.Groups[1].Value.ToLowerInvariant() + ":modifier", m.Value), IgnoreCase);
        text = Regex.Replace(text, @"your ([\w-]+) level", m => Lookup("level:" + m.Groups[1].Value.ToLowerInvariant(), m.Value), IgnoreCase);
        if (ownerClass.Length > 0)
        {
            if (values.TryGetValue(ownerClass + ":spellcasting:attack", out int attack))
                text = Regex.Replace(text, @"your spell attack (?:modifier|bonus)", Signed(attack), IgnoreCase);
            if (values.TryGetValue(ownerClass + ":spellcasting:dc", out int dc))
                text = Regex.Replace(text, @"your spell save DC", "DC " + dc, IgnoreCase);
        }
        text = Regex.Replace(text, @"\b(two|three|four|five) times (?=\d)", m => (m.Groups[1].Value.ToLowerInvariant() switch
            { "two" => "2", "three" => "3", "four" => "4", _ => "5" }) + " * ", IgnoreCase);
        text = Regex.Replace(text, @"(?<=\d)\s+plus\s+(?=[+-]?\d)", " + ", IgnoreCase);
        text = Regex.Replace(text, @"\bplus\s+(?=[+-]?\d)", "+ ", IgnoreCase);
        // Only reduce integer arithmetic, never dice, dates, or arbitrary expressions from content.
        text = Regex.Replace(text, @"(?<![\w.])[+-]?\d+\s*[×*]\s*\d+(?![\w.])", m =>
        {
            var n = Regex.Matches(m.Value, @"[+-]?\d+").Select(x => int.Parse(x.Value)).ToArray();
            return ((long)n[0] * n[1]).ToString(CultureInfo.InvariantCulture);
        });
        text = Regex.Replace(text, @"(?<![\w.])[+-]?\d+(?:\s+[+-]\s+[+-]?\d+)+(?![\w.])", m =>
        {
            var parts = Regex.Matches(m.Value, @"[+-]?\s*\d+").Select(x => int.Parse(x.Value.Replace(" ", ""))).ToArray();
            long sum = parts.Sum(n => (long)n);
            return m.Value.StartsWith('+') && sum >= 0 ? "+" + sum : sum.ToString(CultureInfo.InvariantCulture);
        });
        return text;
    }

    internal static string ApplyAttackBonuses(string text, int attack, int damage)
    {
        if (attack != 0)
            text = Regex.Replace(text, @"([+-]\d+)(?= to hit)", m => Signed(int.Parse(m.Value) + attack), IgnoreCase);
        if (damage != 0)
            text = Regex.Replace(text, @"(Hit:\s*)(?:(\d+)\s*\()?((?:\d+)d\d+)(?:\s*([+-])\s*(\d+))?", m =>
            {
                int bonus = (m.Groups[5].Success ? int.Parse(m.Groups[5].Value) * (m.Groups[4].Value == "-" ? -1 : 1) : 0) + damage;
                string average = m.Groups[2].Success ? (int.Parse(m.Groups[2].Value) + damage) + " (" : "";
                return m.Groups[1].Value + average + m.Groups[3].Value + (bonus == 0 ? "" : " " + (bonus > 0 ? "+ " : "- ") + Math.Abs(bonus));
            }, IgnoreCase);
        return text;
    }
}
