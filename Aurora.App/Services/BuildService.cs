using Aurora.Components.Models;
using Builder.Data;
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
public static class BuildService
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
                    BuildEntryKey(ruleType, ruleName, n));

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
                try
                {
                    var regEl = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n)
                        as Builder.Data.ElementBase;
                    if (regEl != null) spellLevel = (int)((dynamic)regEl).Level;
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
                    BuildEntryKey(ruleType, ruleName, n), spellLevel));
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
    public static IReadOnlyList<ElementOption> GetOptions(SelectRule rule)
    {
        try
        {
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
                    return listItems
                        .Select(li => new ElementOption(li.ID.ToString(), li.Text, li.Text, "", ""))
                        .ToList();
                // ListItems not populated — read directly from the owner element's XML node.
                return GetListOptionsFromElementNode(rule);
            }

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
            var list = elements
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .OrderBy(e => isSpellRule ? GetElementSpellLevel(e) : 0)
                .ThenBy(e => e.Name)
                .Select(e => new ElementOption(
                    e.Id, e.Name!,
                    isSpellRule ? GetSpellPickerDescription(e) : GetFeatureDescription(e),
                    e.Source ?? "",
                    e.HasRequirements ? FormatRequirements(e.Requirements) : ""))
                .ToList();

            if (list.Count == 0 && isSpellRule)
                list = SpellFallbackOptions(rule, baseCollection)
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .OrderBy(e => GetElementSpellLevel(e))
                    .ThenBy(e => e.Name)
                    .Select(e => new ElementOption(
                        e.Id, e.Name!, GetSpellPickerDescription(e),
                        e.Source ?? "",
                        e.HasRequirements ? FormatRequirements(e.Requirements) : ""))
                    .ToList();

            // Case-insensitive fallback: only used when the main expression returned nothing AND
            // this is not a Spell rule (Spell rules use SpellFallbackOptions above). Catches
            // content that uses internal ID aliases like ID_INTERNAL_SUPPORT_LANGUAGE_EXOTIC
            // whose plain-word token ("Exotic") fails the evaluator's case-sensitive Contains.
            // Running it as a union (even when the main evaluator found results) risks adding
            // wrong elements that pass substring-matching but fail proper rule validation on reload.
            if (list.Count == 0 && rule.Attributes.ContainsSupports()
                && !string.Equals(rule.Attributes.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            {
                list = FilterBySupportsCaseInsensitive(rule.Attributes.Supports, baseCollection)
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .OrderBy(e => e.Name)
                    .Select(e => new ElementOption(
                        e.Id, e.Name!, GetFeatureDescription(e),
                        e.Source ?? "",
                        e.HasRequirements ? FormatRequirements(e.Requirements) : ""))
                    .ToList();
            }

            return DeduplicateOptions(list);
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
                result.Add(group.First() with { Source = combined });
            }
        }
        return result;
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
        string? className = null;
        if (rule.Attributes.ContainsSpellcastingName())
            className = rule.Attributes.SpellcastingName;

        if (className == null && rule.Attributes.ContainsSupports())
        {
            // Extract first plain word — skips macro tokens like "$(spellcasting:list)".
            var firstWord = System.Text.RegularExpressions.Regex
                .Match(rule.Attributes.Supports, @"(?<!\$\()[A-Za-z][A-Za-z0-9 ]+")
                .Value.Trim();
            if (!string.IsNullOrEmpty(firstWord))
                className = firstWord;
        }

        if (className == null) return [];

        string scName = className;

        // When the rule uses $(spellcasting:slots), restrict to spells the character
        // can actually cast at their current level (prevents a Sorcerer 1 from seeing
        // 9th-level spells in the picker).
        int maxSpellLevel = 9;
        if (!isCantrip && rule.Attributes.ContainsSupports() &&
            rule.Attributes.Supports.Contains("$(spellcasting:slots)", StringComparison.OrdinalIgnoreCase))
        {
            maxSpellLevel = ResolveMaxCastableSpellLevel(scName);
        }

        // Fast path: use the pre-resolved spell access map from the DB loader.
        // Only use this path when it actually finds matches; if the map has the key but the
        // element IDs don't match the loaded content (e.g. API-imported IDs vs XML-loaded IDs),
        // fall through to the text-based scan instead of returning empty.
        if (DbElementLoader.SpellAccessMap.TryGetValue(scName, out var spellIds))
        {
            var fromMap = spellBase.Where(e =>
            {
                if (!spellIds.Contains(e.Id)) return false;
                int lvl = 0;
                try { lvl = (int)((dynamic)e).Level; } catch { }
                return isCantrip ? lvl == 0 : (lvl > 0 && lvl <= maxSpellLevel);
            }).ToList();
            if (fromMap.Count > 0) return fromMap;
        }

        // Text-based scan: filter by class name in supports attribute.
        // Use Any+Contains (substring, case-insensitive) because supports values may be
        // comma-joined strings like "Ranger, Paladin" rather than individual entries.
        return spellBase.Where(e =>
        {
            if (e.Supports == null || !e.Supports.Any(s => s.Contains(scName, StringComparison.OrdinalIgnoreCase)))
                return false;
            int lvl = 0;
            try { lvl = (int)((dynamic)e).Level; } catch { }
            return isCantrip ? lvl == 0 : (lvl > 0 && lvl <= maxSpellLevel);
        });
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

        await Task.Run(() =>
        {
            try
            {
            var cm = CharacterManager.Current;

            // 1. Register the new selection (handler also unregisters the previous one).
            SelectionRuleExpanderContext.Current?.SetRegisteredElement(rule, elementId, number);

            // 2. Re-run progression processing so grant rules and requirement checks fire.
            cm.ReprocessCharacter();

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
                        // If this slot is optional and the entire supported set is empty
                        // (e.g. dataset has no "Optional Background Feature" elements),
                        // validation is meaningless — preserve the existing selection rather
                        // than silently removing a choice the user made intentionally.
                        if (r.Attributes.Optional && validIds.Count == 0)
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
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "BuildService.ApplySelectionAsync");
                // Include the innermost stack frame so we can pinpoint the NRE location.
                var firstFrame = ex.StackTrace?.Split('\n')
                    .FirstOrDefault(l => l.TrimStart().StartsWith("at "))?.Trim() ?? "";
                taskError = $"{ex.GetType().Name}: {ex.Message} | {firstFrame}";
            }
        });

        if (taskError != null)
            invalidated.Add($"[Error: {taskError}]");

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

    private static void SaveCharacterFile(
        CharacterTab tab,
        Builder.Presentation.Models.CharacterFile? explicitFile = null)
    {
        Builder.Presentation.Models.CharacterFile? targetFile = explicitFile ?? tab.File;
        if (targetFile is null)
            throw new InvalidOperationException("No file associated with this tab.");

        if (tab.Snapshot != null && tab.Character != null)
            FlushSnapshotToCharacter(tab.Snapshot, tab.Character);

        targetFile.Save();

        if (tab.Snapshot != null && !targetFile.SaveTextEdits(tab.Snapshot))
            throw new InvalidOperationException("Character save completed, but snapshot-backed edits could not be patched into the file.");
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
        try
        {
            dynamic d = e;
            int level = 0;
            string school = "", castingTime = "", range = "", components = "", duration = "";
            bool ritual = false, concentration = false, body_ok = false;
            string body = "";

            try { level = (int)d.Level; } catch { }
            try { school = (string)(d.MagicSchool ?? ""); } catch { }
            try { castingTime = (string)(d.CastingTime ?? ""); } catch { }
            try { range = (string)(d.Range ?? ""); } catch { }
            try { duration = (string)(d.Duration ?? ""); } catch { }
            try { components = (string)(d.GetComponentsString()); } catch { }
            try { ritual = (bool)d.IsRitual; } catch { }
            try { concentration = (bool)d.IsConcentration; } catch { }
            try
            {
                string raw = (string)(d.Description ?? "");
                if (!string.IsNullOrWhiteSpace(raw))
                { body = ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim(); body_ok = true; }
            }
            catch { }

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

    private static int GetElementSpellLevel(ElementBase e)
    {
        try { return (int)((dynamic)e).Level; } catch { return 0; }
    }

    // ── Advancement timeline ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a per-level breakdown of features for each class in the current character,
    /// grouped by class-level. Only features that unlock at each specific level are included
    /// (not cumulative); feature level is read via dynamic dispatch on e.Attributes.Level.
    /// </summary>
    public static IReadOnlyList<AdvancementClassTimeline> GetAdvancementTimeline()
    {
        var cm = CharacterManager.Current;
        var result = new List<AdvancementClassTimeline>();

        foreach (var m in cm.ClassProgressionManagers)
        {
            var byLevel = new Dictionary<int, List<FeatureEntry>>();
            for (int lvl = 1; lvl <= m.ProgressionLevel; lvl++)
                byLevel[lvl] = [];

            foreach (var e in m.GetElements())
            {
                if (e.Type != "Class Feature" && e.Type != "Archetype Feature") continue;
                if (string.IsNullOrWhiteSpace(e.Name)) continue;
                if (e.Name.StartsWith("Ability Score Increase") ||
                    e.Name.StartsWith("Ability Score Improvement") ||
                    e.Name.Equals("Feat", StringComparison.OrdinalIgnoreCase)) continue;

                int atLevel = 1;
                try { dynamic d = e; atLevel = (int)d.Attributes.Level; } catch { }
                if (atLevel < 1 || atLevel > m.ProgressionLevel) continue;
                if (!byLevel.TryGetValue(atLevel, out var list)) continue;
                if (list.Any(f => f.Name == e.Name)) continue;
                list.Add(new FeatureEntry(e.Name!, GetFeatureDescription(e)));
            }

            var hitDieVal = 0;
            try { hitDieVal = m.GetHitDieValue(); } catch { }

            var levels = Enumerable.Range(1, m.ProgressionLevel)
                .Select(lvl =>
                {
                    int avgHp = lvl == 1 && m.IsMainClass
                        ? hitDieVal
                        : (hitDieVal / 2) + 1;
                    return new AdvancementLevelEntry(lvl, avgHp, byLevel[lvl]);
                })
                .ToList();

            result.Add(new AdvancementClassTimeline(
                m.ClassElement?.Name ?? "Unknown",
                m.HD ?? "—",
                m.IsMainClass,
                levels));
        }

        return result;
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
            if (e == null) return null;

            dynamic d = e;

            int    level      = 0;
            string subtitle   = "";   // e.g. "1st-level abjuration (ritual)"
            string school     = "";
            string castingTime = "";
            string range      = "";
            string components = "";
            string duration   = "";
            bool ritual       = false;
            bool concentration = false;

            try { level       = (int)(d.Level); }        catch { }
            try { subtitle    = (string)(d.Underline ?? ""); } catch { }
            try { school      = (string)(d.MagicSchool ?? ""); } catch { }
            try { castingTime = (string)(d.CastingTime ?? ""); } catch { }
            try { range       = (string)(d.Range ?? ""); }       catch { }
            try { duration    = (string)(d.Duration ?? ""); }    catch { }
            try { components  = (string)(d.GetComponentsString()); } catch { }
            try { ritual      = (bool)(d.IsRitual); } catch { }
            try { concentration = (bool)(d.IsConcentration); } catch { }

            // Body description — use the plain-text generator on the raw XML description.
            string body = "";
            try
            {
                string raw = (string)(d.Description ?? "");
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

    /// <summary>
    /// Returns all Multiclass elements from the data collection — the list of classes
    /// the character could multiclass into. Uses dynamic dispatch since Multiclass is
    /// a Builder.Data type not directly nameable from Aurora.App.
    /// </summary>
    public static IReadOnlyList<ElementOption> GetMulticlassOptions()
    {
        return DataManager.Current.ElementsCollection
            .Where(e => e.Type == "Multiclass")
            .OrderBy(e => e.Name)
            .Select(e => new ElementOption(
                e.Id,
                e.Name ?? "",
                GetFeatureDescription(e),
                e.Source ?? ""))
            .ToList();
    }

    /// <summary>
    /// Starts a new multiclass or adds a level to an existing one.
    /// <paramref name="multiclassElementId"/> is the element ID of the Multiclass element.
    /// Saves and re-snaps. Returns any error string.
    /// </summary>
    public static async Task<string?> AddMulticlassLevelAsync(CharacterTab tab, string multiclassElementId)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                var element = DataManager.Current.ElementsCollection
                    .FirstOrDefault(e => e.Id == multiclassElementId && e.Type == "Multiclass");
                if (element == null) return "Multiclass element not found.";

                // Check if this multiclass already exists on the character
                bool alreadyHasThisMulticlass = cm.ClassProgressionManagers
                    .Any(m => m.IsMulticlass && m.ClassElement?.Id == multiclassElementId);

                if (alreadyHasThisMulticlass)
                {
                    // Add a level to the existing multiclass via dynamic dispatch
                    dynamic d = element;
                    cm.LevelUpMulti(d);
                }
                else
                {
                    // Start a new multiclass: first register the Multiclass element
                    cm.RegisterElement(element);
                    // NewMulticlass() adds the level and wires the progression manager
                    cm.NewMulticlass();
                }

                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.AddMulticlassLevelAsync"); }
        });
    }

    // ── Tab-based build structure ─────────────────────────────────────────────────

    /// <summary>
    /// Classifies all active SelectionRules into tab groups for the Build page.
    /// Always returns Race, Class, and Background tabs (may be empty of rules if none
    /// apply yet). Additional overflow tabs are added for any other rule types.
    /// </summary>
    /// <summary>
    /// Returns build tabs, ASI entries, and the next required step in a single scan of
    /// <see cref="CharacterManager.SelectionRules"/>. Prefer this over calling
    /// <see cref="GetBuildTabs"/>, <see cref="GetAsiEntries"/>, and
    /// <see cref="GetNextRequiredStep"/> separately.
    /// </summary>
    public static (IReadOnlyList<BuildTabGroup> Tabs,
                   IReadOnlyList<SelectionRuleEntry> AsiEntries,
                   BuildGuidanceTarget? NextStep)
        GetBuildData(bool preferClassFirst)
    {
        var tabs = GetBuildTabs(preferClassFirst);
        var asi  = GetAsiEntries();

        BuildGuidanceTarget? next = null;
        var coreOrder = preferClassFirst
            ? new[] { "Class", "Race" }
            : new[] { "Race", "Class" };

        foreach (var label in coreOrder)
        {
            var tab = tabs.FirstOrDefault(t => t.Label == label);
            if (tab == null) continue;
            foreach (var group in tab.RuleGroups)
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.CurrentName == null)
                    {
                        next = new BuildGuidanceTarget(
                            BuildGuidanceActionKind.Selection,
                            tab.Label,
                            rule.Label,
                            rule.EntryKey,
                            TargetLabel: $"{tab.Label} tab");
                        goto done;
                    }
                }
            }
        }

        if (NeedsInitialAbilityScores())
        {
            next = new BuildGuidanceTarget(
                BuildGuidanceActionKind.AbilityScores,
                "Ability Scores",
                "Assign your starting ability scores",
                EntryKey: null,
                TargetLabel: "Ability Scores tab");
            goto done;
        }

        foreach (var label in new[] { "Background", "Languages", "Proficiencies" })
        {
            var tab = tabs.FirstOrDefault(t => t.Label == label);
            if (tab == null) continue;
            foreach (var group in tab.RuleGroups)
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.CurrentName == null)
                    {
                        next = new BuildGuidanceTarget(
                            BuildGuidanceActionKind.Selection,
                            tab.Label,
                            rule.Label,
                            rule.EntryKey,
                            TargetLabel: $"{tab.Label} tab");
                        goto done;
                    }
                }
            }
        }

        foreach (var tab in tabs.Where(t =>
                     t.Label is not "Race" and not "Class" and not "Background" and not "Languages" and not "Proficiencies"))
        {
            foreach (var group in tab.RuleGroups)
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.CurrentName == null)
                    {
                        next = new BuildGuidanceTarget(
                            BuildGuidanceActionKind.Selection,
                            tab.Label,
                            rule.Label,
                            rule.EntryKey,
                            TargetLabel: $"{tab.Label} tab");
                        goto done;
                    }
                }
            }
        }

        foreach (var entry in asi)
        {
            if (entry.CurrentName == null)
            {
                next = new BuildGuidanceTarget(
                    BuildGuidanceActionKind.Selection,
                    "Ability Scores",
                    entry.Label,
                    entry.EntryKey,
                    TargetLabel: "Ability Scores tab");
                break;
            }
        }
        done:
        return (tabs, asi, next);
    }

    public static IReadOnlyList<BuildTabGroup> GetBuildTabs(bool preferClassFirst)
    {
        var cm       = CharacterManager.Current;
        var classMgrs = cm.ClassProgressionManagers;

        var raceEntries        = new List<SelectionRuleEntry>();
        var classMainEntries   = new List<SelectionRuleEntry>(); // "Class" type before PM exists
        var bgEntries          = new List<SelectionRuleEntry>();
        var languageEntries    = new List<SelectionRuleEntry>();
        var proficiencyEntries = new List<SelectionRuleEntry>();
        var companionEntries   = new List<SelectionRuleEntry>();
        var featEntries        = new Dictionary<string, List<SelectionRuleEntry>>(StringComparer.OrdinalIgnoreCase);
        var overflowEntries    = new Dictionary<string, List<SelectionRuleEntry>>(StringComparer.OrdinalIgnoreCase);
        var classGroupEntries  = new Dictionary<ClassProgressionManager, List<SelectionRuleEntry>>();

        foreach (var rule in cm.SelectionRules)
        {
            if (rule.Attributes.Type == "Spell") continue;

            var pm       = cm.GetProgressManager(rule);
            var classMgr = classMgrs.FirstOrDefault(m => ReferenceEquals(m, pm));

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                string? currentName = ResolveCurrentSelectionName(rule, n);
                string ruleType  = rule.Attributes.Type  ?? "Other";
                string ruleName  = rule.Attributes.Name  ?? ruleType;
                string label = rule.Attributes.Number > 1
                    ? $"{ruleName} ({n})"
                    : ruleName;

                var entry = new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(ruleType, ruleName, n));

                switch (ClassifyBuildRule(rule, classMgr))
                {
                    case BuildRuleBucket.Class:
                        if (classMgr != null)
                        {
                            if (!classGroupEntries.ContainsKey(classMgr))
                                classGroupEntries[classMgr] = [];
                            classGroupEntries[classMgr].Add(entry);
                        }
                        else
                        {
                            classMainEntries.Add(entry);
                        }
                        break;
                    case BuildRuleBucket.Race:
                        raceEntries.Add(entry);
                        break;
                    case BuildRuleBucket.Background:
                        bgEntries.Add(entry);
                        break;
                    case BuildRuleBucket.Language:
                        languageEntries.Add(entry);
                        break;
                    case BuildRuleBucket.Proficiency:
                        proficiencyEntries.Add(entry);
                        break;
                    case BuildRuleBucket.Feat:
                        string featGroup = GetFeatGroupLabel(rule);
                        if (!featEntries.ContainsKey(featGroup))
                            featEntries[featGroup] = [];
                        featEntries[featGroup].Add(entry);
                        break;
                    case BuildRuleBucket.Companion:
                        companionEntries.Add(entry);
                        break;
                    case BuildRuleBucket.AbilityScores:
                        break;
                    default:
                        string typeName = ruleType;
                        if (!overflowEntries.ContainsKey(typeName))
                            overflowEntries[typeName] = [];
                        overflowEntries[typeName].Add(entry);
                        break;
                }
                // AsiTypes are excluded from all tabs — exposed via GetAsiEntries()
            }
        }

        static List<SelectionRuleEntry> Sort(List<SelectionRuleEntry> l) =>
            l.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList();

        var tabs = new List<BuildTabGroup>();

        // Class tab — always present; initial Class rule first, then per-PM groups.
        var classGroups = new List<SelectionRuleGroup>();
        if (classMainEntries.Count > 0)
            classGroups.Add(new SelectionRuleGroup("", Sort(classMainEntries)));
        foreach (var m in classMgrs)
        {
            if (!classGroupEntries.TryGetValue(m, out var entries) || entries.Count == 0) continue;
            classGroups.Add(new SelectionRuleGroup(
                m.ClassElement?.Name ?? "Class",
                Sort(entries)));
        }
        var classTab = new BuildTabGroup("Class", classGroups, CountUnresolved(classGroups));

        // Race tab
        var raceGroups = raceEntries.Count > 0
            ? new List<SelectionRuleGroup> { new("", Sort(raceEntries)) }
            : new List<SelectionRuleGroup>();
        var raceTab = new BuildTabGroup("Race", raceGroups, CountUnresolved(raceGroups));

        if (preferClassFirst)
        {
            tabs.Add(classTab);
            tabs.Add(raceTab);
        }
        else
        {
            tabs.Add(raceTab);
            tabs.Add(classTab);
        }

        // Background tab — always present
        var bgGroups = bgEntries.Count > 0
            ? new List<SelectionRuleGroup> { new("", Sort(bgEntries)) }
            : new List<SelectionRuleGroup>();
        tabs.Add(new BuildTabGroup("Background", bgGroups, CountUnresolved(bgGroups)));

        // Language tab — always present
        var langGroups = languageEntries.Count > 0
            ? new List<SelectionRuleGroup> { new("", Sort(languageEntries)) }
            : new List<SelectionRuleGroup>();
        tabs.Add(new BuildTabGroup("Languages", langGroups, CountUnresolved(langGroups)));

        // Proficiency tab — always present
        var profGroups = proficiencyEntries.Count > 0
            ? new List<SelectionRuleGroup> { new("", Sort(proficiencyEntries)) }
            : new List<SelectionRuleGroup>();
        tabs.Add(new BuildTabGroup("Proficiencies", profGroups, CountUnresolved(profGroups)));

        // Feats tab — always present
        var featGroups = featEntries.Count > 0
            ? featEntries
                .OrderBy(kv => kv.Key)
                .Select(kv => new SelectionRuleGroup(kv.Key, Sort(kv.Value)))
                .ToList()
            : new List<SelectionRuleGroup>();
        tabs.Add(new BuildTabGroup("Feats", featGroups, CountUnresolved(featGroups)));

        // Companions tab — only shown when companion rules are present
        if (companionEntries.Count > 0)
        {
            var companionGroups = new List<SelectionRuleGroup> { new("", Sort(companionEntries)) };
            tabs.Add(new BuildTabGroup("Companions", companionGroups, CountUnresolved(companionGroups)));
        }

        // Overflow tabs — one per unrecognised type, alphabetical
        foreach (var (typeName, entries) in overflowEntries.OrderBy(kv => kv.Key))
            tabs.Add(new BuildTabGroup(typeName, [new SelectionRuleGroup("", Sort(entries))], entries.Count(e => e.CurrentName == null)));

        return tabs;
    }

    /// <summary>
    /// Returns SelectionRule entries for Ability Score Improvement and Feat rules that are
    /// not tied to a class progression manager. These are shown on the Ability Scores tab
    /// rather than as separate overflow tabs.
    /// </summary>
    public static IReadOnlyList<SelectionRuleEntry> GetAsiEntries()
    {
        var cm      = CharacterManager.Current;
        var classMgrs = cm.ClassProgressionManagers;
        var result  = new List<SelectionRuleEntry>();

        foreach (var rule in cm.SelectionRules)
        {
            var pm       = cm.GetProgressManager(rule);
            var classMgr = classMgrs.FirstOrDefault(m => ReferenceEquals(m, pm));
            if (classMgr != null) continue; // class-PM rules stay in Class tab

            string ruleType = rule.Attributes.Type ?? "Other";
            if (ClassifyBuildRule(rule, classMgr) != BuildRuleBucket.AbilityScores) continue;

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                string? currentName = ResolveCurrentSelectionName(rule, n);
                string ruleName = rule.Attributes.Name ?? ruleType;
                string label    = rule.Attributes.Number > 1 ? $"{ruleName} ({n})" : ruleName;
                result.Add(new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(ruleType, ruleName, n)));
            }
        }

        return result.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList();
    }

    /// <summary>
    /// Returns the tab label and rule label of the first unfilled required SelectionRule,
    /// or null when everything is complete for the current level.
    /// </summary>
    public static BuildGuidanceTarget? GetNextRequiredStep()
    {
        var (tabs, asi, next) = GetBuildData(preferClassFirst: false);
        if (next != null)
            return next;

        foreach (var tab in tabs)
        {
            foreach (var group in tab.RuleGroups)
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.CurrentName == null)
                        return new BuildGuidanceTarget(
                            BuildGuidanceActionKind.Selection,
                            tab.Label,
                            rule.Label,
                            rule.EntryKey,
                            TargetLabel: $"{tab.Label} tab");
                }
            }
        }
        // Check ASI entries last
        foreach (var entry in asi)
        {
            if (entry.CurrentName == null)
                return new BuildGuidanceTarget(
                    BuildGuidanceActionKind.Selection,
                    "Ability Scores",
                    entry.Label,
                    entry.EntryKey,
                    TargetLabel: "Ability Scores tab");
        }
        return null;
    }

    private static int CountUnresolved(IEnumerable<SelectionRuleGroup> groups) =>
        groups.SelectMany(group => group.Rules).Count(rule => rule.CurrentName == null);

    private static string BuildEntryKey(string ruleType, string ruleName, int number) =>
        $"{ruleType}|{ruleName}|{number}";

    private static BuildRuleBucket ClassifyBuildRule(SelectRule rule, ClassProgressionManager? classMgr)
    {
        var ownerElement = ResolveOwnerElement(rule);
        return BuildRuleClassifier.Classify(
            ruleType:       rule.Attributes.Type ?? "Other",
            ruleName:       rule.Attributes.Name ?? rule.Attributes.Type ?? "Other",
            ownerType:      ownerElement?.Type ?? rule.ElementHeader?.Type ?? string.Empty,
            ownerName:      ownerElement?.Name ?? rule.ElementHeader?.Name ?? string.Empty,
            hasClassManager: classMgr != null);
    }

    private static ElementBase? ResolveOwnerElement(SelectRule rule)
    {
        var ownerId = rule.ElementHeader?.Id;
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        return DataManager.Current.ElementsCollection
            .FirstOrDefault(element => element.Id.Equals(ownerId, StringComparison.Ordinal));
    }

    private static string GetFeatGroupLabel(SelectRule rule)
    {
        var ownerElement = ResolveOwnerElement(rule);
        string ownerType = ownerElement?.Type ?? rule.ElementHeader?.Type ?? string.Empty;
        return BuildRuleClassifier.GetFeatGroupLabel(ownerType);
    }

    private static HashSet<string> GetValidSelectionIds(
        SelectRule rule,
        string? registeredId,
        IReadOnlyCollection<string> currentIds)
    {
        var interpreter = new ExpressionInterpreter();
        interpreter.InitializeWithSelectionRule(rule);

        var baseCollection = new ElementBaseCollection(
            DataManager.Current.ElementsCollection.Where(element => element.Type.Equals(rule.Attributes.Type)));

        ElementBaseCollection supported;
        if (rule.Attributes.ContainsSupports())
        {
            supported = new ElementBaseCollection(
                interpreter.EvaluateSupportsExpression<ElementBase>(
                    rule.Attributes.Supports,
                    baseCollection,
                    rule.Attributes.SupportsElementIdRange()));
        }
        else
        {
            supported = new ElementBaseCollection(baseCollection);
        }

        var sourcesManager = CharacterManager.Current.SourcesManager;
        var restrictedSourceNames = sourcesManager.GetUndefinedRestrictedSourceNames().ToHashSet(StringComparer.Ordinal);
        var restrictedElementIds = sourcesManager.GetRestrictedElementIds().ToHashSet(StringComparer.Ordinal);

        foreach (var restricted in supported
                     .Where(element => restrictedElementIds.Contains(element.Id) || restrictedSourceNames.Contains(element.Source))
                     .ToList())
        {
            supported.RemoveElement(restricted.Id);
        }

        foreach (var existing in CharacterManager.Current.GetElements().Where(e => e.Type.Equals(rule.Attributes.Type)))
        {
            if (supported.Any(e => e.Id.Equals(existing.Id, StringComparison.Ordinal)) &&
                !existing.AllowDuplicate &&
                !existing.Id.Equals(registeredId, StringComparison.Ordinal))
            {
                supported.RemoveElement(existing.Id);
            }
        }

        var validIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in supported)
        {
            if (!candidate.HasRequirements ||
                interpreter.EvaluateElementRequirementsExpression(candidate.Requirements, currentIds))
            {
                validIds.Add(candidate.Id);
            }
        }

        return validIds;
    }

    private static bool NeedsInitialAbilityScores()
    {
        try
        {
            var abilities = CharacterManager.Current?.Character?.Abilities;
            if (abilities == null) return false;

            return abilities.Strength.BaseScore == 10 &&
                   abilities.Dexterity.BaseScore == 10 &&
                   abilities.Constitution.BaseScore == 10 &&
                   abilities.Intelligence.BaseScore == 10 &&
                   abilities.Wisdom.BaseScore == 10 &&
                   abilities.Charisma.BaseScore == 10;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the Name of the element currently registered for <paramref name="rule"/> slot
    /// <paramref name="n"/>, or null if the slot is empty or the lookup fails.
    /// </summary>
    private static string? ResolveCurrentSelectionName(SelectRule rule, int n)
    {
        try
        {
            var current = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n);
            if (current is null) return null;
            if (current is SelectionRuleListItem listItem) return listItem.Text;
            return (string?)((dynamic)current).Name;
        }
        catch { return null; }
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
    int        SpellLevel = 0);

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

public sealed record ElementOption(string Id, string Name, string Description, string Source = "", string Requirements = "");

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
