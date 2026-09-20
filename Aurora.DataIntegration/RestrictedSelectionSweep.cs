using Builder.Core.Logging;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services;

namespace Aurora.App.Services;

/// <summary>
/// Takes back what a restricted source gave a character: the picks it supplied. Its grants are
/// suppressed separately by <see cref="RestrictedSourceGrantPolicy"/>.
/// </summary>
public static class RestrictedSelectionSweep
{
    /// <summary>
    /// Clears picks whose source the character now restricts, so Build shows them as choices to make
    /// again instead of silently keeping content the character may no longer use. Returns the labels of
    /// the cleared choices. Call after applying the restrictions, with the engine held for this
    /// character; grants from restricted sources are handled by <see cref="RestrictedSourceGrantPolicy"/>.
    /// </summary>
    public static IReadOnlyList<string> ClearRestrictedSelections()
    {
        var cleared = new List<string>();
        var restrictions = BuildSourceRestrictionSnapshot.CaptureCurrent();
        if (restrictions.ElementIds.Count == 0 && restrictions.SourceNames.Count == 0)
            return cleared;

        var cm = CharacterManager.Current;
        foreach (var rule in cm.SelectionRules.ToList())
        {
            for (int n = 1; n <= rule.Attributes.Number; n++)
            {
                if (SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n) is not ElementBase registered)
                    continue;
                if (restrictions.Allows(registered))
                    continue;

                DebugLogService.Instance.Log(Aurora.App.Services.LogLevel.Warning,
                    $"[Restrict] clearing '{registered.Id}' from '{registered.Source}': " +
                    $"rule='{rule.Attributes.Name ?? rule.Attributes.Type}'");
                try
                {
                    cm.UnregisterElement(registered);
                    SelectionRuleExpanderContext.Current?.ClearRegisteredElement(rule, n);
                }
                catch (Exception ex)
                {
                    DebugLogService.Instance.LogException(ex, $"BuildService.ClearRestrictedSelections '{registered.Id}'");
                    continue;
                }
                cleared.Add(rule.Attributes.Number > 1
                    ? $"{rule.Attributes.Name ?? rule.Attributes.Type} ({n})"
                    : rule.Attributes.Name ?? rule.Attributes.Type);
            }
        }

        // Nested choices and grants under a cleared pick are settled by reprocessing, the same way
        // selection validation settles them after a choice changes.
        if (cleared.Count > 0)
            cm.ReprocessCharacter();
        return cleared;
    }
}
