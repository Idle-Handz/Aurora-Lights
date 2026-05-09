namespace Aurora.Components.Models;

public sealed class MagicOverviewModel
{
    public bool HasSpellcasting { get; set; }
    public IReadOnlyList<MagicKnownSpellGroupModel> KnownSpellGroups { get; set; } = [];
    public IReadOnlyList<MagicSpellcastingSectionModel> Sections { get; set; } = [];

    // Back-compat convenience surface for older consumers that still assume a single active section.
    private MagicSpellcastingSectionModel PrimarySection => Sections.FirstOrDefault() ?? new MagicSpellcastingSectionModel();
    private bool? _isPreparedCaster;
    private string? _spellcastingClass;
    private string? _spellcastingAbility;
    private string? _spellcastingDc;
    private string? _spellcastingAttack;
    private int? _preparedCount;
    private int? _maxPrepared;
    private IReadOnlyList<MagicSpellListEntryModel>? _cantrips;
    private IReadOnlyList<MagicSpellLevelModel>? _spellLevels;

    public bool IsPreparedCaster
    {
        get => _isPreparedCaster ?? PrimarySection.IsPreparedCaster;
        set => _isPreparedCaster = value;
    }

    public string SpellcastingClass
    {
        get => _spellcastingClass ?? PrimarySection.Label;
        set => _spellcastingClass = value;
    }

    public string SpellcastingAbility
    {
        get => _spellcastingAbility ?? PrimarySection.SpellcastingAbility;
        set => _spellcastingAbility = value;
    }

    public string SpellcastingDc
    {
        get => _spellcastingDc ?? PrimarySection.SpellcastingDc;
        set => _spellcastingDc = value;
    }

    public string SpellcastingAttack
    {
        get => _spellcastingAttack ?? PrimarySection.SpellcastingAttack;
        set => _spellcastingAttack = value;
    }

    public int PreparedCount
    {
        get => _preparedCount ?? PrimarySection.PreparedCount;
        set => _preparedCount = value;
    }

    public int MaxPrepared
    {
        get => _maxPrepared ?? PrimarySection.MaxPrepared;
        set => _maxPrepared = value;
    }

    public IReadOnlyList<MagicSpellListEntryModel> Cantrips
    {
        get => _cantrips ?? PrimarySection.Cantrips;
        set => _cantrips = value;
    }

    public IReadOnlyList<MagicSpellLevelModel> SpellLevels
    {
        get => _spellLevels ?? PrimarySection.SpellLevels;
        set => _spellLevels = value;
    }
}

public sealed class MagicSpellcastingSectionModel
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool HasExtensions { get; set; }
    public bool IsPreparedCaster { get; set; }
    public string SpellcastingAbility { get; set; } = string.Empty;
    public string SpellcastingDc { get; set; } = string.Empty;
    public string SpellcastingAttack { get; set; } = string.Empty;
    public int PreparedCount { get; set; }
    public int MaxPrepared { get; set; }
    public IReadOnlyList<MagicSpellListEntryModel> Cantrips { get; set; } = [];
    public IReadOnlyList<MagicSpellLevelModel> SpellLevels { get; set; } = [];
}

public sealed record MagicKnownSpellGroupModel(string Label, string? SectionId, IReadOnlyList<MagicKnownSpellEntryModel> Entries)
{
    public MagicKnownSpellGroupModel(string Label, IReadOnlyList<MagicKnownSpellEntryModel> Entries)
        : this(Label, null, Entries)
    {
    }
}

public sealed record MagicKnownSpellEntryModel(string Id, string Label, string? CurrentName);

public sealed class MagicSpellListEntryModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Source { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public bool IsRitual { get; set; }
    public bool IsConcentration { get; set; }
    public bool IsPrepared { get; set; }
    public bool IsAlwaysPrepared { get; set; }
    public bool IsCantrip { get; set; }
    public MagicSpellDisplayState DisplayState { get; set; }

    public MagicSpellListEntryModel()
    {
    }

    public MagicSpellListEntryModel(string id, string name, int level, string source)
        : this(
            id,
            name,
            level,
            source,
            string.Empty,
            false,
            false,
            isPrepared: level == 0,
            isAlwaysPrepared: level == 0,
            isCantrip: level == 0,
            displayState: level == 0 ? MagicSpellDisplayState.AlwaysPrepared : MagicSpellDisplayState.Prepared)
    {
    }

    public MagicSpellListEntryModel(string id, string name, bool isPrepared, bool isAlwaysPrepared)
        : this(
            id,
            name,
            0,
            string.Empty,
            string.Empty,
            false,
            false,
            isPrepared,
            isAlwaysPrepared,
            isCantrip: false,
            displayState: isAlwaysPrepared ? MagicSpellDisplayState.AlwaysPrepared : (isPrepared ? MagicSpellDisplayState.Prepared : MagicSpellDisplayState.Available))
    {
    }

    public MagicSpellListEntryModel(string id, string name, int level, string source, bool isPrepared, bool isAlwaysPrepared, bool isCantrip = false)
        : this(
            id,
            name,
            level,
            source,
            string.Empty,
            false,
            false,
            isPrepared,
            isAlwaysPrepared,
            isCantrip,
            isAlwaysPrepared ? MagicSpellDisplayState.AlwaysPrepared : MagicSpellDisplayState.Prepared)
    {
    }

    public MagicSpellListEntryModel(
        string id,
        string name,
        int level,
        string source,
        string school,
        bool isRitual,
        bool isConcentration,
        bool isPrepared,
        bool isAlwaysPrepared,
        bool isCantrip = false,
        MagicSpellDisplayState displayState = MagicSpellDisplayState.Prepared)
    {
        Id = id;
        Name = name;
        Level = level;
        Source = source;
        School = school;
        IsRitual = isRitual;
        IsConcentration = isConcentration;
        IsPrepared = isPrepared;
        IsAlwaysPrepared = isAlwaysPrepared;
        IsCantrip = isCantrip;
        DisplayState = displayState;
    }
}

public enum MagicSpellDisplayState
{
    Known,
    Available,
    Prepared,
    AlwaysPrepared,
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
    string School,
    bool Ritual,
    bool Concentration,
    string CastingTime,
    string Range,
    string Components,
    string Duration,
    string Description)
{
    public MagicSpellDetailModel(
        string Id,
        string Name,
        string Source,
        int Level,
        string Subtitle,
        string CastingTime,
        string Range,
        string Components,
        string Duration,
        string Description)
        : this(Id, Name, Source, Level, Subtitle, string.Empty, false, false, CastingTime, Range, Components, Duration, Description)
    {
    }
}

public sealed record MagicPreparedChangeModel(string SpellId, bool Value, string SpellcastingSectionId, string SpellcastingClass);

public sealed record MagicSlotToggleModel(int Level, int SlotIndex);
