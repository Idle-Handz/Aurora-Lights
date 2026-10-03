using System.Net.Http.Headers;
using System.Text.Json;
using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>Desktop host adapter. Tokens live in the OS credential store, never character files.</summary>
public sealed class CloudCharacterService
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(90) };
    private GoogleDriveAuthorization? _authorization;
    private GoogleDriveCharacterDocumentStore? _store;
    public string? AccountId { get; private set; }
    public string? AccountName { get; private set; }
    public bool IsConnected => AccountId is not null;
    public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS();
    public bool IsConfigured => File.Exists(ConfigurationPath);
    private string ConfigurationPath => Path.Combine(FileSystem.Current.AppDataDirectory, "google-drive-client.json");
    public string WorkspaceRoot => Path.Combine(FileSystem.Current.AppDataDirectory, "Cloud Saves");

    public async Task ImportConfigurationAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose Google desktop OAuth client JSON" });
        if (picked is null) return;
        await using var stream = await picked.OpenReadAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var client = json.RootElement.GetProperty("installed");
        string? id = client.GetProperty("client_id").GetString();
        if (string.IsNullOrWhiteSpace(id) || !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new InvalidDataException("Choose a Google OAuth Desktop app client configuration.");
        var config = new GoogleDriveClientOptions(id,
            client.TryGetProperty("client_secret", out var secret) ? secret.GetString() : null);
        Builder.Presentation.Utilities.CharacterFileIo.SaveTextFileAtomic(ConfigurationPath, JsonSerializer.Serialize(config));
        _authorization = null;
        _store = null;
        AccountId = null;
        AccountName = null;
    }

    public async Task ConnectAsync(bool interactive, CancellationToken cancellationToken = default)
    {
        if (!IsSupported) throw new NotSupportedException("Google Drive sign-in is currently available on desktop.");
        if (!IsConfigured) throw new InvalidOperationException("Google Drive sign-in needs the app's desktop client configuration.");
        if (_authorization is null)
        {
            var options = JsonSerializer.Deserialize<GoogleDriveClientOptions>(File.ReadAllText(ConfigurationPath))!;
            _authorization = new GoogleDriveAuthorization(_http, new SecureTokenStore(), options);
            _store = new GoogleDriveCharacterDocumentStore(_http, _authorization);
        }
        if (interactive)
        {
            string? expectedAccount = AccountId;
            await _authorization.SignInAsync(
                async uri => { await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred); },
                cancellationToken,
                async (accessToken, ct) =>
                {
                    var account = await ReadAccountAsync(accessToken, ct);
                    if (expectedAccount is not null && expectedAccount != account.Id)
                        throw new InvalidOperationException("Reconnect the same Google account. To switch accounts, close cloud characters and disconnect first.");
                });
        }
        string token = await _authorization.GetAccessTokenAsync(cancellationToken);
        var current = await ReadAccountAsync(token, cancellationToken);
        AccountId = current.Id;
        AccountName = current.Name;
    }

    private async Task<(string Id, string? Name)> ReadAccountAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/about?fields=user(permissionId,emailAddress,displayName)");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var user = json.RootElement.GetProperty("user");
        return (
            user.GetProperty("permissionId").GetString() ?? throw new InvalidDataException("Google did not identify the account."),
            user.TryGetProperty("emailAddress", out var email) ? email.GetString() : user.GetProperty("displayName").GetString());
    }

    public Task<IReadOnlyList<CharacterDocumentMetadata>> ListAsync(CancellationToken ct = default) =>
        (_store ?? throw new InvalidOperationException("Connect Google Drive first.")).ListAsync(ct);

    public Task<CloudCharacterSession> OpenAsync(CharacterDocumentReference reference, CancellationToken ct = default) =>
        CloudCharacterSession.OpenAsync(_store ?? throw new InvalidOperationException("Connect Google Drive first."),
            WorkspaceRoot, AccountId ?? throw new InvalidOperationException("Connect Google Drive first."), reference, ct);

    public async Task<CharacterDocumentMetadata?> UploadFileAsync(CancellationToken ct = default)
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Upload an Aurora character to Google Drive" });
        if (picked is null) return null;
        await using var input = await picked.OpenReadAsync();
        using var buffer = new MemoryStream();
        byte[] block = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + count > GoogleDriveCharacterDocumentStore.MaximumCharacterBytes)
                throw new InvalidDataException("This character is too large to upload (maximum 32 MB).");
            buffer.Write(block, 0, count);
        }
        byte[] content = buffer.ToArray();
        CloudCharacterSession.ValidateCharacter(content);
        // Include saved session state when the picker exposes an ordinary local path.
        string? sidecar = string.IsNullOrEmpty(picked.FullPath) ? null : SessionStore.GetSidecarPath(picked.FullPath);
        if (sidecar is not null && File.Exists(sidecar))
            content = CloudCharacterSession.Pack(content, File.ReadAllBytes(sidecar));
        return await (_store ?? throw new InvalidOperationException("Connect Google Drive first."))
            .CreateAsync(picked.FileName, content, cancellationToken: ct);
    }

    public async Task DisconnectAsync()
    {
        if (_authorization is not null) await _authorization.DisconnectAsync();
        AccountId = null;
        AccountName = null;
    }

    private sealed class SecureTokenStore : IGoogleDriveTokenStore
    {
        public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);
        public Task WriteAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
        public void Remove(string key) => SecureStorage.Default.Remove(key);
    }
}
