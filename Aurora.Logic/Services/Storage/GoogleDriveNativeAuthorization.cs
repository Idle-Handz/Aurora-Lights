using System.Text.Json;

namespace Builder.Presentation.Services.Storage;

public sealed record GoogleDriveNativeAccount(string Id, string Email);

/// <summary>
/// Binds a native token broker to a verified Drive account. Only account identity is persisted;
/// Google Play services owns token caching and renewal. Every renewed token is checked before use.
/// </summary>
public sealed class GoogleDriveNativeAuthorization(
    IGoogleDriveTokenStore accountStore,
    Func<string?, bool, CancellationToken, Task<string>> authorize,
    Func<string, CancellationToken, Task<GoogleDriveNativeAccount>> readAccount)
    : IGoogleDriveAccessTokenProvider
{
    private const string AccountKey = "google-drive.android.account";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GoogleDriveNativeAccount? _account;
    private bool _loaded;

    public async Task<GoogleDriveNativeAccount> ConnectAsync(bool interactive, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAccountAsync();
            if (!interactive && _account is null)
                throw new InvalidOperationException("Connect Google Drive in Cloud Saves first.");
            var (_, account) = await AuthorizeAsync(interactive, ct);
            ct.ThrowIfCancellationRequested();
            await accountStore.WriteAsync(AccountKey, JsonSerializer.Serialize(account));
            _account = account;
            return account;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadAccountAsync();
            if (_account is null)
                throw new InvalidOperationException("Connect Google Drive in Cloud Saves first.");
            var (token, _) = await AuthorizeAsync(interactive: false, cancellationToken);
            return token;
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            accountStore.Remove(AccountKey);
            _account = null;
            _loaded = true;
        }
        finally { _gate.Release(); }
    }

    private async Task LoadAccountAsync()
    {
        if (_loaded) return;
        string? saved = await accountStore.ReadAsync(AccountKey);
        _account = saved is null ? null : JsonSerializer.Deserialize<GoogleDriveNativeAccount>(saved);
        _loaded = true;
    }

    private async Task<(string Token, GoogleDriveNativeAccount Account)> AuthorizeAsync(bool interactive, CancellationToken ct)
    {
        string token = await authorize(_account?.Email, interactive, ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Google Drive file access was not granted. Reconnect Google Drive.");
        var account = await readAccount(token, ct);
        if (string.IsNullOrWhiteSpace(account.Id) || string.IsNullOrWhiteSpace(account.Email))
            throw new InvalidOperationException("Google did not identify the Drive account.");
        if (_account is not null && !string.Equals(_account.Id, account.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Reconnect the same Google account. To switch accounts, close cloud characters and disconnect first.");
        ct.ThrowIfCancellationRequested();
        return (token, account);
    }
}
