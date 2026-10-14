using Aurora.Components.Models;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>
/// Represents a single open character tab. Holds the character file metadata,
/// the loaded Character object (cached so switching tabs doesn't reload),
/// and a dirty flag for unsaved in-memory edits.
/// </summary>
public sealed class CharacterTab
{
    public Guid Id { get; } = Guid.NewGuid();
    public CharacterFile File { get; }
    public Character? Character { get; set; }
    private bool _isDirty;
    internal SessionWriteState SessionWrites { get; } = new();
    public long EditVersion { get; private set; }
    public long CloudSavedEditVersion { get; set; }
    public long CloudReloadVersion { get; set; }
    public CloudCharacterSession? CloudSession { get; set; }
    public string? CloudNotice { get; set; }
    /// <summary>A failed hydration must be retried before the previous model can be saved.</summary>
    public bool CloudReloadFailed { get; set; }
    private bool _isSaving;
    internal object SaveRequestGate { get; } = new();
    internal Task<string?>? InFlightSave { get; set; }
    internal long InFlightSaveEditVersion { get; set; }
    public event Action? SaveStateChanged;
    public bool IsSaving
    {
        get => _isSaving;
        set { _isSaving = value; SaveStateChanged?.Invoke(); }
    }
    public bool IsDirty
    {
        get => _isDirty || SessionWrites.HasPendingChanges || CloudSession?.HasPendingChanges == true;
        set
        {
            if (value) EditVersion++;
            _isDirty = value || (CloudSession is not null && CloudSavedEditVersion < EditVersion);
        }
    }

    /// <summary>
    /// Snapshot of class progression data captured at load time.
    /// Stored separately because CharacterManager.Current is a singleton
    /// and gets overwritten when subsequent characters are loaded.
    /// </summary>
    public CharacterSnapshot? Snapshot { get; set; }
    public IReadOnlyList<ClassProgressionSnapshot>? ProgressionSnapshots { get; set; }

    public string DisplayName => File.DisplayName ?? "Character";

    /// <summary>Session state (HP, spell slots, custom resources, etc.) for this character.</summary>
    public SessionState Session { get; set; } = new();

    /// <summary>Magic filters and selected view for the lifetime of this open character.</summary>
    public MagicWorkspaceState MagicWorkspace { get; } = new();

    /// <summary>
    /// True while the character is loading in the background (eager-navigation mode).
    /// Build.razor shows a loading indicator instead of "no character" when this is set.
    /// Cleared by Start.razor once LoadCharacterAsync completes.
    /// </summary>
    public bool IsLoading { get; set; }

    /// <summary>
    /// True when this character was just created (not loaded from an existing file).
    /// Used by Build.razor to show the guided creation banner.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// In-memory XML representation of this tab's latest state. Captured by
    /// <see cref="CharacterContext"/> before swapping the CharacterManager singleton to a different
    /// tab. Null until first captured; CharacterContext treats null as "not yet captured, load from
    /// disk" and non-null as "use this serialised state". Never assign an empty byte[] — the check
    /// <c>StateXml is { Length: &gt; 0 }</c> treats empty the same as null, but the intent is lost.
    /// </summary>
    public byte[]? StateXml { get; set; }

    /// <summary>
    /// Serializes concurrent writes to this character's XML file so a background full save
    /// (SaveCharacterFile) and UI-triggered patch saves (SaveCurrency, SaveNotes, etc.)
    /// cannot interleave their read-modify-write cycles.
    ///
    /// Use <see cref="CharacterFileWriteCoordinator"/> for app-facing writes so stale-file
    /// detection and failure reporting stay consistent.
    /// </summary>
    public SemaphoreSlim FileSaveSemaphore { get; } = new(1, 1);

    public CharacterTab(CharacterFile file) => File = file;
}

/// <summary>Tracks session writes independently of other unsaved character edits.</summary>
internal sealed class SessionWriteState
{
    private long _version;
    private long _savedVersion;
    private SessionCharacterEdits? _savedEdits;
    public long Version => Interlocked.Read(ref _version);
    public bool HasPendingChanges => Version > Interlocked.Read(ref _savedVersion);
    public long BeginChange() => Interlocked.Increment(ref _version);
    public bool IsAlreadySaved(long version) => version <= Interlocked.Read(ref _savedVersion);
    public void Initialize(CharacterSnapshot snapshot) => _savedEdits ??= SessionCharacterEdits.From(snapshot);
    public void InitializeFromFile(CharacterFile file)
    {
        if (_savedEdits is not null) return;
        var document = Builder.Presentation.Utilities.CharacterFileIo.LoadXmlDocument(file.FilePath);
        string Read(string path) => document.SelectSingleNode("/character/build/input/" + path)?.InnerText ?? string.Empty;
        long Coin(string name) => long.TryParse(Read("currency/" + name), out long value) ? value : 0;
        _savedEdits = new SessionCharacterEdits(
            (Coin("copper"), Coin("silver"), Coin("electrum"), Coin("gold"), Coin("platinum")),
            (Read("notes/note[@column='left']"), Read("notes/note[@column='right']")));
    }
    public bool NeedsCurrencyPatch(SessionCharacterEdits edits) => _savedEdits?.Currency != edits.Currency;
    public bool NeedsNotesPatch(SessionCharacterEdits edits) => _savedEdits?.Notes != edits.Notes;
    public void Saved(long version, SessionCharacterEdits edits)
    {
        if (version < Interlocked.Read(ref _savedVersion)) return;
        _savedEdits = edits;
        Interlocked.Exchange(ref _savedVersion, version);
    }
}

internal sealed record SessionCharacterEdits(
    (long Copper, long Silver, long Electrum, long Gold, long Platinum) Currency,
    (string Notes1, string Notes2) Notes)
{
    public static SessionCharacterEdits From(CharacterSnapshot snapshot) => new(
        (snapshot.CoinCopper, snapshot.CoinSilver, snapshot.CoinElectrum, snapshot.CoinGold, snapshot.CoinPlatinum),
        (snapshot.Notes1, snapshot.Notes2));

    public CharacterSnapshot ToSnapshot() => new()
    {
        CoinCopper = Currency.Copper, CoinSilver = Currency.Silver, CoinElectrum = Currency.Electrum,
        CoinGold = Currency.Gold, CoinPlatinum = Currency.Platinum,
        Notes1 = Notes.Notes1, Notes2 = Notes.Notes2,
    };
}

/// <summary>
/// Immutable snapshot of a single class's progression data for display.
/// </summary>
public sealed record ClassProgressionSnapshot(
    string ClassName,
    string HitDie,
    int Level,
    bool IsMainClass,
    IReadOnlyList<FeatureEntry> Features
);
