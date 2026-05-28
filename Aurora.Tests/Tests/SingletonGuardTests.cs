using Aurora.Logic;

namespace Aurora.Tests.Tests;

/// <summary>
/// Unit tests for <see cref="SingletonGuard{T}"/> — the concurrency guard that backs
/// CharacterContext. These run without any content database.
///
/// They verify the exact behaviours fixed in the tab-swap bug:
///   • After a failed swap, Active must be null (not pointing at the old tab).
///   • ReleaseAsync only clears Active when it matches the given item.
///   • EnterAsync skips the swap callback when the same item is already active.
/// </summary>
public sealed class SingletonGuardTests
{
    // ── Claim / Invalidate ────────────────────────────────────────────────────

    [Fact]
    public void Claim_SetsActive()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();

        guard.Claim(item);

        guard.Active.Should().BeSameAs(item);
    }

    [Fact]
    public async Task InvalidateAsync_ClearsActive()
    {
        var guard = new SingletonGuard<object>();
        guard.Claim(new object());

        await guard.InvalidateAsync();

        guard.Active.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_WhenAlreadyNull_DoesNotThrow()
    {
        var guard = new SingletonGuard<object>();

        var act = async () => await guard.InvalidateAsync();

        await act.Should().NotThrowAsync();
        guard.Active.Should().BeNull();
    }

    // ── ReleaseAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseAsync_ClearsActive_WhenItemMatches()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        await guard.ReleaseAsync(item);

        guard.Active.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_KeepsActive_WhenDifferentItem()
    {
        var guard = new SingletonGuard<object>();
        var item1 = new object();
        var item2 = new object();
        guard.Claim(item1);

        await guard.ReleaseAsync(item2);

        guard.Active.Should().BeSameAs(item1,
            because: "releasing a non-active item must not affect the active reference");
    }

    [Fact]
    public async Task ReleaseAsync_WhenActiveIsNull_DoesNotThrow()
    {
        var guard = new SingletonGuard<object>();

        var act = async () => await guard.ReleaseAsync(new object());

        await act.Should().NotThrowAsync();
        guard.Active.Should().BeNull();
    }

    // ── EnterAsync: same item skips swap ──────────────────────────────────────

    [Fact]
    public async Task EnterAsync_SameItem_SkipsSwapCallback()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        int callCount = 0;
        using (await guard.EnterAsync(item, (_, _) => { callCount++; return Task.CompletedTask; }))
        { }

        callCount.Should().Be(0,
            because: "entering the already-active item must not invoke the swap callback");
    }

    [Fact]
    public async Task EnterAsync_SameItem_ActiveRemainsSet()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        using (await guard.EnterAsync(item, (_, _) => Task.CompletedTask))
        { }

        guard.Active.Should().BeSameAs(item);
    }

    // ── EnterAsync: successful swap ───────────────────────────────────────────

    [Fact]
    public async Task EnterAsync_DifferentItem_SetsActiveAfterSuccessfulSwap()
    {
        var guard    = new SingletonGuard<object>();
        var old      = new object();
        var incoming = new object();
        guard.Claim(old);

        using (await guard.EnterAsync(incoming, (_, _) => Task.CompletedTask))
        { }

        guard.Active.Should().BeSameAs(incoming);
    }

    [Fact]
    public async Task EnterAsync_PassesOutgoingToCallback()
    {
        var guard    = new SingletonGuard<object>();
        var old      = new object();
        var incoming = new object();
        guard.Claim(old);

        object? capturedOutgoing = null;
        using (await guard.EnterAsync(incoming,
            (outgoing, _) => { capturedOutgoing = outgoing; return Task.CompletedTask; }))
        { }

        capturedOutgoing.Should().BeSameAs(old,
            because: "the callback must receive the previously-active item as the outgoing argument");
    }

    [Fact]
    public async Task EnterAsync_FromNull_PassesNullOutgoing()
    {
        var guard    = new SingletonGuard<object>();
        var incoming = new object();

        object? capturedOutgoing = "sentinel";
        using (await guard.EnterAsync(incoming,
            (outgoing, _) => { capturedOutgoing = outgoing; return Task.CompletedTask; }))
        { }

        capturedOutgoing.Should().BeNull(
            because: "when nothing is active the outgoing argument must be null");
    }

    // ── EnterAsync: failed swap leaves Active = null (the core bug fix) ───────

    [Fact]
    public async Task EnterAsync_WhenSwapThrows_ActiveIsNull()
    {
        var guard    = new SingletonGuard<object>();
        var old      = new object();
        var incoming = new object();
        guard.Claim(old);

        var act = async () =>
            await guard.EnterAsync(incoming,
                (_, _) => throw new InvalidOperationException("simulated load failure"));

        await act.Should().ThrowAsync<InvalidOperationException>();

        guard.Active.Should().BeNull(
            because: "a failed swap must leave Active = null, not pointing at the outgoing tab " +
                     "whose singleton state has been torn down");
    }

    [Fact]
    public async Task EnterAsync_AfterFailedSwap_NextEnterReloads()
    {
        var guard    = new SingletonGuard<object>();
        var old      = new object();
        var incoming = new object();
        guard.Claim(old);

        // First enter: throws.
        try { await guard.EnterAsync(incoming, (_, _) => throw new Exception()); } catch { }

        // Second enter for same incoming: active is null, so the swap callback must fire again.
        int secondSwapCount = 0;
        using (await guard.EnterAsync(incoming,
            (_, _) => { secondSwapCount++; return Task.CompletedTask; }))
        { }

        secondSwapCount.Should().Be(1,
            because: "after a failed swap active is null, so the next enter must redo the load");
    }

    // ── Scope / lock release ──────────────────────────────────────────────────

    [Fact]
    public async Task ScopeDispose_ReleasesLock_AllowingSubsequentEnter()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        var scope = await guard.EnterAsync(item, (_, _) => Task.CompletedTask);
        scope.Dispose();

        // If the lock was NOT released this would deadlock.
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var completed = false;
        await Task.Run(async () =>
        {
            using (await guard.EnterAsync(item, (_, _) => Task.CompletedTask))
                completed = true;
        }, cts.Token);

        completed.Should().BeTrue("the lock must be released by Dispose so a subsequent enter can proceed");
    }

    // ── Non-reentrancy (portrait-picker deadlock) ─────────────────────────────
    //
    // Root cause: OpenPortraitPickerAsync held a CharacterContext scope and then
    // called SaveTabAsync, which tries to acquire the same SingletonGuard lock.
    // Since the guard is non-reentrant the inner WaitAsync never completes,
    // silently hanging the save and leaving _portraitSrc unchanged.
    // Fix: release the outer scope before calling any code that re-enters the guard.

    [Fact]
    public async Task EnterAsync_WhileScopeHeld_DoesNotComplete()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        var outer      = await guard.EnterAsync(item, (_, _) => Task.CompletedTask);
        var innerEnter = guard.EnterAsync(item, (_, _) => Task.CompletedTask);
        var winner     = await Task.WhenAny(innerEnter, Task.Delay(200));

        // Clean up: release outer so the pending inner can finish without leaking.
        outer.Dispose();
        (await innerEnter).Dispose();

        winner.Should().NotBeSameAs(innerEnter,
            because: "SingletonGuard is non-reentrant — a second EnterAsync while a scope " +
                     "is held must not complete (this was the portrait-picker deadlock)");
    }

    [Fact]
    public async Task EnterAsync_SequentialEnterAfterRelease_Completes()
    {
        // The fix pattern: dispose the outer scope before calling code that re-enters
        // (e.g. the inner scope representing SaveTabAsync in CharacterContext).
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        { using var outer = await guard.EnterAsync(item, (_, _) => Task.CompletedTask); }

        var innerEnter = guard.EnterAsync(item, (_, _) => Task.CompletedTask);
        var winner     = await Task.WhenAny(innerEnter, Task.Delay(200));
        (await innerEnter).Dispose();

        winner.Should().BeSameAs(innerEnter,
            because: "once the outer scope is released the guard is free and the next enter completes");
    }

    [Fact]
    public async Task EnterAsync_ReentrantWhileHeld_ThrowsDescriptiveTimeout()
    {
        // The guard is non-reentrant, so a reentrant acquire used to hang forever. It now times out
        // with a clear error. Use a short timeout for the test and restore it afterward so the static
        // setting can't leak into the other (default-timeout) tests in this class.
        var original = SingletonGuard<object>.AcquireTimeout;
        SingletonGuard<object>.AcquireTimeout = TimeSpan.FromMilliseconds(150);
        try
        {
            var guard = new SingletonGuard<object>();
            var item  = new object();
            guard.Claim(item);

            using var outer = await guard.EnterAsync(item, (_, _) => Task.CompletedTask);

            var reenter = async () =>
            {
                using (await guard.EnterAsync(item, (_, _) => Task.CompletedTask)) { }
            };

            (await reenter.Should().ThrowAsync<InvalidOperationException>(
                    "a reentrant acquire can never succeed and must surface as an error, not a hang"))
                .Which.Message.Should().Contain("re-entered");
        }
        finally
        {
            SingletonGuard<object>.AcquireTimeout = original;
        }
    }

    // ── CaptureAndInvalidateAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CaptureAndInvalidateAsync_CallsCallbackWithCurrentActive_ThenClearsIt()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        object? captured = null;
        await guard.CaptureAndInvalidateAsync(current => captured = current);

        captured.Should().BeSameAs(item,
            because: "the callback must receive the active item before it is cleared");
        guard.Active.Should().BeNull();
    }

    [Fact]
    public async Task CaptureAndInvalidateAsync_WithNoCallback_JustClearsActive()
    {
        var guard = new SingletonGuard<object>();
        guard.Claim(new object());

        await guard.CaptureAndInvalidateAsync();

        guard.Active.Should().BeNull();
    }

    // ── EnterForLoadAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task EnterForLoadAsync_ClearsActiveAndHoldsLock()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        using var scope = await guard.EnterForLoadAsync();

        guard.Active.Should().BeNull(
            because: "EnterForLoadAsync clears active so the caller can load a new item");
    }

    [Fact]
    public async Task EnterForLoadAsync_CallbackReceivesOutgoing()
    {
        var guard = new SingletonGuard<object>();
        var item  = new object();
        guard.Claim(item);

        object? captured = null;
        using var scope = await guard.EnterForLoadAsync(current => captured = current);

        captured.Should().BeSameAs(item);
    }

    [Fact]
    public async Task EnterForLoadAsync_ScopeDispose_AllowsSubsequentEnter()
    {
        var guard    = new SingletonGuard<object>();
        var incoming = new object();

        var scope = await guard.EnterForLoadAsync();
        scope.Dispose();

        // Claim the new item (simulating what CharacterService does after load).
        guard.Claim(incoming);

        // Now a page calling EnterAsync for the same tab should not deadlock.
        using (await guard.EnterAsync(incoming, (_, _) => Task.CompletedTask))
        { }

        guard.Active.Should().BeSameAs(incoming);
    }
}
