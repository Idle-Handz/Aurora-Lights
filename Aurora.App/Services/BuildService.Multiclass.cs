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
    /// <summary>
    /// Returns all Multiclass elements from the data collection — the list of classes
    /// the character could multiclass into. Uses dynamic dispatch since Multiclass is
    /// a Builder.Data type not directly nameable from Aurora.App.
    /// </summary>
    public static IReadOnlyList<ElementOption> GetMulticlassOptions()
    {
        var options = DataManager.Current.ElementsCollection
            .Where(e => e.Type == "Multiclass")
            .Select(e => new ElementOption(
                e.Id,
                e.Name ?? "",
                GetFeatureDescription(e),
                e.Source ?? "",
                // The multiclass ability-score prerequisite (e.g. "Strength 13 or Dexterity 13"),
                // surfaced in the picker's detail pane as "Requires: …".
                e.Prerequisite ?? "",
                SourceReleaseDate: TryGetElementSortMetadata(e)?.SourceReleaseDate,
                SourceFileModifiedUtc: TryGetElementSortMetadata(e)?.SourceFileModifiedUtc))
            .ToList();

        return OrderElementOptions(options, isSpellRule: false).ToList();
    }

    /// <summary>
    /// Returns the character's current classes (main class + any multiclasses) for the "which class
    /// do you want to level?" picker. Id is the class's element id — the main class's Class element
    /// id, or a multiclass's Multiclass element id — which routes the level-up (see LevelUpAsync).
    /// </summary>
    public static async Task<IReadOnlyList<LevelUpClassOption>> GetLevelUpClassesAsync(CharacterTab tab)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                return (IReadOnlyList<LevelUpClassOption>)cm.ClassProgressionManagers
                    .Where(m => m.ClassElement != null)
                    .Select(m => new LevelUpClassOption(
                        m.ClassElement!.Id ?? "",
                        m.ClassElement.Name ?? "Class",
                        m.ProgressionLevel,
                        m.IsMainClass))
                    // Main class first, then multiclasses alphabetically.
                    .OrderByDescending(o => o.IsMain)
                    .ThenBy(o => o.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                DebugLogService.Catch(ex, "BuildService.GetLevelUpClassesAsync");
                return [];
            }
        });
    }

    /// <summary>
    /// Returns the multiclass choices this character can currently take.
    /// Includes existing multiclass progressions so the same dialog can add another level to them.
    /// </summary>
    public static async Task<IReadOnlyList<ElementOption>> GetAvailableMulticlassOptionsAsync(CharacterTab tab)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                if (!cm.Status.HasMainClass || !cm.Status.CanLevelUp || !IsMulticlassingEnabled(cm))
                    return [];

                if (!CanCurrentBuildMulticlass(cm))
                    return [];

                // Classes the character already has (main class + any multiclasses), by display name.
                // Used to keep one source of a class at a time: a class already on the character can't
                // be added again from a different source. Existing multiclasses are matched by id below
                // so they stay available to level up.
                var currentClassNames = cm.ClassProgressionManagers
                    .Select(m => m.ClassElement?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var elements = DataManager.Current.ElementsCollection
                    .Where(e => e.Type == "Multiclass")
                    .Where(e =>
                        // Keep an existing multiclass (so it can be leveled), otherwise only offer a
                        // class the character doesn't already have (any source) and meets the prereq for.
                        FindMulticlassProgression(cm, e.Id) != null ||
                        (!currentClassNames.Contains(e.Name ?? "") &&
                         MeetsMulticlassElementRequirements(e, cm, out _)))
                    .OrderBy(e => e.Name)
                    .ToList();

                return BuildMulticlassOptions(elements);
            }
            catch (Exception ex)
            {
                DebugLogService.Catch(ex, "BuildService.GetAvailableMulticlassOptionsAsync");
                return [];
            }
        });
    }

    private static IReadOnlyList<ElementOption> BuildMulticlassOptions(List<ElementBase> elements)
    {
        var options = elements
            .Select(e => new ElementOption(
                e.Id,
                e.Name ?? "",
                GetFeatureDescription(e),
                e.Source ?? "",
                e.Prerequisite ?? "",
                SourceReleaseDate: TryGetElementSortMetadata(e)?.SourceReleaseDate,
                SourceFileModifiedUtc: TryGetElementSortMetadata(e)?.SourceFileModifiedUtc))
            .ToList();

        return DeduplicateOptions(OrderElementOptions(options, isSpellRule: false).ToList());
    }

    public static async Task<string?> AddMulticlassLevelAsync(CharacterTab tab, string multiclassElementId)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            var levelBefore = 0;
            var startedNewMulticlass = false;
            var mutationCompleted = false;

            try
            {
                var cm = CharacterManager.Current;
                levelBefore = cm.Character.Level;

                var element = DataManager.Current.ElementsCollection
                    .FirstOrDefault(e => e.Id == multiclassElementId && e.Type == "Multiclass");
                if (element == null)
                    return "Multiclass element not found.";

                if (!cm.Status.HasMainClass)
                    return "Choose a class before multiclassing.";

                if (!cm.Status.CanLevelUp)
                    return "This character cannot gain another level.";

                if (!IsMulticlassingEnabled(cm))
                    return "Multiclassing is not enabled for this character.";

                if (!CanCurrentBuildMulticlass(cm))
                    return "This character doesn't meet the ability-score prerequisite to multiclass "
                         + "(you need the minimum ability scores for both your current class and the new one).";

                var existingProgression = FindMulticlassProgression(cm, multiclassElementId);
                if (existingProgression != null)
                {
                    var progressionBefore = existingProgression.ProgressionLevel;

                    dynamic d = element;
                    cm.LevelUpMulti(d);

                    if (cm.Character.Level != levelBefore + 1 ||
                        existingProgression.ProgressionLevel != progressionBefore + 1)
                    {
                        RollBackToLevel(cm, levelBefore);
                        return $"Couldn't add a level of {element.Name}. No change was made.";
                    }
                }
                else
                {
                    if (!MeetsMulticlassElementRequirements(element, cm, out var requirementError))
                        return requirementError
                            ?? $"This character doesn't meet the multiclass prerequisite for {element.Name}.";

                    var existingRuleIds = GetMulticlassRules(cm)
                        .Select(r => r.UniqueIdentifier ?? "")
                        .ToHashSet(StringComparer.Ordinal);

                    cm.NewMulticlass();
                    startedNewMulticlass = true;

                    if (cm.Character.Level != levelBefore + 1)
                        return RollBackNewMulticlass(cm, levelBefore, "Couldn't gain a new multiclass level. No change was made.");

                    var mcRule = FindNewMulticlassRule(cm, existingRuleIds, cm.Character.Level);
                    if (mcRule == null)
                        return RollBackNewMulticlass(cm, levelBefore, "Couldn't open a multiclass selection for this level. No change was made.");

                    var expander = SelectionRuleExpanderContext.Current;
                    if (expander == null)
                        return RollBackNewMulticlass(cm, levelBefore, "Couldn't register the multiclass selection. No change was made.");

                    expander.SetRegisteredElement(mcRule, element.Id);

                    if (FindMulticlassProgression(cm, multiclassElementId) == null)
                        return RollBackNewMulticlass(cm, levelBefore, $"Couldn't add {element.Name} as a multiclass. No change was made.");
                }

                mutationCompleted = true;
                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex)
            {
                if (startedNewMulticlass && !mutationCompleted)
                    RollBackToLevel(CharacterManager.Current, levelBefore);

                return DebugLogService.Catch(ex, "BuildService.AddMulticlassLevelAsync");
            }
        });
    }

    private static bool IsMulticlassingEnabled(CharacterManager cm)
    {
        try { return cm.ContainsOption(MulticlassOptionId); }
        catch { return false; }
    }

    private static bool CanCurrentBuildMulticlass(CharacterManager cm) =>
        cm.Status.CanMulticlass || cm.GetElements().Any(e => e.Id == MulticlassPrereqGrantId);

    private static bool MeetsMulticlassElementRequirements(ElementBase element, CharacterManager cm, out string? error)
    {
        error = null;
        if (!element.HasRequirements)
            return true;

        try
        {
            var interpreter = new ExpressionInterpreter();
            var currentIds = cm.GetElements().Select(e => e.Id).ToList();
            return interpreter.EvaluateElementRequirementsExpression(element.Requirements, currentIds);
        }
        catch (Exception ex)
        {
            error = DebugLogService.Catch(ex, "BuildService.MeetsMulticlassElementRequirements");
            return false;
        }
    }

    private static ClassProgressionManager? FindMulticlassProgression(CharacterManager cm, string multiclassElementId) =>
        cm.ClassProgressionManagers
            .FirstOrDefault(m => m.IsMulticlass && m.ClassElement?.Id == multiclassElementId);

    private static IReadOnlyList<SelectRule> GetMulticlassRules(CharacterManager cm) =>
        cm.SelectionRules
            .Where(r => string.Equals(r.Attributes.Type, "Multiclass", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static SelectRule? FindNewMulticlassRule(
        CharacterManager cm,
        HashSet<string> existingRuleIds,
        int newCharacterLevel)
    {
        var rules = GetMulticlassRules(cm);
        var newRules = rules
            .Where(r => !existingRuleIds.Contains(r.UniqueIdentifier ?? ""))
            .ToList();

        return newRules.FirstOrDefault(r => r.Attributes.RequiredLevel == newCharacterLevel)
            ?? newRules.OrderByDescending(r => r.Attributes.RequiredLevel).FirstOrDefault()
            ?? rules.FirstOrDefault(r =>
                r.Attributes.RequiredLevel == newCharacterLevel &&
                (r.Attributes.Requirements ?? "").Contains(
                    $"ID_INTERNAL_MULTICLASS_LEVEL_{newCharacterLevel}",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string RollBackNewMulticlass(CharacterManager cm, int levelBefore, string message)
    {
        RollBackToLevel(cm, levelBefore);
        return message;
    }

    private static void RollBackToLevel(CharacterManager cm, int levelBefore)
    {
        try
        {
            while (cm.Character.Level > levelBefore && cm.Status.CanLevelDown)
                cm.LevelDown();
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "BuildService.RollBackToLevel");
        }
    }

    /// <summary>
    /// Starts a new multiclass or adds a level to an existing one.
    /// <paramref name="multiclassElementId"/> is the element ID of the Multiclass element.
    /// Saves and re-snaps. Returns any error string.
    /// </summary>
    private static async Task<string?> AddMulticlassLevelLegacyAsync(CharacterTab tab, string multiclassElementId)
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
                    // Starting a NEW multiclass is, in 5e and in the engine, spending a new level on a
                    // different class. The order matters:
                    //   1. NewMulticlass() levels the character up and adds ID_INTERNAL_MULTICLASS_LEVEL_N,
                    //      which is what *activates* the matching <select type="Multiclass"> rule.
                    //   2. The chosen class is then acquired THROUGH that rule — RegisterElement's
                    //      Multiclass case reads element.Aquisition.SelectRule, so registering a raw
                    //      element (no SelectRule) throws a NullReferenceException.
                    // The ability-score prerequisite (ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE) is
                    // granted by the current class only when met; gate on it first so we never level the
                    // character up into a dead end. (Multiclassing itself is a default-on, locked option.)
                    bool meetsPrereq = cm.GetElements()
                        .Any(e => e.Id == "ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE");
                    if (!meetsPrereq)
                        return "This character doesn't meet the ability-score prerequisite to multiclass "
                             + "(you need the minimum ability scores for both your current class and the new one).";

                    cm.NewMulticlass();

                    var mcRule = cm.SelectionRules
                        .Where(r => r.Attributes.Type == "Multiclass")
                        .OrderByDescending(r => r.Attributes.RequiredLevel)
                        .FirstOrDefault();
                    if (mcRule == null)
                    {
                        // Shouldn't happen once the prerequisite is met, but don't strand a leveled-up
                        // character with no class assigned — undo the level NewMulticlass just added.
                        if (cm.Status.CanLevelDown) cm.LevelDown();
                        return "Couldn't open a multiclass selection for this level. No change was made.";
                    }

                    element.Aquisition.SelectedBy(mcRule);
                    cm.RegisterElement(element);
                }

                ResnapTab(tab);
                SaveCharacterFile(tab);
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.AddMulticlassLevelAsync"); }
        });
    }
}
