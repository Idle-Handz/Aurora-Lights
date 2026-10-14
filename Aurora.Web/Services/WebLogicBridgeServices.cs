using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.UserControls.Spellcasting;

using Builder.Presentation.Services;

namespace Aurora.Web.Services;

internal sealed class WebSelectionRuleExpanderHandler : ISelectionRuleExpanderHandler
{
    private readonly Dictionary<string, object> _registered = new(StringComparer.Ordinal);

    public void RegisterSupport(ISupportExpanders support) { }

    public bool HasExpander(string uniqueIdentifier) => true;

    public bool HasExpander(string uniqueIdentifier, int number) => true;

    public void ClearRegisteredElement(SelectRule selectionRule, int number = 1) =>
        _registered.Remove($"{selectionRule.UniqueIdentifier}:{number}");

    public void SetRegisteredElement(SelectRule selectionRule, string id, int number = 1)
    {
        if (selectionRule == null || string.IsNullOrEmpty(id))
            return;

        ElementBase? element = DataManager.Current.ElementsCollection.GetElement(id);
        if (element == null)
        {
            Logger.Warning($"[WebExpander] element not found in collection: {id}");
            return;
        }

        string key = $"{selectionRule.UniqueIdentifier}:{number}";
        if (_registered.TryGetValue(key, out object? previous) && previous is ElementBase previousElement)
        {
            try { CharacterManager.Current.UnregisterElement(previousElement); }
            catch (Exception ex) { Logger.Exception(ex, $"WebSelectionRuleExpanderHandler.SetRegisteredElement unregister previous '{previousElement.Id}'"); }
        }

        element.Aquisition.WasSelected = true;
        element.Aquisition.SelectRule = selectionRule;
        CharacterManager.Current.RegisterElement(element);

        _registered[key] = element;
    }

    public void SetRegisteredListItem(SelectRule selectionRule, SelectionRuleListItem item, int number = 1)
    {
        if (selectionRule == null)
            return;

        _registered[$"{selectionRule.UniqueIdentifier}:{number}"] = item;
    }

    public object GetRegisteredElement(SelectRule selectionRule, int number = 1)
    {
        _registered.TryGetValue($"{selectionRule.UniqueIdentifier}:{number}", out object? element);
        return element!;
    }

    public int GetExpandersCount() => 0;

    public void FocusExpander(SelectRule rule, int number = 1) { }

    public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel) { }

    public void RemoveAllExpanders() => _registered.Clear();

    public bool RequiresSelection(SelectRule rule, int number = 1) => false;

    public int GetRetrainLevel(SelectRule rule, int number) => 0;
}

internal sealed class WebSpellcastingSectionHandler : ISpellcastingSectionHandler
{
    private readonly Dictionary<string, HashSet<string>> _preparedIds = new(StringComparer.OrdinalIgnoreCase);

    public void ResetPreparedState() => _preparedIds.Clear();

    public IReadOnlyCollection<string> GetPreparedIds(string spellcastingName)
    {
        var profiles = CharacterManager.Current.GetSpellcastingInformations()
            .Where(p => !p.IsExtension && p.Name == spellcastingName).ToArray();
        return profiles.Length == 1 ? GetPreparedIds(profiles[0]) : Array.Empty<string>();
    }

    public IReadOnlyCollection<string> GetPreparedIds(SpellcastingInformation profile) =>
        _preparedIds.TryGetValue(SpellAcquisitionResolver.ProfileKey(profile), out var ids)
            ? ids : Array.Empty<string>();

    public void UnsetPrepareSpell(SpellcastingInformation profile, string elementId)
    {
        if (_preparedIds.TryGetValue(SpellAcquisitionResolver.ProfileKey(profile), out var ids))
            ids.Remove(elementId);
    }

    public void UnsetPrepareSpell(string spellcastingName, string elementId)
    {
        var profiles = CharacterManager.Current.GetSpellcastingInformations()
            .Where(p => !p.IsExtension && p.Name == spellcastingName).ToArray();
        if (profiles.Length == 1) UnsetPrepareSpell(profiles[0], elementId);
    }

    public SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;

    public bool SetPrepareSpell(SpellcastingInformation information, string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            return false;

        string key = SpellAcquisitionResolver.ProfileKey(information);
        if (!_preparedIds.TryGetValue(key, out HashSet<string>? ids))
            _preparedIds[key] = ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ids.Add(elementId);
        return true;
    }
}

internal sealed class WebMessageDialogService : IMessageDialogService
{
    public void Show(string message, string? caption = null) =>
        System.Diagnostics.Debug.WriteLine($"[Aurora.Web][Dialog] {caption}: {message}");

    public void ShowException(Exception ex, string? message = null, string? caption = null) =>
        System.Diagnostics.Debug.WriteLine($"[Aurora.Web][Dialog:Exception] {caption}: {message}{Environment.NewLine}{ex}");

    public bool Confirm(string message, string? caption = null)
    {
        System.Diagnostics.Debug.WriteLine($"[Aurora.Web][Dialog:Confirm] {caption}: {message} -> false");
        return false;
    }
}

internal sealed class WebExternalLauncher : IExternalLauncher
{
    public bool OpenPath(string path) => false;

    public bool OpenUri(string uri) => false;
}
