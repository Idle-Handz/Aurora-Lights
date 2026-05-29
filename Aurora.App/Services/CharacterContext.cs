using Aurora.Logic;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;

namespace Aurora.App.Services;

/// <summary>
/// Guards the process-wide <see cref="CharacterManager.Current"/> singleton so that the active
/// MAUI tab (or web session) can deterministically mutate exactly its own character.
///
/// Every mutation entry point (Build rule apply, equipment toggle, text save, PDF generation,
/// snapshot rebuild after an edit) must wrap its work in <c>using (await CharacterContext.EnterAsync(tab))</c>.
/// Read-only rendering from a tab's cached <see cref="CharacterTab.Snapshot"/> does not need a context.
///
/// Concurrency is managed by a <see cref="SingletonGuard{T}"/>: entering the same tab twice
/// in a row costs a single lock acquire with no reload. If a swap fails mid-load, the guard
/// leaves active = null so the next enter triggers a full reload rather than assuming stale state.
/// </summary>
public static class CharacterContext
{
    private static readonly SingletonGuard<CharacterTab> _guard = new();

    /// <summary>
    /// Fires when in-memory character state could not be captured before a tab swap or load.
    /// Invoked on a background thread; marshal to the UI thread before touching Blazor components.
    /// </summary>
    public static event Action<string>? CaptureError;

    /// <summary>
    /// Subscribe to receive exception details from state-capture failures.
    /// Wire to your logging sink at startup, e.g.:
    /// <c>CharacterContext.ExceptionLogged += (ex, ctx) => debugLog.LogException(ex, ctx);</c>
    /// </summary>
    public static event Action<Exception, string>? ExceptionLogged;

    /// <summary>The tab currently loaded into the singleton, or null if nothing is active.</summary>
    public static CharacterTab? ActiveTab => _guard.Active;

    /// <summary>
    /// Take the context lock and ensure <paramref name="tab"/>'s character is the one loaded into
    /// <see cref="CharacterManager.Current"/>. Dispose the returned scope to release the lock.
    /// </summary>
    public static async Task<IDisposable> EnterAsync(CharacterTab tab)
    {
        if (tab == null) throw new ArgumentNullException(nameof(tab));
        return await _guard.EnterAsync(tab, SwapAsync);
    }

    /// <summary>
    /// Records <paramref name="tab"/> as the one currently loaded into the singleton without
    /// acquiring the lock. Call this immediately after a character load completes while the
    /// caller still owns the UI thread (e.g. directly after
    /// <see cref="CharacterService.LoadCharacterAsync"/> returns in Start.razor). This lets
    /// the next <see cref="EnterAsync"/> for the same tab skip the reload entirely.
    /// </summary>
    public static void ClaimAfterLoad(CharacterTab tab) => _guard.Claim(tab);

    /// <summary>
    /// Drops the active tab reference so the next <see cref="EnterAsync"/> on any tab will hydrate
    /// fresh. Call this when reloading element data.
    /// </summary>
    public static Task InvalidateAsync() => _guard.InvalidateAsync();

    /// <summary>
    /// Drops the active tab reference if it matches <paramref name="tab"/>. Call this when closing
    /// a tab so the closed object is not retained until the next swap.
    /// </summary>
    public static Task ReleaseAsync(CharacterTab tab) => _guard.ReleaseAsync(tab);

    /// <summary>
    /// Captures the active tab's current character state into its <see cref="CharacterTab.StateXml"/>
    /// buffer (so unsaved edits survive), then drops the active tab reference. Call this immediately
    /// before an external flow mutates <see cref="CharacterManager.Current"/> outside of EnterAsync.
    /// </summary>
    public static Task CaptureAndInvalidateAsync() =>
        _guard.CaptureAndInvalidateAsync(outgoing =>
        {
            if (outgoing != null)
                TryCaptureState(outgoing, "CharacterContext.CaptureAndInvalidateAsync");
        });

    /// <summary>
    /// Acquires the context lock, captures the active tab's state, drops the active tab reference,
    /// and returns a scope that releases the lock on dispose. Used by <see cref="CharacterService"/>
    /// to hold the lock for the duration of a full-file load.
    /// </summary>
    public static Task<IDisposable> EnterForLoadAsync() =>
        _guard.EnterForLoadAsync(outgoing =>
        {
            if (outgoing != null)
                TryCaptureState(outgoing, "CharacterContext.EnterForLoadAsync");
        });

    /// <summary>
    /// Attempts to serialize the active tab's live character state into its StateXml buffer.
    /// Fires <see cref="CaptureError"/> and <see cref="ExceptionLogged"/> if serialization fails.
    /// </summary>
    private static void TryCaptureState(CharacterTab tab, string callSite)
    {
        if (tab.Character == null) return;
        try
        {
            tab.StateXml = tab.File.SerializeCharacter(tab.Character);
        }
        catch (Exception ex)
        {
            ExceptionLogged?.Invoke(ex, callSite);
            var name = tab.File.DisplayName ?? "character";
            CaptureError?.Invoke(
                $"Could not preserve unsaved edits for \"{name}\". " +
                "Switching back to this tab will reload from the last saved file.");
        }
    }

    private static async Task SwapAsync(CharacterTab? outgoing, CharacterTab incoming)
    {
        // 1. Capture the outgoing tab's live state so unsaved edits are not lost on swap.
        //    (The guard has already nulled _active before calling us, so a throw here
        //     leaves the guard in the safe "nothing loaded" state.)
        if (outgoing != null)
            TryCaptureState(outgoing, "CharacterContext.SwapAsync");

        // 2. Reset ancillary singletons (prepared spells, expander registry) before hydrating.
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        // 3. Hydrate the incoming tab. Prefer the in-memory snapshot (unsaved edits); fall back
        //    to the on-disk file path for the first activation of a tab.
        if (incoming.StateXml is { Length: > 0 } bytes)
        {
            string originalPath = incoming.File.FilePath;
            var originalStamp = incoming.File.LastKnownDiskStamp;
            string temp = Path.Combine(
                Path.GetTempPath(),
                $"aurora_ctx_{Guid.NewGuid():N}.dnd5e");
            try
            {
                await File.WriteAllBytesAsync(temp, bytes);
                await incoming.File.Load(temp);
            }
            finally
            {
                incoming.File.FilePath = originalPath;
                incoming.File.RestoreKnownDiskStamp(originalStamp);
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }
        else
        {
            await incoming.File.Load();
        }

        // 4. Re-point the tab's cached Character at the freshly-loaded singleton character.
        incoming.Character = CharacterManager.Current.Character;

        // 5. Re-apply custom features (Extras-tab feats/spells/ASIs). They live outside the standard
        //    build, so file.Load doesn't restore them; without this their granted content (e.g. feat
        //    spells) silently disappears on tab swap. Reads the persisted <custom-features> list.
        BuildService.ReapplyCustomFeatures(incoming.File);
    }
}
