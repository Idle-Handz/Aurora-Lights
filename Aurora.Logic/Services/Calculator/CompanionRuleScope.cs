using Builder.Data;
using Builder.Data.Elements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Builder.Presentation.Services.Calculator;

/// <summary>Scopes legacy companion statistics to the creature and the feature that acquired it.</summary>
internal static class CompanionRuleScope
{
    public static Func<ElementBase, bool> For(CompanionElement companion, IEnumerable<ElementBase> elements)
    {
        var all = elements.Distinct().ToArray();

        // Index the three ways an element can name its parent before walking anything. Searching
        // the whole collection per element instead costs a pass over every element's children for
        // every element, and this runs once per companion on every recalculation.
        var ownerOfChild = new Dictionary<ElementBase, ElementBase>();
        var ownerOfSelectRule = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        var byId = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        foreach (var element in all)
        {
            // First writer wins in each index, which is the element the old scan would have found.
            foreach (var child in element.RuleElements)
                ownerOfChild.TryAdd(child, element);
            if (element.ContainsSelectRules)
                foreach (var rule in element.GetSelectRules())
                    ownerOfSelectRule.TryAdd(rule.UniqueIdentifier, element);
            if (!string.IsNullOrEmpty(element.Id))
                byId.TryAdd(element.Id, element);
        }

        var parents = new Dictionary<ElementBase, ElementBase>();
        foreach (var element in all)
        {
            var acquisition = element.Aquisition;
            ElementBase? parent = acquisition.WasSelected
                ? acquisition.SelectRule is { } rule ? ownerOfSelectRule.GetValueOrDefault(rule.UniqueIdentifier) : null
                : ownerOfChild.GetValueOrDefault(element);
            if (parent is null && acquisition.GetParentHeader()?.Id is { } parentId)
                parent = byId.GetValueOrDefault(parentId);
            if (parent != null && !ReferenceEquals(parent, element)) parents[element] = parent;
        }

        IEnumerable<ElementBase> Ancestors(ElementBase? element)
        {
            var seen = new HashSet<ElementBase>();
            while (element != null && seen.Add(element))
            {
                yield return element;
                element = parents.GetValueOrDefault(element);
            }
        }

        var scopes = new Dictionary<ElementBase, HashSet<CompanionElement>>();
        foreach (var creature in all.OfType<CompanionElement>())
            foreach (var ancestor in Ancestors(creature))
            {
                if (!scopes.TryGetValue(ancestor, out var members)) scopes[ancestor] = members = new();
                members.Add(creature);
            }

        return owner =>
        {
            foreach (var ancestor in Ancestors(owner))
                if (scopes.TryGetValue(ancestor, out var members)) return members.Contains(companion);
            // Unowned legacy companion bonuses retain their existing global meaning.
            return true;
        };
    }
}
