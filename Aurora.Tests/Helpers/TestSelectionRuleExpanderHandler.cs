using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;

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
        => SelectionRuleRegistrationService.SetRegisteredElement(_registered, selectionRule, id, number);

    public void ClearRegisteredElement(SelectRule selectionRule, int number = 1)
        => SelectionRuleRegistrationService.ClearRegisteredElement(_registered, selectionRule, number);

    public int GetExpandersCount() => 0;

    public void FocusExpander(SelectRule rule, int number = 1) { }

    public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel) { }

    public void RemoveAllExpanders() => _registered.Clear();

    public bool RequiresSelection(SelectRule rule, int number = 1) => false;

    public int GetRetrainLevel(SelectRule rule, int number) => 0;

    private static string Key(SelectRule rule, int number)
        => $"{rule.UniqueIdentifier}:{number}";
}
