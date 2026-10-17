using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

public static partial class BuildService
{
    private const string LegacyClericId = "ID_WOTC_PHB_CLASS_CLERIC";
    private const string RevisedClericId = "ID_WOTC_PHB24_CLASS_CLERIC";

    private sealed record ClericSpellSelections(
        string ClassId, SpellcastingInformation PreviousProfile,
        IReadOnlyList<ElementBase> Cantrips, IReadOnlyList<ElementBase> Prepared);

    private static ClericSpellSelections? CaptureClericSpellSelections(
        SelectRule rule, string replacementId, int number)
    {
        if (rule.Attributes.Type != "Class"
            || SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, number) is not ElementBase previous
            || !((previous.Id == LegacyClericId && replacementId == RevisedClericId)
                || (previous.Id == RevisedClericId && replacementId == LegacyClericId)))
            return null;

        var manager = CharacterManager.Current;
        var progression = manager.ClassProgressionManagers.FirstOrDefault(p => p.ClassElement == previous);
        var profile = progression?.SpellcastingInformations.SingleOrDefault(p => !p.IsExtension && p.Name == "Cleric");
        if (progression is null || profile is null) return null;

        var cantrips = ClericCantripSlots(progression, profile)
            .Select(slot => SelectionRuleExpanderContext.Current?.GetRegisteredElement(slot.Rule, slot.Number))
            .OfType<ElementBase>().Where(e => GetElementSpellLevel(e) == 0).ToArray();
        var automatic = SpellAcquisitionResolver.Resolve(manager.GetElements(), manager.GetSpellcastingInformations())
            .Where(a => a.ProfileKey == SpellAcquisitionResolver.ProfileKey(profile) && a.IsAlwaysPrepared)
            .Select(a => a.Spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prepared = (SpellcastingSectionContext.Current?.GetPreparedIds(profile) ?? [])
            .Where(id => !automatic.Contains(id))
            .Select(id => DataManager.Current.ElementsCollection.GetElement(id))
            .OfType<ElementBase>().Where(e => GetElementSpellLevel(e) > 0)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        return new(replacementId, profile, cantrips, prepared);
    }

    private static IEnumerable<(SelectRule Rule, int Number)> ClericCantripSlots(
        ClassProgressionManager progression, SpellcastingInformation profile)
    {
        var manager = CharacterManager.Current;
        var elements = manager.GetElements().ToArray();
        var profiles = manager.GetSpellcastingInformations().ToArray();
        // Subclass and feat grants are rebuilt by their own features; they must not
        // consume the replacement class's ordinary cantrip choices.
        return manager.SelectionRules
            .Where(r => r.Attributes.Type == "Spell"
                && r.ElementHeader.Type is "Class" or "Class Feature"
                && ReferenceEquals(manager.GetProgressManager(r), progression)
                && SpellAcquisitionResolver.ResolveProfile(r, elements, profiles) is { } owner
                && SpellAcquisitionResolver.ProfileKey(owner) == SpellAcquisitionResolver.ProfileKey(profile))
            .OrderBy(r => r.Attributes.RequiredLevel)
            .SelectMany(r => Enumerable.Range(1, Math.Max(0, r.Attributes.Number)).Select(n => (r, n)));
    }

    private static void RestoreClericSpellSelections(ClericSpellSelections? saved, List<string> invalidated)
    {
        if (saved is null) return;
        var manager = CharacterManager.Current;
        var progression = manager.ClassProgressionManagers.FirstOrDefault(p => p.ClassElement?.Id == saved.ClassId);
        var profile = progression?.SpellcastingInformations.SingleOrDefault(p => !p.IsExtension && p.Name == "Cleric");
        var selectionHandler = SelectionRuleExpanderContext.Current;
        var spellHandler = SpellcastingSectionContext.Current;
        if (progression is null || profile is null || selectionHandler is null || spellHandler is null)
            throw new InvalidOperationException("The replacement Cleric's spellcasting choices could not be restored.");

        var slots = ClericCantripSlots(progression, profile).ToArray();
        foreach (var spell in saved.Cantrips)
        {
            var slot = slots.FirstOrDefault(s => selectionHandler.GetRegisteredElement(s.Rule, s.Number) is null
                && GetOptions(s.Rule, s.Number).Any(o => o.Id == spell.Id && !o.IsDisabled));
            if (slot.Rule is null)
                invalidated.Add($"Cleric cantrip: {spell.Name}");
            else
                selectionHandler.SetRegisteredElement(slot.Rule, spell.Id, slot.Number);
        }
        manager.ReprocessCharacter();

        // A previous visit to this edition may have left preparation IDs behind.
        // Replace that state with this switch's choices, never merge old selections.
        foreach (var id in spellHandler.GetPreparedIds(profile).ToArray())
            spellHandler.UnsetPrepareSpell(profile, id);
        foreach (var id in spellHandler.GetPreparedIds(saved.PreviousProfile).ToArray())
            spellHandler.UnsetPrepareSpell(saved.PreviousProfile, id);

        var section = CharacterSnapshot.From(manager.Character).SpellcastingSections
            .Single(s => s.SourceId == profile.ElementHeader.Id && s.Name == profile.Name);
        int maxLevel = section.SpellLevels.Where(l => l.TotalSlots > 0).Select(l => l.Level).DefaultIfEmpty(0).Max();
        var eligible = section.SpellLevels.Where(l => l.Level <= maxLevel).SelectMany(l => l.Spells)
            .ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var restrictions = BuildSourceRestrictionSnapshot.CaptureCurrent();
        int remaining = Math.Max(0, section.MaxPrepared);
        foreach (var spell in saved.Prepared)
        {
            if (!eligible.TryGetValue(spell.Id, out var entry) || !restrictions.Allows(spell))
                invalidated.Add($"Prepared Cleric spell: {spell.Name}");
            else if (entry.IsAlwaysPrepared)
                continue;
            else if (remaining > 0 && spellHandler.SetPrepareSpell(profile, spell.Id))
                remaining--;
            else
                invalidated.Add($"Prepared Cleric spell: {spell.Name}");
        }
    }
}
