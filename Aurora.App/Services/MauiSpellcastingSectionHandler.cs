using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.UserControls.Spellcasting;

namespace Aurora.App.Services;

/// <summary>
/// MAUI implementation of ISpellcastingSectionHandler that stores prepared spell IDs
/// so CharacterSnapshot can reflect them when building the spell list.
/// </summary>
internal sealed class MauiSpellcastingSectionHandler : ISpellcastingSectionHandler
{
    // Stable source ID + profile name. Runtime GUIDs change when content reloads.
    private readonly Dictionary<string, HashSet<string>> _preparedIds
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Clears prepared state. Call before loading a new character.</summary>
    public void ResetPreparedState() => _preparedIds.Clear();

    /// <summary>Returns the prepared spell element IDs for the given spellcasting class.</summary>
    public IReadOnlyCollection<string> GetPreparedIds(string spellcastingName)
    {
        var profiles = CharacterManager.Current.GetSpellcastingInformations()
            .Where(p => !p.IsExtension && p.Name == spellcastingName).ToArray();
        return profiles.Length == 1 ? GetPreparedIds(profiles[0]) : Array.Empty<string>();
    }

    public SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;

    public bool SetPrepareSpell(SpellcastingInformation information, string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return false;
        if (!_preparedIds.TryGetValue(SpellAcquisitionResolver.ProfileKey(information), out var ids))
            _preparedIds[SpellAcquisitionResolver.ProfileKey(information)] = ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ids.Add(elementId);
        return true;
    }

    public IReadOnlyCollection<string> GetPreparedIds(SpellcastingInformation profile) =>
        _preparedIds.TryGetValue(SpellAcquisitionResolver.ProfileKey(profile), out var ids) ? ids : Array.Empty<string>();

    public void UnsetPrepareSpell(SpellcastingInformation profile, string elementId)
    {
        if (_preparedIds.TryGetValue(SpellAcquisitionResolver.ProfileKey(profile), out var ids)) ids.Remove(elementId);
    }

    public void UnsetPrepareSpell(string spellcastingName, string elementId)
    {
        var profiles = CharacterManager.Current.GetSpellcastingInformations()
            .Where(p => !p.IsExtension && p.Name == spellcastingName).ToArray();
        if (profiles.Length == 1) UnsetPrepareSpell(profiles[0], elementId);
    }

}

