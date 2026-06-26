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

public static partial class BuildService
{
    // ── Tab-based build structure ─────────────────────────────────────────────────

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
        // Race comes first in the guided flow even when an older or reopened tab keeps
        // the class-first visual tab ordering. Race rules establish movement, languages,
        // and origin ASI choices that the remaining build depends on.
        var coreOrder = new[] { "Race", "Class" };

        foreach (var label in coreOrder)
        {
            var tab = tabs.FirstOrDefault(t => t.Label == label);
            if (tab == null) continue;
            foreach (var group in tab.RuleGroups)
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.CurrentName == null && !rule.IsOptional)
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
                    if (rule.CurrentName == null && !rule.IsOptional)
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
                if (string.Equals(ruleType, "Multiclass", StringComparison.OrdinalIgnoreCase) &&
                    currentName != null)
                    continue;

                string label = rule.Attributes.Number > 1
                    ? $"{ruleName} ({n})"
                    : ruleName;

                var entry = new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(rule, n, ruleType, ruleName));

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
        OriginAbilityScoreSource activeOriginAsiSource = GetActiveOriginAbilityScoreSource();

        foreach (var rule in cm.SelectionRules)
        {
            var pm       = cm.GetProgressManager(rule);
            var classMgr = classMgrs.FirstOrDefault(m => ReferenceEquals(m, pm));
            if (classMgr != null) continue; // class-PM rules stay in Class tab

            string ruleType = rule.Attributes.Type ?? "Other";
            if (ClassifyBuildRule(rule, classMgr) != BuildRuleBucket.AbilityScores) continue;
            OriginAbilityScoreSource originAsiSource = GetOriginAbilityScoreSource(rule);

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                string? currentName = ResolveCurrentSelectionName(rule, n);
                if (activeOriginAsiSource != OriginAbilityScoreSource.None &&
                    originAsiSource != OriginAbilityScoreSource.None &&
                    originAsiSource != activeOriginAsiSource &&
                    currentName == null)
                {
                    continue;
                }

                string ruleName = rule.Attributes.Name ?? ruleType;
                string label    = rule.Attributes.Number > 1 ? $"{ruleName} ({n})" : ruleName;
                result.Add(new SelectionRuleEntry(
                    rule, n, label, currentName, rule.Attributes.RequiredLevel,
                    BuildEntryKey(rule, n, ruleType, ruleName)));
            }
        }

        return result.OrderBy(e => e.RequiredLevel).ThenBy(e => e.Label).ToList();
    }

    private static OriginAbilityScoreSource GetOriginAbilityScoreSource(SelectRule rule)
    {
        var cm = CharacterManager.Current;
        var pm = cm.GetProgressManager(rule);
        bool hasClassManager = cm.ClassProgressionManagers.Any(m => ReferenceEquals(m, pm));
        var ownerElement = ResolveOwnerElement(rule);

        return BuildRuleClassifier.ClassifyOriginAbilityScoreSource(
            ruleType: rule.Attributes.Type ?? "Other",
            ruleName: rule.Attributes.Name ?? rule.Attributes.Type ?? "Other",
            ownerType: ownerElement?.Type ?? rule.ElementHeader?.Type ?? string.Empty,
            ownerName: ownerElement?.Name ?? rule.ElementHeader?.Name ?? string.Empty,
            hasClassManager: hasClassManager);
    }

    private static OriginAbilityScoreSource GetActiveOriginAbilityScoreSource()
    {
        bool hasRace = HasRegisteredOriginAbilityScoreSelection(OriginAbilityScoreSource.Race);
        bool hasBackground = HasRegisteredOriginAbilityScoreSelection(OriginAbilityScoreSource.Background);

        // If a character somehow already has both, prefer the 2024-style background source
        // until the validator can clear the older racial selections.
        if (hasBackground)
            return OriginAbilityScoreSource.Background;
        return hasRace ? OriginAbilityScoreSource.Race : OriginAbilityScoreSource.None;
    }

    private static bool HasRegisteredOriginAbilityScoreSelection(OriginAbilityScoreSource source)
    {
        if (source == OriginAbilityScoreSource.None)
            return false;

        foreach (var rule in CharacterManager.Current.SelectionRules)
        {
            if (GetOriginAbilityScoreSource(rule) != source)
                continue;

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                if (SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n) is ElementBase)
                    return true;
            }
        }

        return false;
    }

    private static void ClearConflictingOriginAbilityScoreSelections(
        SelectRule selectedRule,
        List<string> invalidated)
    {
        OriginAbilityScoreSource selectedSource = GetOriginAbilityScoreSource(selectedRule);
        if (selectedSource == OriginAbilityScoreSource.None)
            return;

        OriginAbilityScoreSource conflictingSource = selectedSource == OriginAbilityScoreSource.Race
            ? OriginAbilityScoreSource.Background
            : OriginAbilityScoreSource.Race;

        ClearOriginAbilityScoreSelections(conflictingSource, selectedRule, invalidated);
    }

    private static void ClearExistingOriginAbilityScoreCollisions(List<string> invalidated)
    {
        if (!HasRegisteredOriginAbilityScoreSelection(OriginAbilityScoreSource.Race) ||
            !HasRegisteredOriginAbilityScoreSelection(OriginAbilityScoreSource.Background))
        {
            return;
        }

        // Existing mixed-rule characters are normalized toward 2024 background ASIs.
        ClearOriginAbilityScoreSelections(OriginAbilityScoreSource.Race, exceptRule: null, invalidated);
    }

    private static void ClearOriginAbilityScoreSelections(
        OriginAbilityScoreSource source,
        SelectRule? exceptRule,
        List<string> invalidated)
    {
        if (source == OriginAbilityScoreSource.None)
            return;

        var cm = CharacterManager.Current;
        foreach (var rule in cm.SelectionRules.ToList())
        {
            if (exceptRule != null && ReferenceEquals(rule, exceptRule))
                continue;
            if (GetOriginAbilityScoreSource(rule) != source)
                continue;

            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                var registered = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n) as ElementBase;
                if (registered == null)
                    continue;

                bool cleared = false;
                try
                {
                    cm.UnregisterElement(registered);
                    SelectionRuleExpanderContext.Current?.ClearRegisteredElement(rule, n);
                    cleared = true;
                }
                catch (Exception ex)
                {
                    DebugLogService.Instance.LogException(
                        ex,
                        "BuildService.ClearOriginAbilityScoreSelections");
                }

                if (cleared)
                    invalidated.Add(BuildSelectionLabel(rule, n));
            }
        }
    }

    private static void ClearStaleSelectedAbilityScoreElements(List<string> invalidated)
    {
        var cm = CharacterManager.Current;
        var activeRuleIds = cm.SelectionRules
            .Select(rule => rule.UniqueIdentifier)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var element in cm.GetElements().ToList())
        {
            if (!element.Type.Equals("Ability Score Improvement", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!element.Aquisition.WasSelected)
                continue;

            var selectedRule = element.Aquisition.SelectRule;
            if (selectedRule == null)
                continue;

            var selectedRuleId = selectedRule.UniqueIdentifier;
            if (!string.IsNullOrWhiteSpace(selectedRuleId) && activeRuleIds.Contains(selectedRuleId))
                continue;

            int slot = FindRegisteredSelectionSlot(selectedRule, element);
            bool cleared = false;
            try
            {
                cm.UnregisterElement(element);
                if (slot > 0)
                    SelectionRuleExpanderContext.Current?.ClearRegisteredElement(selectedRule, slot);
                cleared = true;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(
                    ex,
                    "BuildService.ClearStaleSelectedAbilityScoreElements");
            }

            if (cleared)
            {
                invalidated.Add(slot > 0
                    ? BuildSelectionLabel(selectedRule, slot)
                    : (selectedRule.Attributes.Name ?? selectedRule.Attributes.Type ?? "Ability Score Improvement"));
            }
        }
    }

    private static int FindRegisteredSelectionSlot(SelectRule rule, ElementBase element)
    {
        int count = Math.Max(1, rule.Attributes.Number);
        for (int n = 1; n <= count; n++)
        {
            try
            {
                var current = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n) as ElementBase;
                if (current == null)
                    continue;

                if (ReferenceEquals(current, element) ||
                    current.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return n;
                }
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(
                    ex,
                    "BuildService.FindRegisteredSelectionSlot");
            }
        }

        return 0;
    }

    private static string BuildSelectionLabel(SelectRule rule, int number)
    {
        string ruleName = rule.Attributes.Name ?? rule.Attributes.Type ?? "Selection";
        return rule.Attributes.Number > 1 ? $"{ruleName} ({number})" : ruleName;
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
        groups.SelectMany(g => g.Rules).Count(r => r.CurrentName == null && !r.IsOptional);

    private static string BuildEntryKey(SelectRule rule, int number, string ruleType, string ruleName)
    {
        if (!string.IsNullOrWhiteSpace(rule.UniqueIdentifier))
            return $"rule:{rule.UniqueIdentifier}|slot:{number}";

        try
        {
            return $"crc:{rule.GetCrC(number)}";
        }
        catch
        {
            return $"legacy:{ruleType}|{ruleName}|slot:{number}";
        }
    }

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
            return (current as ElementBase)?.Name;
        }
        catch { return null; }
    }

}
