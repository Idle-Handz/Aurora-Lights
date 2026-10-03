using Builder.Presentation.UserControls.Spellcasting;

namespace Builder.Presentation.Interfaces;

/// <summary>
/// Cross-platform contract for spellcasting section management.
/// The WPF implementation (SpellcastingSectionHandler) holds a collection of
/// SpellcasterSelectionControl UserControls; this interface exposes only the
/// ViewModel surface so that Aurora.Logic code (CharacterManager,
/// CharacterSheetGenerator) can query spell data without a WPF dependency.
/// </summary>
public interface ISpellcastingSectionHandler
{
    SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier);
    bool SetPrepareSpell(Builder.Data.Elements.SpellcastingInformation information, string elementId);

    /// <summary>
    /// Removes <paramref name="elementId"/> from the prepared set for the given class.
    /// WPF derives prepared state from live controls and ignores this; MAUI uses it to
    /// keep the in-memory <c>_preparedIds</c> set in sync when the user un-prepares a spell.
    /// Default implementation is a no-op so existing WPF code needs no changes.
    /// </summary>
    void UnsetPrepareSpell(string spellcastingName, string elementId) { }

    /// <summary>
    /// Clears any cached prepared-spell state before loading a different character.
    /// WPF derives this from live controls, so the default implementation is a no-op.
    /// </summary>
    void ResetPreparedState() { }

    /// <summary>
    /// Returns the element IDs of spells that the user has manually prepared for the
    /// given spellcasting class name (e.g. "Artificer"). Used when no WPF ViewModel
    /// is available to derive prepared state. Returns an empty collection by default.
    /// </summary>
    IReadOnlyCollection<string> GetPreparedIds(string spellcastingName)
        => Array.Empty<string>();
    IReadOnlyCollection<string> GetPreparedIds(Builder.Data.Elements.SpellcastingInformation profile)
        => GetPreparedIds(profile.Name);
    void UnsetPrepareSpell(Builder.Data.Elements.SpellcastingInformation profile, string elementId)
        => UnsetPrepareSpell(profile.Name, elementId);
}
