using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Helpers;

public sealed record CharacterParitySnapshot(
    int Level,
    CombatSnapshot Combat,
    IReadOnlyDictionary<string, AbilityScoreSnapshot> AbilityScores,
    IReadOnlyList<ElementSnapshot> RegisteredElements,
    IReadOnlyList<SelectionRuleSnapshot> SelectionRules,
    IReadOnlyList<SpellcastingSnapshot> Spellcasting);

public sealed record CombatSnapshot(
    int ArmorClass,
    int MaxHp,
    int Initiative,
    int Speed,
    int FlySpeed,
    int ClimbSpeed,
    int SwimSpeed,
    int BurrowSpeed);

public sealed record AbilityScoreSnapshot(int Base, int Additional, int Final);

public sealed record ElementSnapshot(string Id, string Name, string Type, string Source);

public sealed record SelectionRuleSnapshot(
    string Type,
    string Name,
    string OwnerId,
    string OwnerName,
    string OwnerType,
    string Bucket,
    bool Optional,
    bool OptionalFlavor,
    int Number,
    int RequiredLevel,
    int OptionCount,
    IReadOnlyList<string> OptionIds,
    IReadOnlyList<string> SelectedIds);

public sealed record SpellcastingSnapshot(
    string Name,
    string SourceId,
    string Ability,
    bool Prepare,
    IReadOnlyList<string> PreparedIds,
    IReadOnlyList<string> AlwaysPreparedIds);

public static class CharacterParitySnapshotter
{
    public static CharacterParitySnapshot Capture()
    {
        var cm = CharacterManager.Current;
        var character = cm.Character
            ?? throw new InvalidOperationException("No current character is loaded.");

        var elements = cm.GetElements().ToList();
        var registeredElements = elements
            .Select(e => new ElementSnapshot(e.Id, e.Name ?? "", e.Type ?? "", e.Source ?? ""))
            .OrderBy(e => e.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectionRules = cm.SelectionRules
            .Select(CaptureRule)
            .OrderBy(r => r.Bucket, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.OwnerId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var spellcasting = cm.GetSpellcastingInformations()
            .Where(info => !info.IsExtension)
            .Select(info => new SpellcastingSnapshot(
                info.Name,
                info.ElementHeader?.Id ?? "",
                info.AbilityName,
                info.Prepare,
                GetPreparedIds(info.Name),
                GetAlwaysPreparedIds(elements, info.Name)))
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CharacterParitySnapshot(
            character.Level,
            CaptureCombat(cm, character),
            CaptureAbilities(character),
            registeredElements,
            selectionRules,
            spellcasting);
    }

    private static CombatSnapshot CaptureCombat(CharacterManager manager, Character character)
    {
        var values = manager.StatisticsCalculator.StatisticValues;
        return new CombatSnapshot(
            character.ArmorClass,
            character.MaxHp,
            character.Initiative,
            character.Speed,
            values.GetValue("speed:fly"),
            values.GetValue("speed:climb"),
            values.GetValue("speed:swim"),
            values.GetValue("speed:burrow"));
    }

    private static IReadOnlyList<string> GetPreparedIds(string spellcastingName) =>
        (SpellcastingSectionContext.Current?.GetPreparedIds(spellcastingName) ?? Array.Empty<string>())
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static IReadOnlyList<string> GetAlwaysPreparedIds(
        IEnumerable<ElementBase> elements,
        string spellcastingName) =>
        elements
            .Where(element => element.Type == "Spell")
            .Where(element => IsAlwaysPreparedFor(element, spellcastingName))
            .Select(element => element.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsAlwaysPreparedFor(ElementBase spell, string spellcastingName)
    {
        if (spell.Aquisition.WasGranted)
        {
            var rule = spell.Aquisition.GrantRule;
            return rule.IsAlwaysPrepared()
                && rule.Setters.ContainsSetter("spellcasting")
                && rule.Setters.GetSetter("spellcasting").Value.Equals(
                    spellcastingName,
                    StringComparison.OrdinalIgnoreCase);
        }

        if (spell.Aquisition.WasSelected)
        {
            var rule = spell.Aquisition.SelectRule;
            return rule.IsAlwaysPrepared()
                && rule.Attributes.ContainsSpellcastingName()
                && rule.Attributes.SpellcastingName.Equals(
                    spellcastingName,
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static IReadOnlyDictionary<string, AbilityScoreSnapshot> CaptureAbilities(Character character)
        => new Dictionary<string, AbilityScoreSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["Strength"] = CaptureAbility(character.Abilities.Strength),
            ["Dexterity"] = CaptureAbility(character.Abilities.Dexterity),
            ["Constitution"] = CaptureAbility(character.Abilities.Constitution),
            ["Intelligence"] = CaptureAbility(character.Abilities.Intelligence),
            ["Wisdom"] = CaptureAbility(character.Abilities.Wisdom),
            ["Charisma"] = CaptureAbility(character.Abilities.Charisma),
        };

    private static AbilityScoreSnapshot CaptureAbility(AbilityItem ability)
        => new(ability.BaseScore, ability.AdditionalScore, ability.FinalScore);

    private static SelectionRuleSnapshot CaptureRule(SelectRule rule)
    {
        var owner = ResolveOwner(rule);
        bool hasClassManager = CharacterManager.Current.ClassProgressionManagers
            .Any(manager => ReferenceEquals(manager, CharacterManager.Current.GetProgressManager(rule)));

        var bucket = BuildRuleClassifier.Classify(
            rule.Attributes.Type ?? "",
            rule.Attributes.Name ?? rule.Attributes.Type ?? "",
            owner?.Type ?? rule.ElementHeader?.Type ?? "",
            owner?.Name ?? rule.ElementHeader?.Name ?? "",
            hasClassManager);

        var optionIds = ResolveOptionIds(rule).ToList();
        string ruleName = rule.Attributes.Name ?? rule.Attributes.Type ?? "";

        return new SelectionRuleSnapshot(
            rule.Attributes.Type ?? "",
            ruleName,
            owner?.Id ?? rule.ElementHeader?.Id ?? "",
            owner?.Name ?? rule.ElementHeader?.Name ?? "",
            owner?.Type ?? rule.ElementHeader?.Type ?? "",
            bucket.ToString(),
            rule.Attributes.Optional,
            BuildRuleClassifier.IsOptionalFlavorSelection(ruleName),
            rule.Attributes.Number,
            rule.Attributes.RequiredLevel,
            optionIds.Count,
            optionIds,
            ResolveSelectedIds(rule));
    }

    private static IReadOnlyList<string> ResolveSelectedIds(SelectRule rule)
    {
        int slots = Math.Max(1, rule.Attributes.Number);
        var expander = SelectionRuleExpanderContext.Current;

        return Enumerable.Range(1, slots)
            .Select(number => ResolveSelectedId(expander?.GetRegisteredElement(rule, number)))
            .ToList();
    }

    private static string ResolveSelectedId(object? selected) => selected switch
    {
        ElementBase element => element.Id ?? string.Empty,
        SelectionRuleListItem listItem => listItem.ID.ToString(),
        string id => id,
        _ => string.Empty,
    };

    private static ElementBase? ResolveOwner(SelectRule rule)
    {
        var ownerId = rule.ElementHeader?.Id;
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        return CharacterManager.Current.GetElements()
                   .FirstOrDefault(e => e.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase))
               ?? DataManager.Current.ElementsCollection
                   .FirstOrDefault(e => e.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ResolveOptionIds(SelectRule rule)
    {
        if (rule.Attributes.IsList)
        {
            return rule.Attributes.ListItems?
                       .Select(item => item.ID.ToString())
                       .Where(id => !string.IsNullOrWhiteSpace(id))
                       .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                   ?? Enumerable.Empty<string>();
        }

        if (string.IsNullOrWhiteSpace(rule.Attributes.Type))
            return [];

        var baseCollection = DataManager.Current.ElementsCollection
            .Where(e => e.Type.Equals(rule.Attributes.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        IEnumerable<ElementBase> options;
        var interpreter = new ExpressionInterpreter();
        interpreter.InitializeWithSelectionRule(rule);

        if (!rule.Attributes.ContainsSupports())
        {
            options = baseCollection;
        }
        else
        {
            try
            {
                options = interpreter.EvaluateSupportsExpression<ElementBase>(
                    rule.Attributes.Supports,
                    baseCollection,
                    rule.Attributes.SupportsElementIdRange());
            }
            catch
            {
                options = ResolveSpellFallbackOptions(rule, baseCollection);
            }
        }

        if (rule.Attributes.Type.Equals("Spell", StringComparison.OrdinalIgnoreCase)
            && !options.Any()
            && (rule.Attributes.Supports?.Contains("$(", StringComparison.Ordinal) ?? false))
        {
            options = ResolveSpellFallbackOptions(rule, baseCollection);
        }

        var registeredIds = CharacterManager.Current.GetElements()
            .Select(e => e.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return options
            .Where(e => !e.HasRequirements || interpreter.EvaluateElementRequirementsExpression(e.Requirements, registeredIds))
            .OrderBy(e => rule.Attributes.Type.Equals("Spell", StringComparison.OrdinalIgnoreCase) ? GetSpellLevel(e) : 0)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<ElementBase> ResolveSpellFallbackOptions(
        SelectRule rule,
        IReadOnlyCollection<ElementBase> baseCollection)
    {
        string? spellcastingName = rule.Attributes.SpellcastingName;
        if (string.IsNullOrWhiteSpace(spellcastingName))
            spellcastingName = CharacterManager.Current.GetSpellcastingInformations()
                .FirstOrDefault(info => !info.IsExtension)?.Name;

        if (string.IsNullOrWhiteSpace(spellcastingName))
            return [];

        bool cantripOnly = rule.Attributes.Supports?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals("0", StringComparison.OrdinalIgnoreCase)) == true;

        return baseCollection
            .Where(e => e.Supports.Any(s => ContainsSupportToken(s, spellcastingName)))
            .Where(e => !cantripOnly || GetSpellLevel(e) == 0);
    }

    private static bool ContainsSupportToken(string supports, string token)
        => supports.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(value => value.Equals(token, StringComparison.OrdinalIgnoreCase));

    private static int GetSpellLevel(ElementBase element)
        => element is Spell spell ? spell.Level : 0;
}
