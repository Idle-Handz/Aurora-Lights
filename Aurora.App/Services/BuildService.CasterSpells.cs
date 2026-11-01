using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

public static partial class BuildService
{
    private sealed record CasterSpellSelections(string ClassId, IReadOnlyList<CasterProfileSelections> Profiles);

    private sealed record CasterProfileSelections(
        SpellcastingInformation PreviousProfile,
        IReadOnlyList<ElementBase> Selected, IReadOnlyList<ElementBase> Prepared);

    private static CasterSpellSelections? CaptureCasterSpellSelections(
        SelectRule rule, string replacementId, int number)
    {
        if (rule.Attributes.Type is not ("Class" or "Multiclass")
            || SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, number) is not ElementBase previous
            || previous.Id == replacementId)
            return null;

        var manager = CharacterManager.Current;
        var progression = manager.ClassProgressionManagers.FirstOrDefault(p => p.ClassElement == previous);
        if (progression is null) return null;

        var acquisitions = SpellAcquisitionResolver.Resolve(manager.GetElements(), manager.GetSpellcastingInformations());
        var saved = new List<CasterProfileSelections>();
        foreach (var profile in progression.SpellcastingInformations.Where(p => !p.IsExtension))
        {
            var selected = CasterSpellSlots(progression, profile)
                .Select(slot => SelectionRuleExpanderContext.Current?.GetRegisteredElement(slot.Rule, slot.Number))
                .OfType<ElementBase>().Where(e => GetElementSpellLevel(e) >= 0)
                .DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            var automatic = acquisitions
                .Where(a => a.ProfileKey == SpellAcquisitionResolver.ProfileKey(profile) && a.IsAlwaysPrepared)
                .Select(a => a.Spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // A known caster's learned spells are all active. Spellbook casters also retain
            // their unprepared choices, without automatically preparing them in the new class.
            var prepared = profile.Prepare
                ? (SpellcastingSectionContext.Current?.GetPreparedIds(profile) ?? [])
                    .Where(id => !automatic.Contains(id))
                    .Select(id => DataManager.Current.ElementsCollection.GetElement(id))
                    .OfType<ElementBase>().Where(e => GetElementSpellLevel(e) > 0)
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray()
                : selected.Where(e => GetElementSpellLevel(e) > 0).ToArray();
            saved.Add(new(profile, selected, prepared));
        }

        return saved.Count == 0 ? null : new(replacementId, saved);
    }

    private static IEnumerable<(SelectRule Rule, int Number)> CasterSpellSlots(
        ClassProgressionManager progression, SpellcastingInformation profile)
    {
        var manager = CharacterManager.Current;
        var elements = manager.GetElements().ToArray();
        var profiles = manager.GetSpellcastingInformations().ToArray();
        // Only the replaced class's ordinary choices move. Grants and feat spell choices
        // retain their own origins and must not consume the new class's choices.
        return manager.SelectionRules
            .Where(r => r.Attributes.Type == "Spell"
                && r.ElementHeader.Type is "Class" or "Multiclass" or "Class Feature" or "Archetype" or "Archetype Feature"
                && !r.IsAlwaysPrepared()
                && (!r.PreservedAttributes.TryGetValue("spell-access", out var access)
                    || access is not ("feature" or "list" or "ritual" or "always-prepared"))
                && ReferenceEquals(manager.GetProgressManager(r), progression)
                && SpellAcquisitionResolver.ResolveProfile(r, elements, profiles) is { } owner
                && SpellAcquisitionResolver.ProfileKey(owner) == SpellAcquisitionResolver.ProfileKey(profile))
            .OrderBy(r => r.Attributes.RequiredLevel)
            .SelectMany(r => Enumerable.Range(1, Math.Max(0, r.Attributes.Number)).Select(n => (r, n)));
    }

    private static void RestoreCasterSpellSelections(CasterSpellSelections? saved, List<string> invalidated)
    {
        if (saved is null) return;
        var manager = CharacterManager.Current;
        var progression = manager.ClassProgressionManagers.FirstOrDefault(p => p.ClassElement?.Id == saved.ClassId);
        var profiles = progression?.SpellcastingInformations.Where(p => !p.IsExtension).ToArray() ?? [];
        var selectionHandler = SelectionRuleExpanderContext.Current;
        var spellHandler = SpellcastingSectionContext.Current;
        if (selectionHandler is null || spellHandler is null)
            throw new InvalidOperationException("The replacement class's spellcasting choices could not be restored.");

        // A previous visit to either class may have left preparation IDs behind.
        // Replace that state with this switch's choices, never merge old selections.
        foreach (var previous in saved.Profiles)
            foreach (var id in spellHandler.GetPreparedIds(previous.PreviousProfile).ToArray())
                spellHandler.UnsetPrepareSpell(previous.PreviousProfile, id);

        var destinations = profiles.ToList();
        var unmatched = saved.Profiles.ToList();
        var matches = new List<(CasterProfileSelections Saved, SpellcastingInformation Profile)>();
        foreach (var previous in saved.Profiles)
        {
            var sameName = destinations.Where(p => p.Name.Equals(previous.PreviousProfile.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (sameName.Length != 1) continue;
            matches.Add((previous, sameName[0]));
            destinations.Remove(sameName[0]);
            unmatched.Remove(previous);
        }
        // Different classes normally have different profile names. A single remaining
        // pair is unambiguous; multiple independent casting features must not be mixed.
        if (unmatched.Count == 1 && destinations.Count == 1)
        {
            matches.Add((unmatched[0], destinations[0]));
            unmatched.Clear();
        }

        foreach (var match in matches)
        {
            var profile = match.Profile;
            foreach (var id in spellHandler.GetPreparedIds(profile).ToArray())
                spellHandler.UnsetPrepareSpell(profile, id);

            // Prefer active spells when the destination has fewer learned-spell choices.
            var candidates = match.Saved.Selected.Where(e => GetElementSpellLevel(e) == 0)
                .Concat(match.Saved.Prepared).Concat(match.Saved.Selected)
                .DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            var alreadyAcquired = SpellAcquisitionResolver.Resolve(manager.GetElements(), manager.GetSpellcastingInformations())
                .Where(a => a.ProfileKey == SpellAcquisitionResolver.ProfileKey(profile)
                    && !a.IsFeature && a.Preparation is not (SpellPreparation.ListOnly or SpellPreparation.RitualOnly))
                .Select(a => a.Spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            RestoreCasterSpellChoices(progression!, profile,
                candidates.Where(e => !alreadyAcquired.Contains(e.Id)).ToArray());
            manager.ReprocessCharacter();

            var section = CharacterSnapshot.From(manager.Character).SpellcastingSections
                .Single(s => s.SourceId == profile.ElementHeader.Id && s.Name == profile.Name);
            int maxLevel = section.SpellLevels.Where(l => l.TotalSlots > 0).Select(l => l.Level).DefaultIfEmpty(0).Max();
            var eligible = section.SpellLevels.Where(l => !profile.Prepare || l.Level <= maxLevel).SelectMany(l => l.Spells)
                .ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            var cantrips = section.Cantrips.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var active = match.Saved.Prepared.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var restrictions = BuildSourceRestrictionSnapshot.CaptureCurrent();
            int remaining = Math.Max(0, section.MaxPrepared);
            foreach (var spell in candidates)
            {
                bool prepared = active.Contains(spell.Id);
                string label = GetElementSpellLevel(spell) == 0
                    ? $"{match.Saved.PreviousProfile.Name} cantrip: {spell.Name}"
                    : $"{(prepared && match.Saved.PreviousProfile.Prepare ? "Prepared " : "")}{match.Saved.PreviousProfile.Name} spell: {spell.Name}";
                if (GetElementSpellLevel(spell) == 0)
                {
                    if (!cantrips.Contains(spell.Id)) invalidated.Add(label);
                }
                else if (!eligible.TryGetValue(spell.Id, out var entry) || !restrictions.Allows(spell))
                    invalidated.Add(label);
                else if (!profile.Prepare || !prepared || entry.IsAlwaysPrepared)
                    continue;
                else if (remaining > 0 && spellHandler.SetPrepareSpell(profile, spell.Id))
                    remaining--;
                else
                    invalidated.Add(label);
            }
        }

        if (profiles.Length > 0)
            foreach (var previous in unmatched)
                foreach (var spell in previous.Selected.Concat(previous.Prepared).DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                    invalidated.Add($"{previous.PreviousProfile.Name} spell: {spell.Name}");
    }

    private static void RestoreCasterSpellChoices(
        ClassProgressionManager progression, SpellcastingInformation profile, IReadOnlyList<ElementBase> spells)
    {
        var handler = SelectionRuleExpanderContext.Current!;
        var eligibleByRule = new Dictionary<SelectRule, HashSet<string>>();
        var slots = CasterSpellSlots(progression, profile)
            .Where(s => handler.GetRegisteredElement(s.Rule, s.Number) is null)
            .Select(s =>
            {
                // All of these slots are empty, so options only need to be queried once
                // for each rule, even for a large spellbook's repeated choices.
                if (!eligibleByRule.TryGetValue(s.Rule, out var eligible))
                    eligibleByRule[s.Rule] = eligible = GetOptions(s.Rule, s.Number)
                        .Where(o => !o.IsDisabled).Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return (s.Rule, s.Number, Eligible: eligible);
            })
            .ToArray();
        var assignments = Enumerable.Repeat(-1, slots.Length).ToArray();

        // Match across all slots before registering. A spell that fits a flexible choice
        // must not block another spell whose only legal choice is that same slot.
        bool Assign(int spellIndex, HashSet<int> visited)
        {
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (!slots[slotIndex].Eligible.Contains(spells[spellIndex].Id) || !visited.Add(slotIndex)) continue;
                if (assignments[slotIndex] < 0 || Assign(assignments[slotIndex], visited))
                {
                    assignments[slotIndex] = spellIndex;
                    return true;
                }
            }
            return false;
        }

        for (int n = 0; n < spells.Count; n++) Assign(n, []);
        for (int n = 0; n < slots.Length; n++)
            if (assignments[n] >= 0)
                handler.SetRegisteredElement(slots[n].Rule, spells[assignments[n]].Id, slots[n].Number);
    }
}
