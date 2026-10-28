using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Services;

/// <summary>Fills predefined companion slots after progression and saved choices have settled.</summary>
public static class CompanionSelectionDefaults
{
    internal static bool IsPredefinedChoice(SelectRule rule) =>
        rule.Attributes.Type == "Companion" && !rule.Attributes.IsList
        && !rule.Attributes.Optional && rule.Attributes.Number == 1
        && !string.IsNullOrWhiteSpace(rule.Attributes.Default);

    internal static bool IsDefaultCompanion(CompanionElement element) =>
        element.Aquisition.WasSelected && IsPredefinedChoice(element.Aquisition.SelectRule)
        && ElementIdAliases.Resolve(DataManager.Current.ElementsCollection,
            element.Aquisition.SelectRule.Attributes.Default)?.Id == element.Id;

    public static int Apply()
    {
        var handler = SelectionRuleExpanderContext.Current;
        if (handler is null) return 0;
        var manager = CharacterManager.Current;
        var attempted = new HashSet<SelectRule>();
        int added = 0;

        // Registering a creature can expose another choice. Bound the passes so malformed
        // content cannot create an endless chain of default companions.
        for (int pass = 0; pass < 32; pass++)
        {
            bool changed = false;
            foreach (var rule in manager.SelectionRules.ToArray())
            {
                if (!IsPredefinedChoice(rule) || !attempted.Add(rule)
                    || handler.GetRegisteredElement(rule) is not null) continue;
                var creature = ElementIdAliases.Resolve(DataManager.Current.ElementsCollection,
                    rule.Attributes.Default) as CompanionElement;
                if (creature is null || !SavedSelectionRecovery.IsEligible(rule, creature.Id)) continue;

                try
                {
                    handler.SetRegisteredElement(rule, creature.Id);
                    added++;
                    changed = true;
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Warning("could not select default companion {0}: {1}", creature.Id, ex.Message);
                }
            }
            if (!changed) break;
            manager.ReprocessCharacter();
        }
        return added;
    }
}
