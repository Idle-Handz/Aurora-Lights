using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System.Text.RegularExpressions;

namespace Builder.Presentation.Services;

public sealed record BuildSelectionOption(
    string Id,
    string Name,
    string Description,
    string Source = "",
    string Requirements = "",
    int SpellLevel = 0,
    string School = "",
    bool IsRitual = false,
    bool IsConcentration = false,
    DateTimeOffset? SourceReleaseDate = null,
    DateTimeOffset? SourceFileModifiedUtc = null,
    bool IsDisabled = false,
    bool IsCurrentSelection = false,
    string DescriptionMarkup = "");

public sealed record BuildSelectionOptionSortMetadata(
    DateTimeOffset? SourceReleaseDate,
    DateTimeOffset? SourceFileModifiedUtc);

public sealed class BuildSelectionOptionResolverSettings
{
    public IReadOnlyDictionary<string, IReadOnlySet<string>>? SpellAccessMap { get; init; }
    public Func<ElementBase, BuildSelectionOptionSortMetadata?>? SortMetadataSelector { get; init; }
    public Func<SelectRule, IEnumerable<ElementBase>>? ElementFallbackProvider { get; init; }
    public IReadOnlySet<string>? RestrictedElementIds { get; init; }
    public IReadOnlySet<string>? RestrictedSourceNames { get; init; }
}

public static class BuildSelectionOptionResolver
{
    public static IReadOnlyList<BuildSelectionOption> ResolveOptions(
        SelectRule rule,
        int number = 1,
        BuildSelectionOptionResolverSettings? settings = null)
    {
        settings ??= new BuildSelectionOptionResolverSettings();

        try
        {
            string? currentSelectionId = ResolveCurrentSelectionId(rule, number);

            if (rule.Attributes.IsList || string.Equals(rule.Attributes.Type, "List", StringComparison.OrdinalIgnoreCase))
            {
                return MarkCurrentSelection(
                    (rule.Attributes.ListItems ?? [])
                    .Select(item => new BuildSelectionOption(
                        item.ID.ToString(),
                        item.Text,
                        item.Text,
                        IsCurrentSelection: string.Equals(item.ID.ToString(), currentSelectionId, StringComparison.OrdinalIgnoreCase)))
                    .ToList(),
                    currentSelectionId);
            }

            var interpreter = new ExpressionInterpreter();
            interpreter.InitializeWithSelectionRule(rule);

            var baseCollection = DataManager.Current.ElementsCollection
                .Where(element => element.Type.Equals(rule.Attributes.Type));

            IEnumerable<ElementBase> elements;
            if (!rule.Attributes.ContainsSupports())
            {
                elements = baseCollection;
            }
            else
            {
                try
                {
                    elements = interpreter.EvaluateSupportsExpression<ElementBase>(
                        rule.Attributes.Supports,
                        baseCollection,
                        rule.Attributes.SupportsElementIdRange());
                }
                catch
                {
                    elements = SpellFallbackOptions(rule, baseCollection, settings.SpellAccessMap);
                }
            }

            bool isSpellRule = string.Equals(rule.Attributes.Type, "Spell", StringComparison.OrdinalIgnoreCase);
            IReadOnlySet<string> ownedNonRepeatableElementIds = GetOwnedNonRepeatableElementIds(rule);
            List<BuildSelectionOption> options = BuildElementOptions(
                elements,
                isSpellRule,
                currentSelectionId,
                ownedNonRepeatableElementIds,
                settings);

            if (options.Count == 0 && isSpellRule)
            {
                options = BuildElementOptions(
                    SpellFallbackOptions(rule, baseCollection, settings.SpellAccessMap),
                    isSpellRule: true,
                    currentSelectionId,
                    ownedNonRepeatableElementIds,
                    settings);
            }

            if (options.Count == 0
                && rule.Attributes.ContainsSupports()
                && !isSpellRule)
            {
                options = BuildElementOptions(
                    FilterBySupportsCaseInsensitive(rule.Attributes.Supports, baseCollection),
                    isSpellRule: false,
                    currentSelectionId,
                    ownedNonRepeatableElementIds,
                    settings);
            }

            List<BuildSelectionOption> deduplicated = DeduplicateOptions(options);
            if (deduplicated.Count == 0 && settings.ElementFallbackProvider is not null)
            {
                List<BuildSelectionOption> fallback = BuildElementOptions(
                    settings.ElementFallbackProvider(rule),
                    isSpellRule,
                    currentSelectionId,
                    ownedNonRepeatableElementIds,
                    settings);

                if (fallback.Count > 0)
                    return DeduplicateOptions(fallback);
            }

            return deduplicated;
        }
        catch
        {
            return [];
        }
    }

    private static HashSet<string> GetOwnedNonRepeatableElementIds(SelectRule rule)
    {
        try
        {
            if (SelectionRuleTypePolicy.AllowsStackedSelections(rule.Attributes.Type))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return CharacterManager.Current.GetElements()
                .Where(element =>
                    element.Type.Equals(rule.Attributes.Type, StringComparison.Ordinal) &&
                    !element.AllowDuplicate)
                .Select(element => element.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static List<BuildSelectionOption> BuildElementOptions(
        IEnumerable<ElementBase> elements,
        bool isSpellRule,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds,
        BuildSelectionOptionResolverSettings settings)
    {
        return OrderElementOptions(
                elements
                    .Where(element => !string.IsNullOrWhiteSpace(element.Name))
                    .Where(element => !IsSourceRestricted(element, settings))
                    .Select(element => CreateElementOption(
                        element,
                        isSpellRule,
                        currentSelectionId,
                        ownedNonRepeatableElementIds,
                        settings)),
                isSpellRule)
            .ToList();
    }

    private static bool IsSourceRestricted(
        ElementBase element,
        BuildSelectionOptionResolverSettings settings)
    {
        return settings.RestrictedElementIds?.Contains(element.Id) == true ||
               settings.RestrictedSourceNames?.Contains(element.Source ?? string.Empty) == true;
    }

    private static BuildSelectionOption CreateElementOption(
        ElementBase element,
        bool isSpellRule,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds,
        BuildSelectionOptionResolverSettings settings)
    {
        BuildSelectionOptionSortMetadata? metadata = settings.SortMetadataSelector?.Invoke(element);
        bool isCurrentSelection = string.Equals(element.Id, currentSelectionId, StringComparison.OrdinalIgnoreCase);

        return new BuildSelectionOption(
            element.Id,
            element.Name ?? string.Empty,
            isSpellRule ? GetSpellPickerDescription(element) : GetFeatureDescription(element),
            element.Source ?? string.Empty,
            element.HasRequirements ? FormatRequirements(element.Requirements) : string.Empty,
            SpellLevel: isSpellRule ? GetElementSpellLevel(element) : 0,
            School: isSpellRule ? GetElementSchool(element) : string.Empty,
            IsRitual: isSpellRule && GetElementIsRitual(element),
            IsConcentration: isSpellRule && GetElementIsConcentration(element),
            SourceReleaseDate: metadata?.SourceReleaseDate,
            SourceFileModifiedUtc: metadata?.SourceFileModifiedUtc,
            IsDisabled: SelectionOptionAvailability.IsDisabled(
                element.Id,
                element.AllowDuplicate,
                currentSelectionId,
                ownedNonRepeatableElementIds),
            IsCurrentSelection: isCurrentSelection,
            DescriptionMarkup: isSpellRule ? string.Empty : GetRawDescription(element));
    }

    private static IOrderedEnumerable<BuildSelectionOption> OrderElementOptions(
        IEnumerable<BuildSelectionOption> options,
        bool isSpellRule)
    {
        return options
            .OrderBy(option => isSpellRule ? option.SpellLevel : 0)
            .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(option => option.SourceReleaseDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(option => option.SourceFileModifiedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(option => option.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static List<BuildSelectionOption> DeduplicateOptions(List<BuildSelectionOption> options)
    {
        var result = new List<BuildSelectionOption>(options.Count);
        foreach (var group in options.GroupBy(option => (option.Name, option.Description)))
        {
            if (group.Count() == 1)
            {
                result.Add(group.First());
                continue;
            }

            string combinedSources = string.Join(", ",
                group.Select(option => option.Source)
                    .Where(source => !string.IsNullOrEmpty(source))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(source => source));

            BuildSelectionOption representative = group.FirstOrDefault(option => option.IsCurrentSelection)
                ?? group.FirstOrDefault(option => !option.IsDisabled)
                ?? group.First();
            result.Add(representative with { Source = combinedSources });
        }

        return result;
    }

    private static IReadOnlyList<BuildSelectionOption> MarkCurrentSelection(
        IReadOnlyList<BuildSelectionOption> options,
        string? currentSelectionId)
    {
        if (string.IsNullOrWhiteSpace(currentSelectionId))
            return options;

        return options
            .Select(option => option with
            {
                IsCurrentSelection = string.Equals(
                    option.Id,
                    currentSelectionId,
                    StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private static string? ResolveCurrentSelectionId(SelectRule rule, int number)
    {
        try
        {
            return SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, number) switch
            {
                ElementBase element => element.Id,
                SelectionRuleListItem listItem => listItem.ID.ToString(),
                string id => id,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<ElementBase> FilterBySupportsCaseInsensitive(
        string supportsExpression,
        IEnumerable<ElementBase> elements)
    {
        var terms = Regex.Matches(supportsExpression, @"[A-Za-z][A-Za-z0-9_]*")
            .Cast<Match>()
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (terms.Count == 0)
            return Enumerable.Empty<ElementBase>();

        return elements.Where(element =>
            element.Supports is not null &&
            element.Supports.Any(support => terms.Any(term =>
                support.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)));
    }

    private static IEnumerable<ElementBase> SpellFallbackOptions(
        SelectRule rule,
        IEnumerable<ElementBase> spellBase,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? spellAccessMap)
    {
        bool isCantrip = false;
        if (rule.Attributes.ContainsSupports())
            isCantrip = rule.Attributes.Supports.Contains("Cantrip", StringComparison.OrdinalIgnoreCase);
        if (!isCantrip)
            isCantrip = rule.Attributes.Name?.Contains("Cantrip", StringComparison.OrdinalIgnoreCase) == true;

        string? className = ResolveSpellFallbackClassName(rule);
        if (className is null)
            return [];

        int maxSpellLevel = 9;
        if (!isCantrip && (rule.Attributes.Supports?.Contains("$(spellcasting:slots)", StringComparison.OrdinalIgnoreCase) ?? false))
            maxSpellLevel = ResolveMaxCastableSpellLevel(className);

        var spells = spellBase as IReadOnlyList<ElementBase> ?? spellBase.ToList();
        var matches = new List<ElementBase>();

        if (spellAccessMap?.TryGetValue(className, out IReadOnlySet<string>? spellIds) == true)
        {
            matches.AddRange(spells.Where(element =>
            {
                if (!spellIds.Contains(element.Id))
                    return false;

                int level = GetElementSpellLevel(element);
                return isCantrip ? level == 0 : level > 0 && level <= maxSpellLevel;
            }));
        }

        matches.AddRange(spells.Where(element =>
        {
            if (element.Supports is null || !element.Supports.Any(support =>
                    support.Contains(className, StringComparison.OrdinalIgnoreCase)))
                return false;

            int level = GetElementSpellLevel(element);
            return isCantrip ? level == 0 : level > 0 && level <= maxSpellLevel;
        }));

        return matches
            .GroupBy(element => element.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static string? ResolveSpellFallbackClassName(SelectRule rule)
    {
        if (rule.Attributes.ContainsSpellcastingName())
            return rule.Attributes.SpellcastingName;

        string? ownerSpellcastingName = ResolveOwnerSpellcastingName(rule);
        if (!string.IsNullOrWhiteSpace(ownerSpellcastingName))
            return ownerSpellcastingName;

        if (!rule.Attributes.ContainsSupports())
            return null;

        string supports = Regex.Replace(rule.Attributes.Supports ?? string.Empty, @"\$\([^)]*\)", " ");
        supports = Regex.Replace(supports, @"ID_[A-Za-z0-9_]+", " ");
        string firstWord = Regex.Match(supports, @"[A-Za-z][A-Za-z0-9 ]*").Value.Trim();
        return string.IsNullOrWhiteSpace(firstWord) || int.TryParse(firstWord, out _)
            ? null
            : firstWord;
    }

    private static string? ResolveOwnerSpellcastingName(SelectRule rule)
    {
        string? ownerId = rule.ElementHeader?.Id;
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        ElementBase? owner = DataManager.Current.ElementsCollection
            .FirstOrDefault(element => element.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase));
        return owner?.HasSpellcastingInformation == true
            ? owner.SpellcastingInformation.Name
            : null;
    }

    private static int ResolveMaxCastableSpellLevel(string spellcastingClassName)
    {
        try
        {
            var manager = CharacterManager.Current;
            if (manager.Status.HasMulticlassSpellSlots)
            {
                dynamic slots = manager.Character.MulticlassSpellSlots;
                int[] values =
                [
                    0,
                    (int)slots.Slot1,
                    (int)slots.Slot2,
                    (int)slots.Slot3,
                    (int)slots.Slot4,
                    (int)slots.Slot5,
                    (int)slots.Slot6,
                    (int)slots.Slot7,
                    (int)slots.Slot8,
                    (int)slots.Slot9
                ];
                for (int level = 9; level >= 1; level--)
                {
                    if (values[level] > 0)
                        return level;
                }
            }

            var stats = manager.StatisticsCalculator.StatisticValues;
            var info = manager.GetSpellcastingInformations()
                .FirstOrDefault(candidate => candidate.Name.Equals(spellcastingClassName, StringComparison.OrdinalIgnoreCase));
            if (info is null)
                return 9;

            int maxLevel = 0;
            for (int level = 1; level <= 9; level++)
            {
                try
                {
                    if (stats.GetValue(info.GetSlotStatisticName(level)) > 0)
                        maxLevel = level;
                }
                catch
                {
                }
            }

            return maxLevel > 0 ? maxLevel : 9;
        }
        catch
        {
            return 9;
        }
    }

    private static string FormatRequirements(string requirements)
    {
        if (string.IsNullOrWhiteSpace(requirements))
            return string.Empty;

        var tokens = Regex
            .Split(requirements, @"[,;]+|&&|\|\|")
            .Select(part => part.Trim(' ', '!', '(', ')'));

        var parts = new List<string>();
        foreach (string token in tokens)
        {
            if (string.IsNullOrEmpty(token))
                continue;

            Match match = Regex.Match(token, @"^\[(\w+):(\d+)\]$");
            if (match.Success)
            {
                string key = match.Groups[1].Value.ToLowerInvariant();
                string value = match.Groups[2].Value;
                parts.Add(key switch
                {
                    "str" => $"STR {value}+",
                    "dex" => $"DEX {value}+",
                    "con" => $"CON {value}+",
                    "int" => $"INT {value}+",
                    "wis" => $"WIS {value}+",
                    "cha" => $"CHA {value}+",
                    "level" => $"Level {value}",
                    _ => $"{key.ToUpperInvariant()} {value}"
                });
                continue;
            }

            if (token.StartsWith("ID_", StringComparison.OrdinalIgnoreCase))
            {
                ElementBase? element = DataManager.Current.ElementsCollection
                    .FirstOrDefault(candidate => candidate.Id.Equals(token, StringComparison.OrdinalIgnoreCase));
                if (element is not null && !string.IsNullOrWhiteSpace(element.Name))
                    parts.Add(element.Name);
                continue;
            }

            if (token.Contains('[') || token.Contains(':'))
                continue;

            parts.Add(token);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    private static string GetFeatureDescription(object element)
    {
        try
        {
            dynamic dynamicElement = element;
            string raw = (string)(dynamicElement.Description ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(raw))
                return ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string GetRawDescription(object element)
    {
        try
        {
            dynamic dynamicElement = element;
            string raw = (string)(dynamicElement.Description ?? string.Empty);
            return element is ElementBase elementBase
                ? SelectionDescriptionMarkup.WithFeatureProgression(elementBase, raw)
                : raw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetSpellPickerDescription(ElementBase element)
    {
        if (element is not Spell spell)
            return GetFeatureDescription(element);

        try
        {
            int level = spell.Level;
            string school = spell.MagicSchool ?? string.Empty;
            string castingTime = spell.CastingTime ?? string.Empty;
            string range = spell.Range ?? string.Empty;
            string duration = spell.Duration ?? string.Empty;
            string components = spell.GetComponentsString() ?? string.Empty;
            bool ritual = spell.IsRitual;
            bool concentration = spell.IsConcentration;
            string body = string.Empty;

            string raw = spell.Description ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(raw))
                body = ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();

            var builder = new System.Text.StringBuilder();
            string levelText = level == 0
                ? string.IsNullOrEmpty(school) ? "Cantrip" : $"{school} Cantrip"
                : string.IsNullOrEmpty(school) ? $"Level {level}" : $"Level {level} {school}";
            if (concentration)
                levelText += " - Concentration";
            if (ritual)
                levelText += " - Ritual";

            builder.AppendLine(levelText);
            if (!string.IsNullOrEmpty(castingTime))
                builder.AppendLine($"Casting Time: {castingTime}");
            if (!string.IsNullOrEmpty(range))
                builder.AppendLine($"Range: {range}");
            if (!string.IsNullOrEmpty(components))
                builder.AppendLine($"Components: {components}");
            if (!string.IsNullOrEmpty(duration))
                builder.AppendLine($"Duration: {duration}");
            if (!string.IsNullOrEmpty(body))
            {
                builder.AppendLine();
                builder.Append(body);
            }

            return builder.ToString().Trim();
        }
        catch
        {
            return GetFeatureDescription(element);
        }
    }

    private static int GetElementSpellLevel(ElementBase element) =>
        element is Spell spell ? spell.Level : 0;

    private static string GetElementSchool(ElementBase element) =>
        element is Spell spell ? spell.MagicSchool ?? string.Empty : string.Empty;

    private static bool GetElementIsRitual(ElementBase element) =>
        element is Spell spell && spell.IsRitual;

    private static bool GetElementIsConcentration(ElementBase element) =>
        element is Spell spell && spell.IsConcentration;
}
