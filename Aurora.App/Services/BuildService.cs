using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System.Text.RegularExpressions;
using System.Xml;

namespace Aurora.App.Services;

/// <summary>
/// Logic layer for the Build page — rule enumeration, option lookup, selection apply, and re-snapshotting.
/// Also owns the SnapshotProgressionManagers / GetFeatureDescription helpers (moved here from Start.razor)
/// so they can be called from both Start.razor (on load) and Build.razor (after editing).
/// </summary>
public static partial class BuildService
{
    // ── SelectionRule groups ─────────────────────────────────────────────────

    /// <summary>
    /// Returns all active SelectionRules grouped by the progression manager that owns them.
    /// "Character" group = main progression manager (Race, Background, general choices).
    /// One group per class = that class's progression manager (Archetype, Feats, Infusions, etc.)
    /// Spell selection rules are excluded — spells are managed on the Magic page.
    /// </summary>
    public static IReadOnlyList<SelectionRuleGroup> GetRuleGroups()
    {
        var cm       = CharacterManager.Current;
        var classMgrs = cm.ClassProgressionManagers;

        var mainEntries  = new List<SelectionRuleEntry>();
        var classEntries = new Dictionary<ClassProgressionManager, List<SelectionRuleEntry>>();

        foreach (var rule in cm.SelectionRules)
        {
            // Spells are managed on the Magic page, not here.
            if (rule.Attributes.Type == "Spell") continue;

            var pm       = cm.GetProgressManager(rule);
            var classMgr = classMgrs.FirstOrDefault(m => ReferenceEquals(m, pm));

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                string? currentName = ResolveCurrentSelectionName(rule, n);
                string ruleType = rule.Attributes.Type ?? "Other";
                string ruleName = rule.Attributes.Name ?? ruleType;
                string label = rule.Attributes.Number > 1
                    ? $"{ruleName} ({n})"
                    : ruleName;

                var entry = new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(rule, n, ruleType, ruleName));

                if (classMgr != null)
                {
                    if (!classEntries.ContainsKey(classMgr))
                        classEntries[classMgr] = [];
                    classEntries[classMgr].Add(entry);
                }
                else
                {
                    mainEntries.Add(entry);
                }
            }
        }

        var groups = new List<SelectionRuleGroup>();

        if (mainEntries.Count > 0)
        {
            groups.Add(new SelectionRuleGroup(
                "Character",
                mainEntries.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList()));
        }

        foreach (var m in classMgrs)
        {
            if (!classEntries.TryGetValue(m, out var entries) || entries.Count == 0) continue;
            string groupLabel = m.ClassElement?.Name ?? "Class";
            groups.Add(new SelectionRuleGroup(
                groupLabel,
                entries.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList()));
        }

        return groups;
    }

    // ── Spell SelectionRule groups (for Magic page) ──────────────────────────

    /// <summary>
    /// Returns SelectionRule groups containing only Spell-type rules — the mirror of
    /// GetRuleGroups() but restricted to Spell type. Used by the Magic page to allow
    /// selecting/changing known spells for known-caster classes.
    /// </summary>
    public static IReadOnlyList<SelectionRuleGroup> GetSpellRuleGroups()
    {
        var cm      = CharacterManager.Current;

        var byClass = new Dictionary<string, List<SelectionRuleEntry>>(StringComparer.Ordinal);

        foreach (var rule in cm.SelectionRules)
        {
            if (rule.Attributes.Type != "Spell") continue;

            var pm       = cm.GetProgressManager(rule);
            var classMgr = cm.ClassProgressionManagers.FirstOrDefault(m => ReferenceEquals(m, pm));
            string groupName = classMgr?.ClassElement?.Name ?? "Spells";

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                string? currentName = ResolveCurrentSelectionName(rule, n);
                int spellLevel = 0;
                string? spellId = null;
                try
                {
                    var regEl = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n)
                        as Builder.Data.ElementBase;
                    if (regEl != null)
                    {
                        spellLevel = GetElementSpellLevel(regEl);
                        spellId = regEl.Id;
                    }
                }
                catch { }

                string ruleType = rule.Attributes.Type ?? "Spell";
                string ruleName = rule.Attributes.Name ?? ruleType;
                string label = rule.Attributes.Number > 1
                    ? $"{ruleName} ({n})"
                    : ruleName;

                if (!byClass.ContainsKey(groupName))
                    byClass[groupName] = [];
                byClass[groupName].Add(new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(rule, n, ruleType, ruleName), spellLevel, spellId));
            }
        }

        return byClass
            .Select(kv => new SelectionRuleGroup(
                kv.Key,
                kv.Value.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList()))
            .ToList();
    }

    // ── Options for a rule ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the full list of valid options for a SelectRule, filtered from DataManager.
    /// Uses ElementsOrganizerRefactored which applies the rule's supports expression.
    /// </summary>
    public static IReadOnlyList<ElementOption> GetOptions(SelectRule rule, int number = 1)
    {
        try
        {
            string? currentSelectionId = ResolveCurrentSelectionId(rule, number);

            // List-type rules (Bond, Ideal, Flaw, Personality Trait) store their options as
            // inline <item> children in the XML, not as elements in the element collection.
            bool isList = rule.Attributes.Type?.Equals("List") == true;
            if (isList)
            {
                var listItems = rule.Attributes.ListItems;
                DebugLogService.Instance.Log(LogLevel.Info,
                    $"[GetOptions] isList=true name={rule.Attributes.Name} " +
                    $"listItems={(listItems == null ? "null" : listItems.Count.ToString())} " +
                    $"elemId={rule.ElementHeader?.Id}");
                if (listItems?.Count > 0)
                    return MarkCurrentSelection(listItems
                        .Select(li => new ElementOption(li.ID.ToString(), li.Text, li.Text, "", ""))
                        .ToList(), currentSelectionId);
                // ListItems not populated — read directly from the owner element's XML node.
                var fromElementNode = GetListOptionsFromElementNode(rule);
                return fromElementNode.Count > 0
                    ? MarkCurrentSelection(fromElementNode, currentSelectionId)
                    : MarkCurrentSelection(XmlContentFallbackService.GetListFallbackOptions(rule), currentSelectionId);
            }

            var ownedNonRepeatableElementIds = GetOwnedNonRepeatableElementIds(rule);

            // Use the same approach as SelectionRuleCollectionService / SelectionRuleComboBoxViewModel:
            // • InitializeWithSelectionRule so level-based expressions can resolve
            // • Pass SupportsElementIdRange() as the containsElementIDs flag (correct vs ElementsOrganizerRefactored's heuristic)
            var interpreter = new ExpressionInterpreter();
            interpreter.InitializeWithSelectionRule(rule);

            var baseCollection = DataManager.Current.ElementsCollection
                .Where(e => e.Type.Equals(rule.Attributes.Type));

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
                    // Supports expression may contain unsupported macros (e.g., "$(spellcasting:list)").
                    // Fall back to a direct DataManager filter using the spellcasting class name.
                    elements = SpellFallbackOptions(rule, baseCollection);
                }
            }

            // If the expression evaluated but returned nothing (can happen with macro expressions),
            // also try the spell fallback for Spell-type rules.
            bool isSpellRule = rule.Attributes.Type?.Equals("Spell", StringComparison.OrdinalIgnoreCase) == true;
            var list = BuildElementOptions(
                elements,
                isSpellRule,
                currentSelectionId,
                ownedNonRepeatableElementIds);

            if (list.Count == 0 && isSpellRule)
                list = BuildElementOptions(
                    SpellFallbackOptions(rule, baseCollection),
                    isSpellRule: true,
                    currentSelectionId: currentSelectionId,
                    ownedNonRepeatableElementIds: ownedNonRepeatableElementIds);

            // Case-insensitive fallback: only used when the main expression returned nothing AND
            // this is not a Spell rule (Spell rules use SpellFallbackOptions above). Catches
            // content that uses internal ID aliases like ID_INTERNAL_SUPPORT_LANGUAGE_EXOTIC
            // whose plain-word token ("Exotic") fails the evaluator's case-sensitive Contains.
            // Running it as a union (even when the main evaluator found results) risks adding
            // wrong elements that pass substring-matching but fail proper rule validation on reload.
            if (list.Count == 0 && rule.Attributes.ContainsSupports()
                && !string.Equals(rule.Attributes.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            {
                list = BuildElementOptions(
                    FilterBySupportsCaseInsensitive(rule.Attributes.Supports, baseCollection),
                    isSpellRule: false,
                    currentSelectionId: currentSelectionId,
                    ownedNonRepeatableElementIds: ownedNonRepeatableElementIds);
            }

            var deduplicated = DeduplicateOptions(list);
            if (deduplicated.Count == 0)
            {
                var xmlFallback = BuildElementOptions(
                    XmlContentFallbackService.GetElementFallbacks(rule),
                    isSpellRule,
                    currentSelectionId,
                    ownedNonRepeatableElementIds);

                if (xmlFallback.Count > 0)
                    return DeduplicateOptions(xmlFallback);
            }

            return deduplicated;
        }
        catch { return []; }
    }

    // Fallback for list-type rules when Attributes.ListItems is empty: walks the owner element's
    // XmlNode to find the matching <select type="List" name="…"> and reads its <item> children.
    private static IReadOnlyList<ElementOption> GetListOptionsFromElementNode(SelectRule rule)
    {
        if (rule.ElementHeader == null)
        {
            DebugLogService.Instance.Log(LogLevel.Warning,
                $"[GetListOptionsFromElementNode] ElementHeader is null for rule '{rule.Attributes.Name}'");
            return [];
        }

        var element = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id == rule.ElementHeader.Id);
        if (element == null)
        {
            DebugLogService.Instance.Log(LogLevel.Warning,
                $"[GetListOptionsFromElementNode] element not found for id '{rule.ElementHeader.Id}' (rule '{rule.Attributes.Name}')");
            return [];
        }

        if (element.ElementNode == null)
        {
            DebugLogService.Instance.Log(LogLevel.Warning,
                $"[GetListOptionsFromElementNode] ElementNode is null for '{element.Id}' (rule '{rule.Attributes.Name}')");
            return [];
        }

        XmlNode? rulesSection = element.ElementNode["rules"];
        if (rulesSection == null)
        {
            DebugLogService.Instance.Log(LogLevel.Warning,
                $"[GetListOptionsFromElementNode] no <rules> section in element '{element.Id}' (rule '{rule.Attributes.Name}')");
            return [];
        }

        string? ruleName = rule.Attributes.Name;
        foreach (XmlNode child in rulesSection.ChildNodes)
        {
            if (child.Name != "select") continue;
            if (child.Attributes?["type"]?.Value != "List") continue;
            if (ruleName != null && child.Attributes?["name"]?.Value != ruleName) continue;

            var items = new List<ElementOption>();
            foreach (XmlNode item in child.ChildNodes)
            {
                if (item.Name != "item") continue;
                string id = item.Attributes?["id"]?.Value ?? "";
                string text = item.InnerText.Trim();
                if (!string.IsNullOrEmpty(text))
                    items.Add(new ElementOption(id, text, text, "", ""));
            }
            DebugLogService.Instance.Log(LogLevel.Info,
                $"[GetListOptionsFromElementNode] found {items.Count} items for '{ruleName}' in element '{element.Id}'");
            return items;
        }
        DebugLogService.Instance.Log(LogLevel.Warning,
            $"[GetListOptionsFromElementNode] no matching <select type='List' name='{ruleName}'> found in '{element.Id}'");
        return [];
    }

    /// <summary>
    /// Filters elements whose Supports field contains any of the label terms parsed from
    /// <paramref name="supportsExpression"/>, using case-insensitive matching. This catches
    /// elements that use internal ID aliases like ID_INTERNAL_SUPPORT_LANGUAGE_EXOTIC which
    /// contain "exotic" but not the mixed-case literal "Exotic" that the expression uses.
    /// </summary>
    private static IEnumerable<ElementBase> FilterBySupportsCaseInsensitive(
        string supportsExpression, IEnumerable<ElementBase> elements)
    {
        var terms = Regex.Matches(supportsExpression, @"[A-Za-z][A-Za-z0-9_]*")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (terms.Count == 0) return Enumerable.Empty<ElementBase>();

        return elements.Where(e =>
            e.Supports != null &&
            e.Supports.Any(s => terms.Any(t => s.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)));
    }

    /// <summary>
    /// Collapses options with the same Name+Description (same content from multiple sources)
    /// into a single entry with combined source names. Options with the same name but different
    /// descriptions are kept as separate entries.
    /// </summary>
    private static List<ElementOption> DeduplicateOptions(List<ElementOption> options)
    {
        var result = new List<ElementOption>(options.Count);
        foreach (var group in options.GroupBy(o => (o.Name, o.Description)))
        {
            if (group.Count() == 1)
            {
                result.Add(group.First());
            }
            else
            {
                // Same name + description from multiple sources — collapse and combine source names.
                var combined = string.Join(", ",
                    group.Select(o => o.Source)
                         .Where(s => !string.IsNullOrEmpty(s))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(s => s));
                // Keep an actionable option whenever duplicate source records differ in
                // availability. The current slot wins over another available source.
                var representative = group.FirstOrDefault(o => o.IsCurrentSelection)
                    ?? group.FirstOrDefault(o => !o.IsDisabled)
                    ?? group.First();
                result.Add(representative with { Source = combined });
            }
        }
        return result;
    }

    private static List<ElementOption> BuildElementOptions(
        IEnumerable<ElementBase> elements,
        bool isSpellRule,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds)
    {
        return OrderElementOptions(
                elements
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .Select(e => CreateElementOption(
                        e,
                        isSpellRule,
                        currentSelectionId,
                        ownedNonRepeatableElementIds)),
                isSpellRule)
            .ToList();
    }

    private static ElementOption CreateElementOption(
        ElementBase element,
        bool isSpellRule,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds)
    {
        var metadata = TryGetElementSortMetadata(element);
        bool isCurrentSelection = string.Equals(
            element.Id,
            currentSelectionId,
            StringComparison.OrdinalIgnoreCase);

        return new ElementOption(
            element.Id,
            element.Name ?? "",
            isSpellRule ? GetSpellPickerDescription(element) : GetFeatureDescription(element),
            element.Source ?? "",
            element.HasRequirements ? FormatRequirements(element.Requirements) : "",
            SpellLevel: isSpellRule ? GetElementSpellLevel(element) : 0,
            School: isSpellRule ? GetElementSchool(element) : "",
            IsRitual: isSpellRule && GetElementIsRitual(element),
            IsConcentration: isSpellRule && GetElementIsConcentration(element),
            SourceReleaseDate: metadata?.SourceReleaseDate,
            SourceFileModifiedUtc: metadata?.SourceFileModifiedUtc,
            IsDisabled: SelectionOptionAvailability.IsDisabled(
                element.Id,
                element.AllowDuplicate,
                currentSelectionId,
                ownedNonRepeatableElementIds),
            IsCurrentSelection: isCurrentSelection);
    }

    private static HashSet<string> GetOwnedNonRepeatableElementIds(SelectRule rule)
    {
        try
        {
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

    private static IReadOnlyList<ElementOption> MarkCurrentSelection(
        IReadOnlyList<ElementOption> options,
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
                    StringComparison.OrdinalIgnoreCase),
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
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static IOrderedEnumerable<ElementOption> OrderElementOptions(
        IEnumerable<ElementOption> options,
        bool isSpellRule)
    {
        return options
            .OrderBy(o => isSpellRule ? o.SpellLevel : 0)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(o => o.SourceReleaseDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(o => o.SourceFileModifiedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(o => o.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static DbElementLoader.ElementSortMetadata? TryGetElementSortMetadata(ElementBase element)
    {
        DbElementLoader.ElementSortMetadataMap.TryGetValue(
            DbElementLoader.MakeElementSortMetadataKey(element.Id, element.Source),
            out var metadata);
        return metadata;
    }

    /// <summary>
    /// Converts a raw Aurora requirements expression into a concise human-readable string.
    /// Handles <c>[ability:value]</c> tokens (e.g., <c>[str:13]</c> → "STR 13+") and
    /// <c>[level:N]</c> tokens. Internal element IDs and boolean operators are stripped.
    /// </summary>
    private static string FormatRequirements(string requirements)
    {
        if (string.IsNullOrWhiteSpace(requirements)) return "";

        // Split on comma, &&, ||, then trim whitespace and grouping chars.
        var tokens = System.Text.RegularExpressions.Regex
            .Split(requirements, @"[,;]+|&&|\|\|")
            .Select(p => p.Trim(' ', '!', '(', ')'));

        var parts = new List<string>();
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token)) continue;

            // [ability:value] or [level:N]
            var m = System.Text.RegularExpressions.Regex.Match(token, @"^\[(\w+):(\d+)\]$");
            if (m.Success)
            {
                string key = m.Groups[1].Value.ToLowerInvariant();
                string val = m.Groups[2].Value;
                parts.Add(key switch
                {
                    "str"   => $"STR {val}+",
                    "dex"   => $"DEX {val}+",
                    "con"   => $"CON {val}+",
                    "int"   => $"INT {val}+",
                    "wis"   => $"WIS {val}+",
                    "cha"   => $"CHA {val}+",
                    "level" => $"Level {val}",
                    _       => $"{key.ToUpperInvariant()} {val}",
                });
                continue;
            }

            // Element IDs — look up the name from DataManager so the user sees something meaningful.
            // Skip IDs that resolve to nothing (internal flags, generated IDs, etc.).
            if (token.StartsWith("ID_", StringComparison.OrdinalIgnoreCase))
            {
                var element = DataManager.Current.ElementsCollection
                    .FirstOrDefault(e => e.Id.Equals(token, StringComparison.OrdinalIgnoreCase));
                if (element != null && !string.IsNullOrWhiteSpace(element.Name))
                    parts.Add(element.Name!);
                continue;
            }

            if (token.Contains('[') || token.Contains(':')) continue;

            // Plain text (e.g., class/feature names embedded directly).
            parts.Add(token);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "";
    }

    /// <summary>
    /// Fallback option loader for Spell-type rules whose supports expression uses macros
    /// (e.g., "$(spellcasting:list)") that the expression parser cannot evaluate.
    /// Uses SpellcastingInformation or raw supports text to filter DataManager spells directly.
    /// </summary>
    private static IEnumerable<ElementBase> SpellFallbackOptions(
        SelectRule rule, IEnumerable<ElementBase> spellBase)
    {
        // Determine cantrip vs levelled spell.
        // Supports expressions use $(spellcasting:list), 0 for cantrips — the macro text
        // doesn't contain "Cantrip", so we also check the rule name.
        bool isCantrip = false;
        if (rule.Attributes.ContainsSupports())
            isCantrip = rule.Attributes.Supports.Contains("Cantrip", StringComparison.OrdinalIgnoreCase);
        if (!isCantrip)
            isCantrip = rule.Attributes.Name?.Contains("Cantrip", StringComparison.OrdinalIgnoreCase) == true;

        // Prefer the spellcasting class name from the rule attribute; fall back to parsing
        // the supports expression for the first plain word (strips macros like "$(...)").
        string? className = ResolveSpellFallbackClassName(rule);

        if (className == null) return [];

        string scName = className;

        // When the rule uses $(spellcasting:slots), restrict to spells the character
        // can actually cast at their current level (prevents a Sorcerer 1 from seeing
        // 9th-level spells in the picker).
        int maxSpellLevel = 9;
        if (!isCantrip && (rule.Attributes.Supports?.Contains("$(spellcasting:slots)", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            maxSpellLevel = ResolveMaxCastableSpellLevel(scName);
        }

        var spells = spellBase as IReadOnlyList<ElementBase> ?? spellBase.ToList();
        var matches = new List<ElementBase>();

        // Fast path: use the pre-resolved spell access map from the DB loader. Then union in
        // the live supports scan so custom/user spells and append-added supports can override
        // or augment the SQLite projection without being hidden by a non-empty DB map result.
        if (DbElementLoader.SpellAccessMap.TryGetValue(scName, out var spellIds))
        {
            matches.AddRange(spells.Where(e =>
            {
                if (!spellIds.Contains(e.Id)) return false;
                int lvl = GetElementSpellLevel(e);
                return isCantrip ? lvl == 0 : (lvl > 0 && lvl <= maxSpellLevel);
            }));
        }

        // Text-based scan: filter by class name in supports attribute.
        // Use Any+Contains (substring, case-insensitive) because supports values may be
        // comma-joined strings like "Ranger, Paladin" rather than individual entries.
        matches.AddRange(spells.Where(e =>
        {
            if (e.Supports == null || !e.Supports.Any(s => s.Contains(scName, StringComparison.OrdinalIgnoreCase)))
                return false;
            int lvl = GetElementSpellLevel(e);
            return isCantrip ? lvl == 0 : (lvl > 0 && lvl <= maxSpellLevel);
        }));

        return matches
            .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());
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

        string supports = Regex.Replace(rule.Attributes.Supports ?? "", @"\$\([^)]*\)", " ");
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

        var owner = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase));
        return owner?.HasSpellcastingInformation == true
            ? owner.SpellcastingInformation.Name
            : null;
    }

    /// <summary>
    /// Returns the maximum spell level the character can currently cast for the given
    /// spellcasting class, based on available spell slots. Returns 9 on any failure so
    /// the filter degrades gracefully rather than blocking all spell options.
    /// </summary>
    private static int ResolveMaxCastableSpellLevel(string spellcastingClassName)
    {
        try
        {
            var cm = CharacterManager.Current;
            // Multiclass spell slots are pooled — use the combined table.
            if (cm.Status.HasMulticlassSpellSlots)
            {
                dynamic mss = cm.Character.MulticlassSpellSlots;
                int[] slots = { 0, (int)mss.Slot1, (int)mss.Slot2, (int)mss.Slot3,
                                   (int)mss.Slot4, (int)mss.Slot5, (int)mss.Slot6,
                                   (int)mss.Slot7, (int)mss.Slot8, (int)mss.Slot9 };
                for (int i = 9; i >= 1; i--)
                    if (slots[i] > 0) return i;
            }
            var stats = cm.StatisticsCalculator.StatisticValues;
            var info = cm.GetSpellcastingInformations()
                .FirstOrDefault(i => i.Name.Equals(spellcastingClassName, StringComparison.OrdinalIgnoreCase));
            if (info == null) return 9;
            int maxLevel = 0;
            for (int lvl = 1; lvl <= 9; lvl++)
            {
                try { if (stats.GetValue(info.GetSlotStatisticName(lvl)) > 0) maxLevel = lvl; }
                catch { }
            }
            return maxLevel > 0 ? maxLevel : 9;
        }
        catch { return 9; }
    }

    // ── Apply selection + validate + save ────────────────────────────────────

    /// <summary>
    /// Applies a new selection, re-validates all other selections, saves the full character
    /// file (not just text patches), then rebuilds the tab snapshot. The whole pipeline runs
    /// on a background thread so the UI stays responsive.
    ///
    /// Returns a list of rule labels for selections that were removed during validation
    /// so the caller can notify the user.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ApplySelectionAndSaveAsync(
        SelectRule rule, string elementId, int number, CharacterTab tab,
        Builder.Presentation.Models.CharacterFile file,
        bool saveToFile = true)
    {
        using var scope = await CharacterContext.EnterAsync(tab);

        var invalidated = new List<string>();

        string? taskError = null;
        bool needsRollback = false;
        bool selectionMutationStarted = false;

        await Task.Run(() =>
        {
            try
            {
            var cm = CharacterManager.Current;

            // 1. Register the new selection (handler also unregisters the previous one).
            EnsureSelectionCandidateIsAvailable(rule, elementId, number);
            selectionMutationStarted = true;
            SelectionRuleExpanderContext.Current?.SetRegisteredElement(rule, elementId, number);
            ClearConflictingOriginAbilityScoreSelections(rule, invalidated);
            ClearExistingOriginAbilityScoreCollisions(invalidated);

            // 2. Re-run progression processing so grant rules and requirement checks fire.
            cm.ReprocessCharacter();

            NormalizeSelectionState(invalidated);

            // 3. Validate every other SelectionRule: check if its registered element is
            //    still a valid option. If not, unregister it to avoid an inconsistent state.
            var currentIds = cm.GetElements().Select(e => e.Id).ToHashSet();

            foreach (var r in cm.SelectionRules.ToList())
            {
                if (r.Attributes.Type == "Spell") continue; // spell management is on Magic page

                // If the rule's owner element is no longer registered, all its selections are
                // stale — clear them without checking individual option validity.
                var ownerId = r.ElementHeader?.Id;
                if (!string.IsNullOrWhiteSpace(ownerId) && !currentIds.Contains(ownerId))
                {
                    for (int n = 1; n <= r.Attributes.Number; n++)
                    {
                        var stale = SelectionRuleExpanderContext.Current?.GetRegisteredElement(r, n)
                            as Builder.Data.ElementBase;
                        if (stale == null) continue;
                        DebugLogService.Instance.Log(LogLevel.Warning,
                            $"[Validate] owner-missing: rule='{r.Attributes.Name ?? r.Attributes.Type}' " +
                            $"ownerId='{ownerId}' ownerType='{r.ElementHeader?.Type}' " +
                            $"stale='{stale.Id}' currentIds.Count={currentIds.Count}");
                        try
                        {
                            cm.UnregisterElement(stale);
                            SelectionRuleExpanderContext.Current?.ClearRegisteredElement(r, n);
                        }
                        catch { }
                        string staleLabel = r.Attributes.Number > 1
                            ? $"{r.Attributes.Name ?? r.Attributes.Type} ({n})"
                            : (r.Attributes.Name ?? r.Attributes.Type);
                        invalidated.Add(staleLabel);
                    }
                    continue;
                }

                int count = r.Attributes.Number;
                for (int n = 1; n <= count; n++)
                {
                    var registered = SelectionRuleExpanderContext.Current?.GetRegisteredElement(r, n)
                        as Builder.Data.ElementBase;
                    if (registered == null) continue;

                    bool stillValid;
                    HashSet<string> validIds = [];
                    try
                    {
                        validIds = GetValidSelectionIds(r, registered.Id, currentIds);
                        stillValid = validIds.Contains(registered.Id);
                    }
                    catch { stillValid = true; } // be conservative on errors

                    if (!stillValid)
                    {
                        // If the entire supported set is empty, our evaluation can't enumerate
                        // valid candidates (e.g. no elements with that supports tag in the loaded
                        // dataset, or an unrecognised supports expression like "Custom Race Language").
                        // Validation is meaningless in this case — preserve the user's selection
                        // rather than silently clearing a choice that was valid when it was made.
                        if (validIds.Count == 0)
                            continue;

                        DebugLogService.Instance.Log(LogLevel.Warning,
                            $"[Validate] slot-invalid: rule='{r.Attributes.Name ?? r.Attributes.Type}' " +
                            $"type='{r.Attributes.Type}' supports='{r.Attributes.Supports}' " +
                            $"registered='{registered.Id}' validIds.Count={validIds.Count} " +
                            $"ownerId='{ownerId}'");
                        try
                        {
                            cm.UnregisterElement(registered);
                            SelectionRuleExpanderContext.Current?.ClearRegisteredElement(r, n);
                        }
                        catch { }

                        string label = r.Attributes.Number > 1
                            ? $"{r.Attributes.Name ?? r.Attributes.Type} ({n})"
                            : (r.Attributes.Name ?? r.Attributes.Type);
                        invalidated.Add(label);
                    }
                }
            }

            // 4. Reprocess after any invalidations.
            if (invalidated.Count > 0)
                cm.ReprocessCharacter();

            // 4.5. For background list selections (Bond, Ideal, Flaw, Personality Trait),
            //      CharacterManager.SetCharacterDetails has now populated
            //      FillableBackgroundCharacteristics from SelectionRuleListItems.
            //      Copy those into the snapshot so FlushSnapshotToCharacter (step 5) writes
            //      them back to the character's editable text fields.
            if (rule.Attributes.IsList && tab.Snapshot != null)
            {
                var bgChar = cm.Character.FillableBackgroundCharacteristics;
                if (!string.IsNullOrEmpty(bgChar.Traits.Content)) tab.Snapshot.Notes1 = bgChar.Traits.Content;
                if (!string.IsNullOrEmpty(bgChar.Ideals.Content)) tab.Snapshot.Notes2 = bgChar.Ideals.Content;
                if (!string.IsNullOrEmpty(bgChar.Bonds.Content))  tab.Snapshot.Allies = bgChar.Bonds.Content;
                if (!string.IsNullOrEmpty(bgChar.Flaws.Content))  tab.Snapshot.Organisation = bgChar.Flaws.Content;
            }

            // 5. Flush snapshot text edits back into the Character object so they
            //    survive a full Save() which regenerates the XML from CharacterManager state.
            if (tab.Snapshot != null && tab.Character != null)
                FlushSnapshotToCharacter(tab.Snapshot, tab.Character);

            // 6. Full save — rebuilds entire XML from CharacterManager.Current state.
            if (saveToFile)
                SaveCharacterFile(tab, file);
            }
            catch (CharacterFileExternalChangeException ex)
            {
                DebugLogService.Instance.LogException(ex, "BuildService.ApplySelectionAsync");
                taskError = ex.Message;
                needsRollback = true;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "BuildService.ApplySelectionAsync");
                // Include the innermost stack frame so we can pinpoint the NRE location.
                var firstFrame = ex.StackTrace?.Split('\n')
                    .FirstOrDefault(l => l.TrimStart().StartsWith("at "))?.Trim() ?? "";
                taskError = $"{ex.GetType().Name}: {ex.Message} | {firstFrame}";
                needsRollback = saveToFile && selectionMutationStarted;
            }
        });

        if (taskError != null)
            invalidated.Add($"[Error: {taskError}]");

        // On external-change failure the selection was NOT saved; roll back CharacterManager
        // to the on-disk state so ResnapTab sees the actual persisted state, not the mutation
        // we couldn't write.
        if (needsRollback)
        {
            try { await CharacterContext.ReloadFromDiskAsync(tab); }
            catch (Exception reloadEx)
            {
                DebugLogService.Instance.LogException(reloadEx, "BuildService.ApplySelectionAsync rollback");
            }
        }

        // 7. Rebuild the in-memory snapshot and progression list.
        ResnapTab(tab);

        return invalidated;
    }

    // ── Manual save ─────────────────────────────────────────────────────────

    /// <summary>
    /// Flushes snapshot text edits into the Character object and performs a full
    /// CharacterFile.Save(). Called from the AppBar save button and the close-tab
    /// dialog so that build-page changes (which modify CharacterManager state rather
    /// than the raw XML) are always captured correctly.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public static async Task<string?> SaveTabAsync(CharacterTab tab)
    {
        if (tab.File == null) return "No file associated with this tab.";
        using var scope = await CharacterContext.EnterAsync(tab);
        string? error = null;
        await Task.Run(() =>
        {
            try
            {
                SaveCharacterFile(tab);
            }
            catch (Exception ex)
            {
                error = DebugLogService.Catch(ex, "BuildService.SaveTabAsync");
            }
        });
        return error;
    }

    /// <summary>
    /// Pushes the editable text fields from a CharacterSnapshot back into the live
    /// Character object so that a full CharacterFile.Save() includes them.
    /// Called before Save() since Save() reads directly from the Character object.
    /// </summary>
    private static void FlushSnapshotToCharacter(
        CharacterSnapshot snap, Builder.Presentation.Models.Character character)
    {
        character.Name            = snap.Name;
        character.PlayerName      = snap.PlayerName;
        character.Experience      = snap.Experience;
        character.Alignment       = snap.Alignment;
        character.Deity           = snap.Deity;
        character.Backstory       = snap.Backstory;
        character.OrganisationName = snap.Organisation;
        character.Allies          = snap.Allies;
        character.Notes1          = snap.Notes1;
        character.Notes2          = snap.Notes2;
        character.Gender          = snap.Gender;
        character.Eyes            = snap.Eyes;
        character.Skin            = snap.Skin;
        character.Hair            = snap.Hair;
        // FillableField properties used by CharacterFile.Save()
        character.AgeField.Content    = snap.Age;
        character.HeightField.Content = snap.Height;
        character.WeightField.Content = snap.Weight;
        character.BackgroundStory.Content = snap.Backstory;
        if (!string.IsNullOrEmpty(snap.Trinket))
            character.Trinket.Content = snap.Trinket;
        character.Inventory.Equipment  = snap.InventoryEquipmentText;
        character.Inventory.Treasure   = snap.InventoryTreasureText;
        character.Inventory.QuestItems = snap.InventoryQuestText;
        character.Inventory.Coins.Set(snap.CoinCopper, snap.CoinSilver, snap.CoinElectrum, snap.CoinGold, snap.CoinPlatinum);
    }

    private static void EnsureMulticlassSelectionsRegistered()
    {
        var expander = SelectionRuleExpanderContext.Current;
        if (expander == null)
            return;

        foreach (var manager in CharacterManager.Current.ClassProgressionManagers
                     .Where(m => m.IsMulticlass && m.ClassElement != null && m.SelectRule != null))
        {
            var registered = expander.GetRegisteredElement(manager.SelectRule) as ElementBase;
            if (registered?.Id.Equals(manager.ClassElement.Id, StringComparison.OrdinalIgnoreCase) == true)
                continue;

            expander.SetRegisteredElement(manager.SelectRule, manager.ClassElement.Id);
        }
    }

    /// <summary>
    /// Applies the same option-availability policy used by the picker at the mutation
    /// boundary. This protects saves triggered from stale UI state, import flows, or future
    /// callers that do not pass through the picker UI.
    /// </summary>
    private static void EnsureSelectionCandidateIsAvailable(SelectRule rule, string elementId, int number)
    {
        var option = GetOptions(rule, number)
            .FirstOrDefault(candidate => candidate.Id.Equals(elementId, StringComparison.OrdinalIgnoreCase));

        if (option is null)
        {
            throw new InvalidOperationException(
                "That selection is no longer available. Reload the character and choose an available option.");
        }

        if (option.IsDisabled)
        {
            throw new InvalidOperationException(
                $"'{option.Name}' is already selected and cannot be selected again.");
        }
    }

    private static void SaveCharacterFile(
        CharacterTab tab,
        Builder.Presentation.Models.CharacterFile? explicitFile = null)
    {
        Builder.Presentation.Models.CharacterFile? targetFile = explicitFile ?? tab.File;
        if (targetFile is null)
            throw new InvalidOperationException("No file associated with this tab.");

        CharacterFileWriteCoordinator.Write(tab.FileSaveSemaphore, targetFile, "Character", () =>
        {
            if (tab.Snapshot != null && tab.Character != null)
                FlushSnapshotToCharacter(tab.Snapshot, tab.Character);

            EnsureMulticlassSelectionsRegistered();
            NormalizeSelectionState();

            // CharacterFile.Save() rebuilds the document from the model, dropping app-specific
            // root nodes such as <custom-features>. Preserve that node across the rebuild so
            // multiple custom additions (and adds interleaved with other build edits) keep their
            // tracking rather than each save clobbering the previous list.
            var customFeatures = targetFile.LoadCustomFeatures();

            targetFile.Save();

            if (customFeatures.Count > 0 && !targetFile.SaveCustomFeatures(customFeatures))
                throw new InvalidOperationException("Character save completed, but custom feature tracking could not be restored.");

            // Session state lives in a JSON sidecar (SessionStore), so a full save can't drop
            // it any more. Refreshing it here keeps the sidecar in step with the character file
            // and makes any save-to-a-new-path carry the session along automatically.
            SessionStore.Save(targetFile.FilePath, tab.Session);

            if (tab.Snapshot != null && !targetFile.SaveTextEdits(tab.Snapshot))
                throw new InvalidOperationException("Character save completed, but snapshot-backed edits could not be patched into the file.");

            return true;
        }).ThrowIfFailed();
    }

    // ── Re-snapshot ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the snapshot and progression snapshots for a tab after a selection change.
    /// Assumes CharacterManager.Current corresponds to the tab being edited.
    /// </summary>
    public static void ResnapTab(CharacterTab tab)
    {
        if (tab.Character == null) return;
        tab.Snapshot             = CharacterSnapshot.From(tab.Character);
        tab.ProgressionSnapshots = SnapshotProgressionManagers();
    }

    public static IReadOnlyList<string> NormalizeSelectionState()
    {
        var invalidated = new List<string>();
        NormalizeSelectionState(invalidated);
        return invalidated;
    }

    private static void NormalizeSelectionState(List<string> invalidated)
    {
        int countBefore = invalidated.Count;
        ClearStaleSelectedAbilityScoreElements(invalidated);
        if (invalidated.Count > countBefore)
            CharacterManager.Current.ReprocessCharacter();
    }

    // ── Snapshot helpers (shared with Start.razor) ───────────────────────────

    /// <summary>
    /// Builds ClassProgressionSnapshot list from CharacterManager.Current.
    /// Called at load time (Start.razor) and after edits (Build.razor).
    /// </summary>
    public static IReadOnlyList<ClassProgressionSnapshot> SnapshotProgressionManagers()
    {
        var cm = CharacterManager.Current;

        // Build feat lookup: SelectRule object → FeatureEntry for the actual chosen feat.
        // Feats are stored in the main ProgressionManager (default case in RegisterElement),
        // not in any ClassProgressionManager. We match them back to their class by SelectRule ref.
        var featByRule = new Dictionary<object, FeatureEntry>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var e in cm.GetElements().Where(e => e.Type == "Feat"))
        {
            try
            {
                dynamic d = e;
                if (!(bool)d.Aquisition.WasSelected) continue;
                object rule     = d.Aquisition.SelectRule;
                string ruleName = (string)(d.Aquisition.SelectRule.Attributes.Name ?? "");
                string label    = string.IsNullOrEmpty(ruleName) ? e.Name ?? "" : $"{ruleName}: {e.Name}";
                featByRule[rule] = new FeatureEntry(label, GetFeatureDescription(e));
            }
            catch { }
        }

        return cm.ClassProgressionManagers
            .Select(m =>
            {
                var features = m.GetElements()
                    .Where(e => (e.Type == "Class Feature" || e.Type == "Archetype Feature") &&
                                !string.IsNullOrWhiteSpace(e.Name) &&
                                !e.Name.StartsWith("Ability Score Increase") &&
                                !e.Name.StartsWith("Ability Score Improvement") &&
                                !e.Name.Equals("Feat", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(e => e.Id)
                    .Select(g => g.First())
                    .Select(e => new FeatureEntry(e.Name!, GetFeatureDescription(e)))
                    .ToList();

                foreach (var rule in m.SelectionRules)
                {
                    if (featByRule.TryGetValue(rule, out var featEntry))
                        features.Add(featEntry);
                }

                return new ClassProgressionSnapshot(
                    m.ClassElement?.Name ?? string.Empty,
                    m.HD ?? "—",
                    m.ProgressionLevel,
                    m.IsMainClass,
                    features);
            })
            .ToList();
    }

    /// <summary>
    /// Extracts a plain-text description from an element via GeneratePlainDescription,
    /// which properly inserts paragraph breaks between &lt;p&gt; elements.
    /// SheetDescription is skipped — it's a flat PDF-oriented text blob without structure.
    /// </summary>
    public static string GetFeatureDescription(object e)
    {
        try
        {
            dynamic el = e;
            string raw = (string)(el.Description ?? "");
            if (!string.IsNullOrWhiteSpace(raw))
                return ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();
        }
        catch { }
        return "";
    }

    private static string GetSpellPickerDescription(ElementBase e)
    {
        if (e is not Spell sp) return GetFeatureDescription(e);
        try
        {
            int level          = sp.Level;
            string school      = sp.MagicSchool ?? "";
            string castingTime = sp.CastingTime ?? "";
            string range       = sp.Range ?? "";
            string duration    = sp.Duration ?? "";
            string components  = sp.GetComponentsString() ?? "";
            bool ritual        = sp.IsRitual;
            bool concentration = sp.IsConcentration;
            bool body_ok       = false;
            string body        = "";

            string raw = sp.Description ?? "";
            if (!string.IsNullOrWhiteSpace(raw))
            { body = ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim(); body_ok = true; }

            var sb = new System.Text.StringBuilder();
            string levelText = level == 0
                ? (string.IsNullOrEmpty(school) ? "Cantrip" : $"{school} Cantrip")
                : (string.IsNullOrEmpty(school) ? $"Level {level}" : $"Level {level} {school}");
            if (concentration) levelText += " · Concentration";
            if (ritual)        levelText += " · Ritual";
            sb.AppendLine(levelText);

            if (!string.IsNullOrEmpty(castingTime)) sb.AppendLine($"Casting Time: {castingTime}");
            if (!string.IsNullOrEmpty(range))       sb.AppendLine($"Range: {range}");
            if (!string.IsNullOrEmpty(components))  sb.AppendLine($"Components: {components}");
            if (!string.IsNullOrEmpty(duration))    sb.AppendLine($"Duration: {duration}");
            if (body_ok && !string.IsNullOrEmpty(body)) { sb.AppendLine(); sb.Append(body); }

            return sb.ToString().Trim();
        }
        catch { return GetFeatureDescription(e); }
    }

    // Spell-typed elements are always Builder.Data.Elements.Spell instances (verified by
    // SpellTypeInvariantTests), so read their properties via a static cast rather than dynamic — a
    // renamed/removed member becomes a compile error instead of a silently-swallowed wrong value.
    private static int GetElementSpellLevel(ElementBase e) => e is Spell sp ? sp.Level : 0;

    private static string GetElementSchool(ElementBase e) => e is Spell sp ? sp.MagicSchool ?? "" : "";

    private static bool GetElementIsRitual(ElementBase e) => e is Spell sp && sp.IsRitual;

    private static bool GetElementIsConcentration(ElementBase e) => e is Spell sp && sp.IsConcentration;

    // ── Advancement timeline ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a per-level breakdown of features for each class in the current character,
    /// adapting the shared advancement query into Reflections display models and descriptions.
    /// </summary>
    public static IReadOnlyList<AdvancementClassTimeline> GetAdvancementTimeline()
    {
        return AdvancementTimelineQuery.Build(CharacterManager.Current)
            .Select(timeline => new AdvancementClassTimeline(
                timeline.ClassName,
                timeline.HitDie,
                timeline.IsMainClass,
                timeline.Levels.Select(level => new AdvancementLevelEntry(
                        level.Level,
                        level.AverageHp,
                        level.Features.Select(feature =>
                                new FeatureEntry(feature.Name!, GetFeatureDescription(feature)))
                            .ToList()))
                    .ToList()))
            .ToList();
    }

    // ── Spell detail lookup ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns full detail for a single spell, looked up by element ID from DataManager.
    /// Accesses structured Spell properties (CastingTime, Range, Duration, Underline,
    /// GetComponentsString) via dynamic dispatch on the Builder.Data.Elements.Spell runtime type.
    /// Returns null if the spell is not found.
    /// </summary>
    public static SpellDetail? GetSpellDetail(string id)
    {
        try
        {
            var e = DataManager.Current.ElementsCollection
                .FirstOrDefault(x => x.Type == "Spell" &&
                                     x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (e is not Spell sp) return null;

            int    level       = sp.Level;
            string subtitle    = sp.Underline ?? "";   // e.g. "1st-level abjuration (ritual)"
            string school      = sp.MagicSchool ?? "";
            string castingTime = sp.CastingTime ?? "";
            string range       = sp.Range ?? "";
            string duration    = sp.Duration ?? "";
            string components  = sp.GetComponentsString() ?? "";
            bool ritual        = sp.IsRitual;
            bool concentration = sp.IsConcentration;

            // Body description — use the plain-text generator on the raw XML description.
            string body = "";
            try
            {
                string raw = sp.Description ?? "";
                if (!string.IsNullOrWhiteSpace(raw))
                    body = ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();
            }
            catch { }

            // If structured fields came back empty, fall back to parsing "Key: Value" lines
            // from the description (some content packs embed them inline).
            if (string.IsNullOrEmpty(castingTime) && string.IsNullOrEmpty(range))
            {
                var lines    = body.Split('\n').Select(l => l.Trim()).ToList();
                var bodyLeft = new List<string>();
                foreach (var line in lines)
                {
                    if (TryParseSpellHeader(line, "Casting Time", out var ct))  { castingTime = ct; continue; }
                    if (TryParseSpellHeader(line, "Range",        out var rng)) { range       = rng; continue; }
                    if (TryParseSpellHeader(line, "Components",   out var cmp)) { components  = cmp; continue; }
                    if (TryParseSpellHeader(line, "Duration",     out var dur)) { duration    = dur; continue; }
                    bodyLeft.Add(line);
                }
                body = string.Join("\n", bodyLeft).Trim();
            }

            return new SpellDetail(
                Id:          e.Id,
                Name:        e.Name ?? "",
                Source:      e.Source ?? "",
                Level:       level,
                Subtitle:    subtitle,
                School:      school,
                Ritual:      ritual,
                Concentration: concentration,
                CastingTime: castingTime,
                Range:       range,
                Components:  components,
                Duration:    duration,
                Description: body);
        }
        catch { return null; }
    }

    private static bool TryParseSpellHeader(string line, string key, out string value)
    {
        string prefix = key + ":";
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }
        value = "";
        return false;
    }

    // ── Level management ─────────────────────────────────────────────────────────

    /// <summary>True when the character can gain another level (below 20).</summary>
    public static bool CanLevelUp   => CharacterManager.Current.Status.CanLevelUp;

    /// <summary>True when the character can lose a level (above 1).</summary>
    public static bool CanLevelDown => CharacterManager.Current.Status.CanLevelDown;

    /// <summary>True when the character has a main class registered.</summary>
    public static bool HasMainClass => CharacterManager.Current.Status.HasMainClass;

    /// <summary>
    /// True when the character is currently using average HP (the option element is registered).
    /// </summary>
    public static bool IsUsingAverageHp
    {
        get { try { return CharacterManager.Current.ContainsAverageHitPointsOption(); } catch (InvalidOperationException) { return false; } }
    }

    private static string FeatsOptionId         => Builder.Data.Strings.InternalOptions.AllowFeats;
    private static string MulticlassOptionId    => Builder.Data.Strings.InternalOptions.AllowMulticlassing;
    private const  string MulticlassPrereqGrantId = "ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE";
    private const  string CustomOriginOptionId      = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI";
    private const  string CustomLanguageOptionId    = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_LANGUAGE";
    private const  string CustomProficiencyOptionId = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_PROFICIENCY";

    public static bool IsUsingFeats
    {
        get { try { return CharacterManager.Current.ContainsOption(FeatsOptionId); } catch (InvalidOperationException) { return false; } }
    }

    public static bool IsUsingMulticlassing
    {
        get { try { return CharacterManager.Current.ContainsOption(MulticlassOptionId); } catch (InvalidOperationException) { return false; } }
    }

    public static bool IsUsingCustomOrigin
    {
        get { try { return CharacterManager.Current.ContainsOption(CustomOriginOptionId); } catch (InvalidOperationException) { return false; } }
    }

    public static bool IsUsingCustomLanguage
    {
        get { try { return CharacterManager.Current.ContainsOption(CustomLanguageOptionId); } catch (InvalidOperationException) { return false; } }
    }

    public static bool IsUsingCustomProficiency
    {
        get { try { return CharacterManager.Current.ContainsOption(CustomProficiencyOptionId); } catch (InvalidOperationException) { return false; } }
    }

    public static async Task<string?> SetFeatsOptionAsync(CharacterTab tab, bool enabled)
        => await SetOptionAsync(tab, FeatsOptionId, enabled, "BuildService.SetFeatsOptionAsync");

    public static async Task<string?> SetMulticlassingOptionAsync(CharacterTab tab, bool enabled)
        => await SetOptionAsync(tab, MulticlassOptionId, enabled, "BuildService.SetMulticlassingOptionAsync");

    public static async Task<string?> SetCustomOriginOptionAsync(CharacterTab tab, bool enabled)
        => await SetOptionAsync(tab, CustomOriginOptionId, enabled, "BuildService.SetCustomOriginOptionAsync");

    public static async Task<string?> SetCustomLanguageOptionAsync(CharacterTab tab, bool enabled)
        => await SetOptionAsync(tab, CustomLanguageOptionId, enabled, "BuildService.SetCustomLanguageOptionAsync");

    public static async Task<string?> SetCustomProficiencyOptionAsync(CharacterTab tab, bool enabled)
        => await SetOptionAsync(tab, CustomProficiencyOptionId, enabled, "BuildService.SetCustomProficiencyOptionAsync");

    private static async Task<string?> SetOptionAsync(CharacterTab tab, string optionId, bool enabled, string callerName)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                bool has = cm.ContainsOption(optionId);
                if (enabled && !has)
                {
                    var element = DataManager.Current.ElementsCollection
                        .FirstOrDefault(e => e.Id == optionId);
                    if (element != null) cm.RegisterElement(element);
                }
                else if (!enabled && has)
                {
                    var element = cm.GetElements()
                        .FirstOrDefault(e => e.Id == optionId);
                    if (element != null) cm.UnregisterElement(element);
                }
                cm.ReprocessCharacter();
                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, callerName); }
        });
    }

    /// <summary>
    /// Registers or unregisters the AllowAverageHitPoints option element, then reprocesses
    /// and saves. Returns null on success, or an error string.
    /// </summary>
    public static async Task<string?> SetHpMethodAsync(CharacterTab tab, HpMethod method)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                var optionId = Builder.Data.Strings.InternalOptions.AllowAverageHitPoints;

                bool wantsAverage = method == HpMethod.Average;
                bool hasAverage   = cm.ContainsAverageHitPointsOption();

                if (wantsAverage && !hasAverage)
                {
                    var element = DataManager.Current.ElementsCollection
                        .FirstOrDefault(e => e.Id == optionId);
                    if (element != null) cm.RegisterElement(element);
                }
                else if (!wantsAverage && hasAverage)
                {
                    var element = cm.GetElements()
                        .FirstOrDefault(e => e.Id == optionId);
                    if (element != null) cm.UnregisterElement(element);
                }

                cm.ReprocessCharacter();
                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.SetHpMethodAsync"); }
        });
    }


    /// <summary>
    /// Adds a level to the main class (or the bare level if class not yet chosen).
    /// Saves the file and re-snaps the tab.
    /// Returns (error, hpGained, isAverage) — error is null on success.
    /// </summary>
    public static async Task<(string? Error, int HpGained, bool IsAverage)> LevelUpMainAsync(CharacterTab tab)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                int hpBefore = cm.Character.MaxHp;

                cm.LevelUpMain();

                // Capture HP gained before reprocessing overwrites it
                int hpAfter  = cm.Character.MaxHp;
                // ReprocessCharacter is called by LevelUpMain indirectly; MaxHp should be fresh.
                // If it hasn't updated yet, force it.
                if (hpAfter == hpBefore)
                {
                    cm.ReprocessCharacter();
                    hpAfter = cm.Character.MaxHp;
                }

                int hpGained  = hpAfter - hpBefore;
                bool isAverage = cm.ContainsAverageHitPointsOption();

                ResnapTab(tab);
                SaveCharacterFile(tab);
                return ((string?)null, hpGained, isAverage);
            }
            catch (Exception ex) { DebugLogService.Instance.LogException(ex, "BuildService.LevelUpMainAsync"); return (ex.Message, 0, false); }
        });
    }

    /// <summary>
    /// Removes the last level. Saves the file and re-snaps the tab. Returns any error string.
    /// </summary>
    public static async Task<string?> LevelDownAsync(CharacterTab tab)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                CharacterManager.Current.LevelDown();
                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.LevelDownAsync"); }
        });
    }

}

// ── Build tab group ───────────────────────────────────────────────────────────

public sealed record BuildTabGroup(string Label, IReadOnlyList<SelectionRuleGroup> RuleGroups, int UnresolvedCount = 0);

// ── Value types ───────────────────────────────────────────────────────────────

public sealed record SelectionRuleGroup(string Label, IReadOnlyList<SelectionRuleEntry> Rules);

public sealed record SelectionRuleEntry(
    SelectRule Rule,
    int        Number,
    string     Label,
    string?    CurrentName,
    int        RequiredLevel,
    string     EntryKey = "",
    int        SpellLevel = 0,
    string?    SpellId = null)
{
    public bool IsOptional =>
        Rule?.Attributes?.Optional == true ||
        BuildRuleClassifier.IsOptionalFlavorSelection(Label);
}

public enum BuildGuidanceActionKind
{
    Selection,
    AbilityScores,
}

public sealed record BuildGuidanceTarget(
    BuildGuidanceActionKind ActionKind,
    string TabLabel,
    string StepLabel,
    string? EntryKey,
    string TargetLabel);

public sealed record ElementOption(
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
    bool IsCurrentSelection = false);

/// <summary>A class the character can level up: its element id (Class or Multiclass), display name,
/// current level in that class, and whether it's the main class.</summary>
public sealed record LevelUpClassOption(string Id, string Name, int Level, bool IsMain);

public sealed record SpellDetail(
    string Id,
    string Name,
    string Source,
    int    Level,
    string Subtitle,     // e.g. "1st-level abjuration (ritual)" or "Transmutation Cantrip"
    string School,
    bool   Ritual,
    bool   Concentration,
    string CastingTime,
    string Range,
    string Components,
    string Duration,
    string Description);

// ── Advancement timeline ──────────────────────────────────────────────────────

public sealed record AdvancementLevelEntry(
    int                       Level,
    int                       AverageHp,
    IReadOnlyList<FeatureEntry> Features);

public sealed record AdvancementClassTimeline(
    string                             ClassName,
    string                             HitDie,
    bool                               IsMainClass,
    IReadOnlyList<AdvancementLevelEntry> Levels);
