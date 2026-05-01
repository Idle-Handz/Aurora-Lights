namespace Aurora.Components.Models;

public sealed class MagicOverviewModel
{
    public bool HasSpellcasting { get; set; }
    public IReadOnlyList<MagicKnownSpellGroupModel> KnownSpellGroups { get; set; } = [];
    public IReadOnlyList<MagicSpellcastingSectionModel> Sections { get; set; } = [];
}

public sealed class MagicSpellcastingSectionModel
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsPreparedCaster { get; set; }
    public string SpellcastingAbility { get; set; } = string.Empty;
    public string SpellcastingDc { get; set; } = string.Empty;
    public string SpellcastingAttack { get; set; } = string.Empty;
    public int PreparedCount { get; set; }
    public int MaxPrepared { get; set; }
    public IReadOnlyList<MagicSpellListEntryModel> Cantrips { get; set; } = [];
    public IReadOnlyList<MagicSpellLevelModel> SpellLevels { get; set; } = [];
}

public sealed record MagicKnownSpellGroupModel(string Label, IReadOnlyList<MagicKnownSpellEntryModel> Entries);

public sealed record MagicKnownSpellEntryModel(string Id, string Label, string? CurrentName);

public sealed class MagicSpellListEntryModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsPrepared { get; set; }
    public bool IsAlwaysPrepared { get; set; }
    public bool IsCantrip { get; set; }

    public MagicSpellListEntryModel()
    {
    }

    public MagicSpellListEntryModel(string id, string name, int level, string source, bool isPrepared, bool isAlwaysPrepared, bool isCantrip = false)
    {
        Id = id;
        Name = name;
        Level = level;
        Source = source;
        IsPrepared = isPrepared;
        IsAlwaysPrepared = isAlwaysPrepared;
        IsCantrip = isCantrip;
    }
}

public sealed class MagicSpellLevelModel
{
    public int Level { get; set; }
    public IReadOnlyList<MagicSpellListEntryModel> Spells { get; set; } = [];
    public int TotalSlots { get; set; }
    public int UsedSlots { get; set; }

    public MagicSpellLevelModel()
    {
    }

    public MagicSpellLevelModel(int level, IReadOnlyList<MagicSpellListEntryModel> spells, int totalSlots, int usedSlots)
    {
        Level = level;
        Spells = spells;
        TotalSlots = totalSlots;
        UsedSlots = usedSlots;
    }
}

public sealed record MagicSpellDetailModel(
    string Id,
    string Name,
    string Source,
    int Level,
    string Subtitle,
    string CastingTime,
    string Range,
    string Components,
    string Duration,
    string Description);

public sealed record MagicPreparedChangeModel(string SpellId, bool Value, string SpellcastingSectionId, string SpellcastingClass);

public sealed record MagicSlotToggleModel(int Level, int SlotIndex);
