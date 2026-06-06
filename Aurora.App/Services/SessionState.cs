namespace Aurora.App.Services;

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

    public int Remaining => Math.Max(0, Max - Used);
    /// <summary>True when Max is large enough that pips would be unwieldy; show numeric instead.</summary>
    public bool UseNumeric => Max > 10;
}

/// <summary>
/// Session state persisted in the character XML under a &lt;session&gt; node.
/// WPF Aurora Builder ignores unknown nodes, so this coexists safely with
/// the existing &lt;build&gt; data.
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
    /// <summary>Spell slot levels 1-9 → number of slots USED (max comes from snapshot).</summary>
    public Dictionary<int, int> SpellSlotsUsed { get; set; } = [];

    // ── Custom resources ──────────────────────────────────────────────────────
    public List<CustomResource> CustomResources { get; set; } = [];

    // Attack reminders shown on the Session page. Default non-weapon attacks
    // (unarmed strikes, racial/natural attacks, etc.) are shown unless hidden;
    // weapon attacks are opt-in and constrained to currently on-hand weapons.
    public List<string> AttackReminderWeaponIds { get; set; } = [];
    public List<string> HiddenDefaultAttackReminderKeys { get; set; } = [];

    // ── Helpers ───────────────────────────────────────────────────────────────

    public void InitializeIfNew(int maxHp)
    {
        if (CurrentHp < 0) CurrentHp = maxHp;
    }

    public void ShortRest()
    {
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
        if (Exhaustion > 0) Exhaustion--;
        foreach (var r in CustomResources.Where(r => r.ResetOn is ResetOn.LongRest or ResetOn.ShortRest))
            r.Used = 0;
    }
}
