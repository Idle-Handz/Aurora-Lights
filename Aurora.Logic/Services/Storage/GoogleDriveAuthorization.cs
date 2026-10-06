using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Builder.Presentation.Services.Storage;

public interface IGoogleDriveTokenStore
{
    Task<string?> ReadAsync(string key);
    Task WriteAsync(string key, string value);
    void Remove(string key);
}

public sealed record GoogleDriveClientOptions(string ClientId, string? ClientSecret);

/// <summary>Desktop OAuth: system browser, loopback redirect, PKCE and state validation.</summary>
public sealed class GoogleDriveAuthorization(
    HttpClient http, IGoogleDriveTokenStore tokenStore, GoogleDriveClientOptions options)
    : IGoogleDriveAccessTokenProvider
{
    public const string Scope = "https://www.googleapis.com/auth/drive.file";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expires;
    private string TokenKey => "google-drive." + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(options.ClientId)));

    public async Task SignInAsync(Func<Uri, Task> openBrowser, CancellationToken cancellationToken,
        Func<string, CancellationToken, Task>? validateAccount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            string verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            string state = Base64Url(RandomNumberGenerator.GetBytes(32));
            // The OS chooses and holds the port; no find-free-port/close/rebind race.
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            string redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            string url = "https://accounts.google.com/o/oauth2/v2/auth?" + Query(new()
            {
                ["client_id"] = options.ClientId, ["redirect_uri"] = redirect,
                ["response_type"] = "code", ["scope"] = Scope, ["state"] = state,
                ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
                ["code_challenge_method"] = "S256", ["access_type"] = "offline", ["prompt"] = "consent select_account"
            });
            await openBrowser(new Uri(url));
            using var callback = await listener.AcceptTcpClientAsync(timeout.Token);
            string code = await ReceiveCallbackAsync(callback, redirect, state, timeout.Token);
            var values = ClientValues();
            values["code"] = code;
            values["code_verifier"] = verifier;
            values["redirect_uri"] = redirect;
            values["grant_type"] = "authorization_code";
            await ExchangeAsync(values, requireRefreshToken: true, timeout.Token, validateAccount);
        }
        finally { _gate.Release(); }
    }

    public static string ValidateCallback(Uri callback, string redirect, string expectedState)
    {
        var target = new Uri(redirect);
        if (callback.Scheme != target.Scheme || callback.Host != target.Host || callback.Port != target.Port
            || callback.AbsolutePath != target.AbsolutePath)
            throw new InvalidOperationException("Unexpected Google sign-in callback.");
        var values = System.Web.HttpUtility.ParseQueryString(callback.Query);
        if (values.GetValues("state")?.Length != 1
            || !string.Equals(values["state"], expectedState, StringComparison.Ordinal))
            throw new InvalidOperationException("Google sign-in could not be verified. Please try again.");
        if (values["error"] is not null)
            throw new InvalidOperationException("Google sign-in was cancelled or refused.");
        return values.GetValues("code")?.Length == 1 && !string.IsNullOrWhiteSpace(values["code"])
            ? values["code"]!
            : throw new InvalidOperationException("Google sign-in did not return an authorization code.");
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _expires > DateTimeOffset.UtcNow.AddMinutes(1))
                return _accessToken;
            string refresh = await tokenStore.ReadAsync(TokenKey)
                ?? throw new InvalidOperationException("Connect Google Drive in Cloud Saves first.");
            var values = ClientValues();
            values["refresh_token"] = refresh;
            values["grant_type"] = "refresh_token";
            await ExchangeAsync(values, requireRefreshToken: false, cancellationToken);
            return _accessToken!;
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try { tokenStore.Remove(TokenKey); _accessToken = null; _expires = default; }
        finally { _gate.Release(); }
    }

    private Dictionary<string, string> ClientValues()
    {
        var values = new Dictionary<string, string> { ["client_id"] = options.ClientId };
        if (!string.IsNullOrWhiteSpace(options.ClientSecret)) values["client_secret"] = options.ClientSecret;
        return values;
    }

    private async Task ExchangeAsync(Dictionary<string, string> values, bool requireRefreshToken, CancellationToken ct,
        Func<string, CancellationToken, Task>? validateAccount = null)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google authorization expired or was refused. Connect Google Drive again.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        string token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidDataException("Google returned an empty access token.");
        if (root.TryGetProperty("scope", out var scope)
            && !(scope.GetString() ?? "").Split(' ').Contains(Scope))
            throw new InvalidOperationException("Google Drive file access was not granted.");
        // Reject an unintended account switch before replacing a working credential.
        if (validateAccount is not null) await validateAccount(token, ct);
        if (root.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrWhiteSpace(refresh.GetString()))
            await tokenStore.WriteAsync(TokenKey, refresh.GetString()!);
        else if (requireRefreshToken)
            throw new InvalidOperationException("Google did not grant persistent access. Please connect again.");
        _accessToken = token;
        _expires = DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());
    }

    private static async Task<string> ReceiveCallbackAsync(TcpClient client, string redirect, string state, CancellationToken ct)
    {
        var stream = client.GetStream();
        // Only a single bounded GET request is accepted; no general-purpose web server or redirects.
        byte[] buffer = new byte[16 * 1024];
        int length = 0;
        while (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) < 0)
        {
            if (length == buffer.Length) throw new InvalidDataException("The Google sign-in callback was too large.");
            int count = await stream.ReadAsync(buffer.AsMemory(length), ct);
            if (count == 0) throw new IOException("The Google sign-in callback closed before completing.");
            length += count;
        }
        string[] request = Encoding.ASCII.GetString(buffer, 0, length).Split("\r\n")[0].Split(' ');
        string code;
        try
        {
            if (request.Length != 3 || request[0] != "GET" || !request[1].StartsWith('/')
                || request[1].StartsWith("//", StringComparison.Ordinal)
                || (request[2] != "HTTP/1.1" && request[2] != "HTTP/1.0"))
                throw new InvalidOperationException("Unexpected Google sign-in callback.");
            code = ValidateCallback(new Uri(new Uri(redirect), request[1]), redirect, state);
        }
        catch
        {
            await ReplyAsync(client, "400 Bad Request", "Google sign-in could not be verified. Return to Aurora and try again.", ct);
            throw;
        }
        await ReplyAsync(client, "200 OK", "Google sign-in received. You can close this window and return to Aurora.", ct);
        return code;
    }

    private static async Task ReplyAsync(TcpClient client, string status, string message, CancellationToken ct)
    {
        byte[] body = Encoding.UTF8.GetBytes(message);
        byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\n"
            + $"Content-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        var stream = client.GetStream();
        await stream.WriteAsync(headers, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
        client.Client.Shutdown(SocketShutdown.Send);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Query(Dictionary<string, string> values) =>
        string.Join("&", values.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
}
