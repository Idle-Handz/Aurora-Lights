using System.Text.Json.Serialization;
using Builder.Presentation.Utilities;

namespace Aurora.Reflections.Services;

public enum ResetOn { LongRest, ShortRest, Manual }

/// <summary>
/// A single user-defined trackable resource (Bardic Inspiration, Ki Points,
/// Lay on Hands pool, Action Surge, etc.).
/// </summary>
public sealed class CustomResource
{
    public string Id      { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name    { get; set; } = "";
    public int    Max     { get; set; } = 1;
    public int    Used    { get; set; } = 0;
    public ResetOn ResetOn { get; set; } = ResetOn.LongRest;

    [JsonIgnore]
    public int Remaining => Math.Max(0, Max - Used);
    /// <summary>True when Max is large enough that pips would be unwieldy; show numeric instead.</summary>
    [JsonIgnore]
    public bool UseNumeric => Max > 10;
}

public sealed class CustomAttackReminder
{
    public string Id     { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name   { get; set; } = "";
    public string Attack { get; set; } = "";
    public string Damage { get; set; } = "";
    public string Range  { get; set; } = "";
}

/// <summary>
/// Device-local session state persisted by SessionStore in a JSON sidecar.
/// </summary>
public sealed class SessionState
{
    // ── HP ────────────────────────────────────────────────────────────────────
    /// <summary>-1 means "not yet initialised"; Session.razor calls InitializeIfNew(maxHp).</summary>
    public int CurrentHp           { get; set; } = -1;
    public int TempHp              { get; set; }

    // ── Death saves ───────────────────────────────────────────────────────────
    public int DeathSaveSuccesses  { get; set; }
    public int DeathSaveFailures   { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public bool         Inspiration { get; set; }
    public int          Exhaustion  { get; set; }          // 0–6
    public List<string> Conditions  { get; set; } = [];

    // ── Spell slots ───────────────────────────────────────────────────────────
    /// <summary>Regular spell slot levels 1-9 → number USED. Retained for sidecar compatibility.</summary>
    public Dictionary<int, int> SpellSlotsUsed { get; set; } = [];
    public Dictionary<int, int> PactSpellSlotsUsed { get; set; } = [];
    public bool SpellSlotPoolsMigrated { get; set; }

    /// <summary>Old single-pool usage belongs to Pact Magic only for a Pact-only character.
    /// For mixed casters, retain it as regular usage; the old format cannot identify its origin.</summary>
    public void EnsureSpellSlotPools(IReadOnlyList<SpellSlotPool> pools)
    {
        if (SpellSlotPoolsMigrated || pools.Count == 0) return;
        if (pools.All(pool => pool.RecoversOnShortRest))
        {
            foreach (var slot in SpellSlotsUsed)
                PactSpellSlotsUsed.TryAdd(slot.Key, slot.Value);
            SpellSlotsUsed.Clear();
        }
        SpellSlotPoolsMigrated = true;
    }

    private Dictionary<int, int> SlotUsage(string poolId) => poolId switch
    {
        SpellSlotPoolResolver.SpellcastingId => SpellSlotsUsed,
        SpellSlotPoolResolver.PactMagicId => PactSpellSlotsUsed,
        _ => throw new ArgumentOutOfRangeException(nameof(poolId))
    };

    public int UsedSlots(string poolId, int level, int total) =>
        Math.Clamp(SlotUsage(poolId).GetValueOrDefault(level), 0, Math.Max(0, total));

    public void ToggleSlot(string poolId, int level, int index, int total)
    {
        if (index < 1 || index > total) return;
        var used = UsedSlots(poolId, level, total);
        SlotUsage(poolId)[level] = used == index ? index - 1 : index;
    }

    // ── Custom resources ──────────────────────────────────────────────────────
    public List<CustomResource> CustomResources { get; set; } = [];

    // Attack reminders shown on the Session page. Default non-weapon attacks
    // (unarmed strikes, racial/natural attacks, etc.) are shown unless hidden;
    // weapon attacks are opt-in and constrained to weapons still in inventory.
    public List<string> AttackReminderWeaponIds { get; set; } = [];
    public List<string> HiddenDefaultAttackReminderKeys { get; set; } = [];
    public List<CustomAttackReminder> CustomAttackReminders { get; set; } = [];

    // ── Helpers ───────────────────────────────────────────────────────────────

    public void InitializeIfNew(int maxHp)
    {
        SetCurrentHp(CurrentHp < 0 ? maxHp : CurrentHp, maxHp);
    }

    public void SetCurrentHp(int value, int maxHp) => CurrentHp = Math.Clamp(value, 0, Math.Max(0, maxHp));

    public void AdjustCurrentHp(int delta, int maxHp) =>
        CurrentHp = (int)Math.Clamp((long)CurrentHp + delta, 0, Math.Max(0, maxHp));

    public void SetTemporaryHp(int value) => TempHp = Math.Max(0, value);

    public void AdjustTemporaryHp(int delta) =>
        TempHp = (int)Math.Clamp((long)TempHp + delta, 0, int.MaxValue);

    public void ShortRest()
    {
        PactSpellSlotsUsed.Clear();
        DeathSaveSuccesses = 0;
        DeathSaveFailures  = 0;
        foreach (var r in CustomResources.Where(r => r.ResetOn == ResetOn.ShortRest))
            r.Used = 0;
    }

    public void LongRest(int maxHp)
    {
        CurrentHp          = maxHp;
        TempHp             = 0;
        DeathSaveSuccesses = 0;
        DeathSaveFailures  = 0;
        SpellSlotsUsed.Clear();
        PactSpellSlotsUsed.Clear();
        if (Exhaustion > 0) Exhaustion--;
        foreach (var r in CustomResources.Where(r => r.ResetOn is ResetOn.LongRest or ResetOn.ShortRest))
            r.Used = 0;
    }
}
