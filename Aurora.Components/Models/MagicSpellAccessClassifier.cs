namespace Aurora.Components.Models;

public static class MagicSpellAccessClassifier
{
    public static void Apply(MagicOverviewModel model)
    {
        var candidates = new List<AccessCandidate>();
        IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> selections =
            model.KnownSpellGroups
                .SelectMany(group => group.Entries.Select(entry => (group, entry)))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.entry.CurrentName))
                .ToList();

        AddSelectionCandidates(model, selections, candidates);

        foreach (MagicSpellcastingSectionModel section in model.Sections)
        {
            IEnumerable<MagicSpellListEntryModel> spells = section.Cantrips
                .Concat(section.SpellLevels.SelectMany(level => level.Spells));

            foreach (MagicSpellListEntryModel spell in spells)
            {
                IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> matchingSelections =
                    selections.Where(pair => Matches(pair.Entry, spell)).ToList();
                IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> localSelections =
                    matchingSelections.Where(pair => BelongsToSection(pair.Group, section)).ToList();
                bool hasRitualBookAccess = matchingSelections.Any(pair =>
                    pair.Entry.SelectionAccess == MagicSpellSelectionAccess.RitualBook
                    && (BelongsToSection(pair.Group, section)
                        || IsGlobalSourceGroup(model, pair.Group)));

                NormalizeRitualBookOnlySpell(section, spell, localSelections, hasRitualBookAccess);
                AddSectionCandidates(section, spell, localSelections, matchingSelections.Count > 0, candidates);
            }
        }

        foreach (MagicSpellcastingSectionModel section in model.Sections)
        {
            foreach (MagicSpellListEntryModel spell in section.Cantrips
                         .Concat(section.SpellLevels.SelectMany(level => level.Spells)))
            {
                spell.AccessPaths = candidates
                    .Where(candidate => Matches(candidate, spell))
                    .Select(candidate => candidate.Path)
                    .Distinct()
                    .OrderBy(path => AccessSortOrder(path.Kind))
                    .ThenBy(path => path.SourceLabel, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            section.PreparedCount = section.SpellLevels
                .SelectMany(level => level.Spells)
                .Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared);
        }
    }

    public static MagicSpellSelectionAccess InferSelectionAccess(
        string ruleName,
        string supports,
        int spellLevel,
        bool isAlwaysPrepared,
        bool hasMatchingSection,
        bool isPreparedCaster,
        bool isSpellbookCaster)
    {
        if (IsRitualBookRule(ruleName, supports))
        {
            return MagicSpellSelectionAccess.RitualBook;
        }

        if (spellLevel == 0)
        {
            return MagicSpellSelectionAccess.Known;
        }

        if (isAlwaysPrepared)
        {
            return MagicSpellSelectionAccess.AlwaysPrepared;
        }

        if (isSpellbookCaster
            && ruleName.Contains("spellbook", StringComparison.OrdinalIgnoreCase))
        {
            return MagicSpellSelectionAccess.Spellbook;
        }

        if (!hasMatchingSection)
        {
            return MagicSpellSelectionAccess.Granted;
        }

        return isPreparedCaster
            ? MagicSpellSelectionAccess.Unknown
            : MagicSpellSelectionAccess.Known;
    }

    private static void AddSelectionCandidates(
        MagicOverviewModel model,
        IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> selections,
        ICollection<AccessCandidate> candidates)
    {
        foreach ((MagicKnownSpellGroupModel group, MagicKnownSpellEntryModel entry) in selections)
        {
            MagicSpellSelectionAccess access = group.ReadOnlyGroup
                ? MagicSpellSelectionAccess.Granted
                : entry.SelectionAccess;
            string source = FirstNonEmpty(entry.AccessSource, entry.GrantedBy, group.Label, "Feature");
            string castingSectionId = group.SectionId ?? string.Empty;

            MagicSpellAccessPathModel? path = access switch
            {
                MagicSpellSelectionAccess.Known => new(
                    MagicSpellAccessKind.Known,
                    castingSectionId,
                    source,
                    CanCastNormally: true,
                    CanCastAsRitual: EntryCanBeRitualCast(model, entry, castingSectionId)),
                MagicSpellSelectionAccess.AlwaysPrepared => new(
                    MagicSpellAccessKind.AlwaysPrepared,
                    castingSectionId,
                    source,
                    CanCastNormally: true,
                    CanCastAsRitual: EntryCanBeRitualCast(model, entry, castingSectionId)),
                MagicSpellSelectionAccess.Granted => new(
                    MagicSpellAccessKind.Granted,
                    castingSectionId,
                    source,
                    CanCastNormally: true,
                    CanCastAsRitual: false),
                MagicSpellSelectionAccess.RitualBook when entry.IsRitual => new(
                    MagicSpellAccessKind.RitualBook,
                    castingSectionId,
                    source,
                    CanCastNormally: false,
                    CanCastAsRitual: true),
                _ => null,
            };

            if (path is not null)
            {
                candidates.Add(new AccessCandidate(entry.SpellId, entry.CurrentName!, entry.SpellLevel, path));
            }
        }
    }

    private static void NormalizeRitualBookOnlySpell(
        MagicSpellcastingSectionModel section,
        MagicSpellListEntryModel spell,
        IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> localSelections,
        bool hasRitualBookAccess)
    {
        if (section.IsPreparedCaster || spell.IsCantrip)
        {
            return;
        }

        bool hasNormalSelection = localSelections.Any(pair =>
            pair.Entry.SelectionAccess is MagicSpellSelectionAccess.Known
                or MagicSpellSelectionAccess.AlwaysPrepared
                or MagicSpellSelectionAccess.Granted);

        if (hasRitualBookAccess && !hasNormalSelection)
        {
            spell.IsPrepared = false;
            spell.IsAlwaysPrepared = false;
            spell.DisplayState = MagicSpellDisplayState.Available;
        }
    }

    private static void AddSectionCandidates(
        MagicSpellcastingSectionModel section,
        MagicSpellListEntryModel spell,
        IReadOnlyList<(MagicKnownSpellGroupModel Group, MagicKnownSpellEntryModel Entry)> localSelections,
        bool hasAnyMatchingSelection,
        ICollection<AccessCandidate> candidates)
    {
        bool ritualBookOnly = localSelections.Any(pair =>
                                  pair.Entry.SelectionAccess == MagicSpellSelectionAccess.RitualBook)
                              && !localSelections.Any(pair =>
                                  pair.Entry.SelectionAccess is MagicSpellSelectionAccess.Known
                                      or MagicSpellSelectionAccess.AlwaysPrepared
                                      or MagicSpellSelectionAccess.Granted);
        string grantedSource = localSelections
            .Where(pair => (pair.Entry.SelectionAccess is MagicSpellSelectionAccess.AlwaysPrepared
                or MagicSpellSelectionAccess.Granted) || pair.Group.ReadOnlyGroup)
            .Select(pair => FirstNonEmpty(
                pair.Entry.AccessSource,
                pair.Entry.GrantedBy,
                pair.Group.Label,
                section.Label))
            .FirstOrDefault() ?? string.Empty;

        if (spell.IsCantrip)
        {
            AddCandidate(candidates, spell, new MagicSpellAccessPathModel(
                MagicSpellAccessKind.Known,
                section.Id,
                section.Label,
                CanCastNormally: true,
                CanCastAsRitual: false));
        }
        else if (section.IsPreparedCaster)
        {
            if (spell.IsAlwaysPrepared)
            {
                AddCandidate(candidates, spell, new MagicSpellAccessPathModel(
                    MagicSpellAccessKind.AlwaysPrepared,
                    section.Id,
                    FirstNonEmpty(grantedSource, spell.GrantedBy, section.Label),
                    CanCastNormally: true,
                    CanCastAsRitual: spell.IsRitual
                        && section.RitualCastingMode != MagicRitualCastingMode.None));
            }
            else if (spell.IsPrepared)
            {
                AddCandidate(candidates, spell, new MagicSpellAccessPathModel(
                    MagicSpellAccessKind.Prepared,
                    section.Id,
                    section.Label,
                    CanCastNormally: true,
                    CanCastAsRitual: spell.IsRitual
                        && section.RitualCastingMode == MagicRitualCastingMode.PreparedSpells));
            }
        }
        else if (!ritualBookOnly
                 && (localSelections.Count > 0 || !hasAnyMatchingSelection))
        {
            AddCandidate(candidates, spell, new MagicSpellAccessPathModel(
                MagicSpellAccessKind.Known,
                section.Id,
                section.Label,
                CanCastNormally: true,
                CanCastAsRitual: spell.IsRitual
                    && section.RitualCastingMode == MagicRitualCastingMode.KnownSpells));
        }

        if (spell.IsRitual && section.RitualCastingMode == MagicRitualCastingMode.Spellbook)
        {
            AddCandidate(candidates, spell, new MagicSpellAccessPathModel(
                MagicSpellAccessKind.Spellbook,
                section.Id,
                $"{section.Label} Spellbook",
                CanCastNormally: false,
                CanCastAsRitual: true));
        }
    }

    private static bool BelongsToSection(
        MagicKnownSpellGroupModel group,
        MagicSpellcastingSectionModel section) =>
        !string.IsNullOrWhiteSpace(group.SectionId)
            ? string.Equals(group.SectionId, section.Id, StringComparison.OrdinalIgnoreCase)
            : string.Equals(group.Label, section.Label, StringComparison.OrdinalIgnoreCase);

    private static bool IsGlobalSourceGroup(
        MagicOverviewModel model,
        MagicKnownSpellGroupModel group) =>
        string.IsNullOrWhiteSpace(group.SectionId)
        && !model.Sections.Any(section =>
            string.Equals(section.Label, group.Label, StringComparison.OrdinalIgnoreCase));

    private static bool EntryCanBeRitualCast(
        MagicOverviewModel model,
        MagicKnownSpellEntryModel entry,
        string castingSectionId)
    {
        if (!entry.IsRitual)
        {
            return false;
        }

        MagicSpellcastingSectionModel? section = model.Sections.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, castingSectionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Label, castingSectionId, StringComparison.OrdinalIgnoreCase));
        return section?.RitualCastingMode == MagicRitualCastingMode.KnownSpells;
    }

    private static bool IsRitualBookRule(string ruleName, string supports)
    {
        if (!supports.Contains("ritual", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ruleName.Contains("ritual caster", StringComparison.OrdinalIgnoreCase)
            || ruleName.Contains("ritual book", StringComparison.OrdinalIgnoreCase)
            || ruleName.Contains("book of ancient secrets", StringComparison.OrdinalIgnoreCase)
            || ruleName.Contains("book of rituals", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCandidate(
        ICollection<AccessCandidate> candidates,
        MagicSpellListEntryModel spell,
        MagicSpellAccessPathModel path) =>
        candidates.Add(new AccessCandidate(spell.Id, spell.Name, spell.Level, path));

    private static bool Matches(MagicKnownSpellEntryModel entry, MagicSpellListEntryModel spell)
    {
        if (!string.IsNullOrWhiteSpace(entry.SpellId) && !string.IsNullOrWhiteSpace(spell.Id))
        {
            return string.Equals(entry.SpellId, spell.Id, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(entry.CurrentName, spell.Name, StringComparison.OrdinalIgnoreCase)
            && entry.SpellLevel == spell.Level;
    }

    private static bool Matches(AccessCandidate candidate, MagicSpellListEntryModel spell)
    {
        if (!string.IsNullOrWhiteSpace(candidate.SpellId) && !string.IsNullOrWhiteSpace(spell.Id))
        {
            return string.Equals(candidate.SpellId, spell.Id, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(candidate.SpellName, spell.Name, StringComparison.OrdinalIgnoreCase)
            && candidate.SpellLevel == spell.Level;
    }

    private static int AccessSortOrder(MagicSpellAccessKind kind) => kind switch
    {
        MagicSpellAccessKind.AlwaysPrepared => 0,
        MagicSpellAccessKind.Prepared => 1,
        MagicSpellAccessKind.Known => 2,
        MagicSpellAccessKind.Granted => 3,
        MagicSpellAccessKind.Spellbook => 4,
        _ => 5,
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record AccessCandidate(
        string? SpellId,
        string SpellName,
        int SpellLevel,
        MagicSpellAccessPathModel Path);
}
