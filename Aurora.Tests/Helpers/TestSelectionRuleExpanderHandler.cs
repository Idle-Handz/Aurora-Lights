using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Helpers;

/// <summary>
/// Cross-platform test double for Aurora's WPF/MAUI selection expander bridge.
/// It lets CharacterManager.New and CharacterFile.Load run without UI controls.
/// </summary>
public sealed class TestSelectionRuleExpanderHandler : ISelectionRuleExpanderHandler
{
    private readonly Dictionary<string, object> _registered = new(StringComparer.Ordinal);

    public void RegisterSupport(ISupportExpanders support) { }

    public bool HasExpander(string uniqueIdentifier) => true;

    public bool HasExpander(string uniqueIdentifier, int number) => true;

    public object GetRegisteredElement(SelectRule selectionRule, int number = 1)
    {
        _registered.TryGetValue(Key(selectionRule, number), out var element);
        return element!;
    }

    public void SetRegisteredElement(SelectRule selectionRule, string id, int number = 1)
    {
        if (selectionRule.Attributes.IsList)
        {
            var listItem = selectionRule.Attributes.ListItems?
                .FirstOrDefault(item => item.ID.ToString().Equals(id, StringComparison.Ordinal));
            if (listItem is not null)
                _registered[Key(selectionRule, number)] = listItem;
            return;
        }

        ElementBase? element = DataManager.Current.ElementsCollection
            .FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (element is null)
        {
            _registered[Key(selectionRule, number)] = id;
            return;
        }

        var existingSelection = CharacterManager.Current.Elements
            .FirstOrDefault(e =>
                e.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                e.Aquisition.WasSelected &&
                ReferenceEquals(e.Aquisition.SelectRule, selectionRule));
        if (existingSelection is not null)
        {
            _registered[Key(selectionRule, number)] = existingSelection;
            return;
        }

        element.Aquisition.WasSelected = true;
        element.Aquisition.SelectRule = selectionRule;

        CharacterManager.Current.RegisterElement(element);
        _registered[Key(selectionRule, number)] = element;
    }

    public void ClearRegisteredElement(SelectRule selectionRule, int number = 1)
        => _registered.Remove(Key(selectionRule, number));

    public int GetExpandersCount() => 0;

    public void FocusExpander(SelectRule rule, int number = 1) { }

    public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel) { }

    public void RemoveAllExpanders() => _registered.Clear();

    public bool RequiresSelection(SelectRule rule, int number = 1) => false;

    public int GetRetrainLevel(SelectRule rule, int number) => 0;

    private static string Key(SelectRule rule, int number)
        => $"{rule.UniqueIdentifier}:{number}";
}
