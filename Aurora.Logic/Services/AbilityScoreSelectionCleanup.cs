using Builder.Data;
using Builder.Data.Rules;

namespace Builder.Presentation.Services;

/// <summary>Removes selected ASIs whose owning choice is no longer active under the current rules.</summary>
public static class AbilityScoreSelectionCleanup
{
    public sealed record RemovedSelection(string ElementId, SelectRule Rule, int Slot);

    public static IReadOnlyList<RemovedSelection> Normalize()
    {
        var manager = CharacterManager.Current;
        var removed = new List<RemovedSelection>();
        int limit = manager.GetElements().Count;
        // Removing an inactive parent can deactivate its nested choices. Re-evaluate
        // after each removal rather than using one stale snapshot of active rules.
        while (true)
        {
            var element = manager.GetElements().FirstOrDefault(e =>
                e.Type.Equals("Ability Score Improvement", StringComparison.OrdinalIgnoreCase) &&
                e.Aquisition.WasSelected && e.Aquisition.SelectRule != null &&
                manager.GetProgressManager(e.Aquisition.SelectRule) == null);
            if (element == null) return removed;
            if (removed.Count >= limit)
                throw new InvalidOperationException("Ability-score cleanup did not converge.");

            var rule = element.Aquisition.SelectRule;
            int slot = 0;
            for (int n = 1; n <= Math.Max(1, rule.Attributes.Number); n++)
                if (ReferenceEquals(SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, n), element))
                {
                    slot = n;
                    break;
                }
            manager.UnregisterElement(element);
            if (slot > 0) SelectionRuleExpanderContext.Current?.ClearRegisteredElement(rule, slot);
            if (manager.GetElements().Any(e => ReferenceEquals(e, element)))
                throw new InvalidOperationException($"Unable to remove inactive ability-score selection {element.Id}.");
            removed.Add(new(element.Id, rule, slot));
        }
    }

    /// <summary>Only discount saved occurrences actually removed by normalization; missing content stays missing.</summary>
    public static int CountRemovedSavedElements(IEnumerable<string> saved, IEnumerable<string> before, IEnumerable<string> after)
    {
        var savedCounts = saved.GroupBy(id => id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var afterCounts = after.GroupBy(id => id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return before.GroupBy(id => id, StringComparer.Ordinal).Sum(g =>
            Math.Min(savedCounts.GetValueOrDefault(g.Key), Math.Max(0, g.Count() - afterCounts.GetValueOrDefault(g.Key))));
    }
}
