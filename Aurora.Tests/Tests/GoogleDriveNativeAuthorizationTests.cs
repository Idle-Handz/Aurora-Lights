using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveNativeAuthorizationTests
{
    private static readonly GoogleDriveNativeAccount First = new("drive-user-1", "first@example.com");
    private static readonly GoogleDriveNativeAccount Second = new("drive-user-2", "second@example.com");

    [Fact]
    public async Task Restart_RestoresAccountAndSilentlyRequestsFreshToken()
    {
        var store = new AccountStore();
        var auth = Create(store);
        (await auth.ConnectAsync(true)).Should().Be(First);
        store.Value.Should().NotContain("access-token");

        var requests = new List<(string?, bool)>();
        var restored = new GoogleDriveNativeAuthorization(store, (email, interactive, _) =>
        {
            requests.Add((email, interactive));
            return Task.FromResult("renewed-access-token");
        }, (_, _) => Task.FromResult(First));

        (await restored.ConnectAsync(false)).Should().Be(First);
        (await restored.GetAccessTokenAsync()).Should().Be("renewed-access-token");
        requests.Should().Equal((First.Email, false), (First.Email, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReconnectOrRenewal_RejectsDifferentAccountWithoutReplacingBinding(bool reconnect)
    {
        var store = new AccountStore();
        var actual = First;
        var auth = Create(store, () => actual);
        await auth.ConnectAsync(true);
        string? original = store.Value;
        actual = Second;

        Func<Task> action = reconnect
            ? async () => { await auth.ConnectAsync(true); }
            : async () => { await auth.GetAccessTokenAsync(); };
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Reconnect the same Google account*");
        store.Value.Should().Be(original);

        actual = First;
        (await auth.GetAccessTokenAsync()).Should().Be("access-token");
    }

    [Fact]
    public async Task Disconnect_BlocksSilentReconnectionAndAllowsExplicitAccountSwitch()
    {
        var store = new AccountStore();
        var actual = First;
        var auth = Create(store, () => actual);
        await auth.ConnectAsync(true);
        await auth.DisconnectAsync();
        store.Value.Should().BeNull();
        await auth.Invoking(a => a.ConnectAsync(false)).Should().ThrowAsync<InvalidOperationException>();
        Func<Task> token = async () => { await auth.GetAccessTokenAsync(); };
        await token.Should().ThrowAsync<InvalidOperationException>();
        actual = Second;
        (await auth.ConnectAsync(true)).Should().Be(Second);
    }

    [Fact]
    public async Task CancelledAccountValidation_DoesNotPersistConnection()
    {
        var store = new AccountStore();
        using var cancel = new CancellationTokenSource();
        var auth = new GoogleDriveNativeAuthorization(store,
            (_, _, _) => Task.FromResult("access-token"),
            (_, _) => { cancel.Cancel(); return Task.FromResult(First); });
        await auth.Invoking(a => a.ConnectAsync(true, cancel.Token)).Should().ThrowAsync<OperationCanceledException>();
        store.Value.Should().BeNull();
    }

    [Fact]
    public async Task RefusedPermission_DoesNotReplaceExistingAccount()
    {
        var store = new AccountStore();
        await Create(store).ConnectAsync(true);
        string? original = store.Value;
        bool? requestedInteractive = null;
        var auth = new GoogleDriveNativeAuthorization(store, (_, interactive, _) =>
        {
            requestedInteractive = interactive;
            throw new InvalidOperationException("Reconnect to grant permission.");
        }, (_, _) => Task.FromResult(First));
        Func<Task> token = async () => { await auth.GetAccessTokenAsync(); };
        await token.Should().ThrowAsync<InvalidOperationException>();
        requestedInteractive.Should().BeFalse();
        store.Value.Should().Be(original);
    }

    private static GoogleDriveNativeAuthorization Create(AccountStore store, Func<GoogleDriveNativeAccount>? account = null) =>
        new(store, (_, _, _) => Task.FromResult("access-token"), (_, _) => Task.FromResult(account?.Invoke() ?? First));

    private sealed class AccountStore : IGoogleDriveTokenStore
    {
        public string? Value { get; private set; }
        public Task<string?> ReadAsync(string key) => Task.FromResult(Value);
        public Task WriteAsync(string key, string value) { Value = value; return Task.CompletedTask; }
        public void Remove(string key) => Value = null;
    }
}
