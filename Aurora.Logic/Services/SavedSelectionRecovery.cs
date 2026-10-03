using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Services;

/// <summary>Conservative recovery when content changes a saved grant or selection label.</summary>
internal static class SavedSelectionRecovery
{
    internal static bool IsEligible(SelectRule rule, string id, int number = 1)
    {
        var manager = CharacterManager.Current;
        if (!manager.SelectionRules.Contains(rule) || rule.Attributes.IsList ||
            number < 1 || number > Math.Max(1, rule.Attributes.Number)) return false;
        var element = ElementIdAliases.Resolve(DataManager.Current.ElementsCollection, id);
        if (element is null || element.Type != rule.Attributes.Type ||
            !BuildSourceRestrictionSnapshot.CaptureCurrent().Allows(element)) return false;
        var interpreter = new ExpressionInterpreter();
        interpreter.InitializeWithSelectionRule(rule);
        var owned = manager.GetElements().Select(e => e.Id).ToArray();
        if (element.HasRequirements && !interpreter.EvaluateElementRequirementsExpression(element.Requirements, owned))
            return false;
        if (!string.IsNullOrWhiteSpace(rule.Attributes.Requirements) && !interpreter.EvaluateRuleRequirementsExpression(rule.Attributes.Requirements, owned))
            return false;
        if (!rule.Attributes.ContainsSupports()) return true;
        try
        {
            return interpreter.EvaluateSupportsExpression<ElementBase>(
                BuildSelectionOptionResolver.ExpandDynamicSpellcastingSupports(rule),
                new[] { element }, rule.Attributes.SupportsElementIdRange()).Any();
        }
        catch { return false; }
    }

    internal static SelectRule? FindRenamedRule(ElementBase owner, string type, string id, int number)
    {
        var candidates = owner.GetSelectRules().Where(r => r.Attributes.Type == type && IsEligible(r, id, number)).ToArray();
        // A matching type alone is not enough to choose between two distinct choices.
        return candidates.Length == 1 ? candidates[0] : null;
    }

    internal static int RestoreSavedDefaults(IReadOnlySet<string> savedGrantIds, IReadOnlySet<SelectRule>? explicitChoices = null)
    {
        int restored = 0;
        var handler = SelectionRuleExpanderContext.Current;
        if (handler is null) return 0;
        var attempted = new HashSet<string>();
        var manager = CharacterManager.Current;
        while (true)
        {
            bool changed = false;
            foreach (var rule in manager.SelectionRules.ToArray())
            {
                string id = rule.Attributes.Default;
                if (string.IsNullOrWhiteSpace(id) || !savedGrantIds.Contains(id) ||
                    explicitChoices?.Contains(rule) == true ||
                    !IsEligible(rule, id) || !attempted.Add(rule.UniqueIdentifier) ||
                    handler.GetRegisteredElement(rule) is not null) continue;
                try
                {
                    handler.SetRegisteredElement(rule, id);
                    restored++;
                    changed = true;
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Warning("could not restore saved default {0}: {1}", id, ex.Message);
                }
            }
            if (!changed) return restored;
            manager.ReprocessCharacter();
        }
    }
}
