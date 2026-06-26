using Aurora.Documents.Sheets;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Documents;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Models;
using Builder.Presentation.Models.CharacterSheet;
using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Models.Sheet;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels.Shell.Items;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Aurora.App.Services;

/// <summary>
/// MAUI implementation of ISelectionRuleExpanderHandler.
/// In WPF, a WPF user-control expander is created for each SelectionRule; that control
/// registers itself with this handler and triggers CharacterManager.RegisterElement when a
/// selection is made.  In MAUI we have no WPF controls, so we do both jobs here:
///   • HasExpander — reports true whenever CharacterManager already tracks the rule,
///     meaning the owning element was processed and the rule is ready to receive a value.
///   • SetRegisteredElement — looks up the element by ID and registers it directly.
/// This lets CharacterFile.Load() run its full element-registration loop without WPF.
/// </summary>
internal sealed class MauiSelectionRuleExpanderHandler : ISelectionRuleExpanderHandler
{
    // Keyed by "uniqueIdentifier:number" so GetRegisteredElement can answer later queries.
    private readonly Dictionary<string, object> _registered = new(StringComparer.Ordinal);

    public void RegisterSupport(ISupportExpanders support) { }

    /// <summary>
    /// Always returns true — in MAUI there are no WPF expander controls to wait for,
    /// so AwaitExpanderCreationAsync returns immediately without polling delays.
    /// </summary>
    public bool HasExpander(string uniqueIdentifier) => true;

    public bool HasExpander(string uniqueIdentifier, int number) => true;

    /// <summary>Clears the registry entry for a slot without registering anything new.</summary>
    public void ClearRegisteredElement(SelectRule selectionRule, int number = 1)
        => SelectionRuleRegistrationService.ClearRegisteredElement(_registered, selectionRule, number);

    /// <summary>
    /// Directly registers the element (identified by <paramref name="id"/>) that was
    /// selected for the given selection rule.  The element's Acquisition is configured so
    /// that CharacterManager routes it to the correct ProgressionManager.
    /// </summary>
    public void SetRegisteredElement(SelectRule selectionRule, string id, int number = 1)
        => SelectionRuleRegistrationService.SetRegisteredElement(_registered, selectionRule, id, number);

    public object GetRegisteredElement(SelectRule selectionRule, int number = 1)
    {
        _registered.TryGetValue($"{selectionRule.UniqueIdentifier}:{number}", out var element);
        return element!;
    }

    /// <summary>
    /// Always 0: no WPF expander controls exist, so CharacterManager.New()'s cleanup
    /// loop exits on its first iteration.
    /// </summary>
    public int GetExpandersCount() => 0;

    public void FocusExpander(SelectRule rule, int number = 1) { }

    public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel) { }

    public void RemoveAllExpanders() => _registered.Clear();

    public bool RequiresSelection(SelectRule rule, int number = 1) => false;

    public int GetRetrainLevel(SelectRule rule, int number) => 0;
}

/// <summary>
/// MAUI implementation of ISpellcastingSectionHandler that stores prepared spell IDs
/// so CharacterSnapshot can reflect them when building the spell list.
/// </summary>
internal sealed class MauiSpellcastingSectionHandler : ISpellcastingSectionHandler
{
    // Key: spellcasting class name (e.g. "Cleric"), Value: set of prepared element IDs loaded from XML.
    private readonly Dictionary<string, HashSet<string>> _preparedIds
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Clears prepared state. Call before loading a new character.</summary>
    public void ResetPreparedState() => _preparedIds.Clear();

    /// <summary>Returns the prepared spell element IDs for the given spellcasting class.</summary>
    public IReadOnlyCollection<string> GetPreparedIds(string spellcastingName) =>
        _preparedIds.TryGetValue(spellcastingName, out var ids) ? ids : Array.Empty<string>();

    public SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;

    public bool SetPrepareSpell(SpellcastingInformation information, string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return false;
        if (!_preparedIds.TryGetValue(information.Name, out var ids))
            _preparedIds[information.Name] = ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ids.Add(elementId);
        return true;
    }

    public void UnsetPrepareSpell(string spellcastingName, string elementId)
    {
        if (_preparedIds.TryGetValue(spellcastingName, out var ids))
            ids.Remove(elementId);
    }
}

/// <summary>
/// MAUI implementation of the shared launcher contract.
/// Uses MAUI Essentials where possible and returns false when the platform
/// cannot open the requested target.
/// </summary>
internal sealed class MauiExternalLauncher : IExternalLauncher
{
    public bool OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (File.Exists(path))
            {
                _ = Launcher.Default.OpenAsync(
                    new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path)));
                return true;
            }

            Uri? uri = TryCreateUri(path);
            if (uri == null)
                return false;

            _ = Launcher.Default.OpenAsync(uri);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Launcher:Path] {path}\n{ex}");
            return false;
        }
    }

    public bool OpenUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        try
        {
            _ = Launcher.Default.OpenAsync(new Uri(uri, UriKind.Absolute));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Launcher:Uri] {uri}\n{ex}");
            return false;
        }
    }

    private static Uri? TryCreateUri(string pathOrUri)
    {
        if (Uri.TryCreate(pathOrUri, UriKind.Absolute, out var uri))
            return uri;

        if (Path.IsPathRooted(pathOrUri))
            return new Uri(Path.GetFullPath(pathOrUri));

        return null;
    }
}
