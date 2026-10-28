using Builder.Data.Elements;

namespace Builder.Presentation.Utilities;

public sealed record SpellSlotCapacity(int Level, int TotalSlots);

public sealed record SpellSlotPool(string Id, string Label, bool RecoversOnShortRest,
    IReadOnlyList<SpellSlotCapacity> Levels);

/// <summary>Playable slot capacity, independent of each class's spell-learning limits.</summary>
public static class SpellSlotPoolResolver
{
    public const string SpellcastingId = "spellcasting";
    public const string PactMagicId = "pact-magic";

    // Aurora content keys its slot statistics by the spellcasting profile name.
    public static bool IsPactMagic(SpellcastingInformation info) =>
        string.Equals(info.Name, "Warlock", StringComparison.OrdinalIgnoreCase)
        || string.Equals(info.ElementHeader?.Name, "Pact Magic", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<SpellSlotCapacity> ForProfile(CharacterManager cm, SpellcastingInformation info)
    {
        var combined = cm.Character.MulticlassSpellSlots;
        int[] shared = [combined.Slot1, combined.Slot2, combined.Slot3, combined.Slot4, combined.Slot5,
            combined.Slot6, combined.Slot7, combined.Slot8, combined.Slot9];
        return Enumerable.Range(1, 9)
            .Select(level => new SpellSlotCapacity(level,
                cm.Status.HasMulticlassSpellSlots && !IsPactMagic(info)
                    ? shared[level - 1]
                    : cm.StatisticsCalculator.StatisticValues.GetValue(info.GetSlotStatisticName(level))))
            .Where(level => level.TotalSlots > 0).ToList();
    }

    public static IReadOnlyList<SpellSlotPool> Resolve(CharacterManager cm) =>
        cm.GetSpellcastingInformations().Where(info => !info.IsExtension)
            .GroupBy(IsPactMagic)
            .Select(group => new SpellSlotPool(
                group.Key ? PactMagicId : SpellcastingId,
                group.Key ? "Pact Magic" : "Spellcasting",
                group.Key,
                group.SelectMany(info => ForProfile(cm, info)).GroupBy(level => level.Level)
                    .Select(level => new SpellSlotCapacity(level.Key, level.Max(slot => slot.TotalSlots)))
                    .OrderBy(level => level.Level).ToList()))
            .Where(pool => pool.Levels.Count > 0).OrderBy(pool => pool.RecoversOnShortRest).ToList();
}
