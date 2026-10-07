using System.Net.Http.Headers;
using System.Text.Json;
using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>Host adapter for desktop OAuth and native Android authorization.</summary>
public sealed class CloudCharacterService
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(90) };
#if ANDROID
    private GoogleDriveNativeAuthorization? _androidAuthorization;
#else
    private GoogleDriveAuthorization? _authorization;
#endif
    private GoogleDriveCharacterDocumentStore? _store;
    public string? AccountId { get; private set; }
    public string? AccountName { get; private set; }
    public bool IsConnected => AccountId is not null;
    public bool IsSupported => OperatingSystem.IsAndroid() || OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS();
    public bool UsesClientConfiguration => !OperatingSystem.IsAndroid();
    public bool IsConfigured => !UsesClientConfiguration || File.Exists(ConfigurationPath);
    private string ConfigurationPath => Path.Combine(FileSystem.Current.AppDataDirectory, "google-drive-client.json");
    public string WorkspaceRoot => Path.Combine(FileSystem.Current.AppDataDirectory, "Cloud Saves");

    public async Task ImportConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (!UsesClientConfiguration)
            throw new NotSupportedException("Android uses the app's registered package and signing certificate. Connect Google Drive directly.");
        cancellationToken.ThrowIfCancellationRequested();
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose Google desktop OAuth client JSON" });
        cancellationToken.ThrowIfCancellationRequested();
        if (picked is null) return;
        await using var stream = await picked.OpenReadAsync();
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var client = json.RootElement.GetProperty("installed");
        string? id = client.GetProperty("client_id").GetString();
        if (string.IsNullOrWhiteSpace(id) || !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new InvalidDataException("Choose a Google OAuth Desktop app client configuration.");
        var config = new GoogleDriveClientOptions(id,
            client.TryGetProperty("client_secret", out var secret) ? secret.GetString() : null);
        cancellationToken.ThrowIfCancellationRequested();
        Builder.Presentation.Utilities.CharacterFileIo.SaveTextFileAtomic(ConfigurationPath, JsonSerializer.Serialize(config));
#if !ANDROID
        _authorization = null;
#endif
        _store = null;
        AccountId = null;
        AccountName = null;
    }

    public async Task ConnectAsync(bool interactive, CancellationToken cancellationToken = default)
    {
        if (!IsSupported) throw new NotSupportedException("Google Drive sign-in is available on desktop and Android.");
#if ANDROID
        _androidAuthorization ??= new GoogleDriveNativeAuthorization(new SecureTokenStore(),
            AndroidGoogleDriveAuthorization.AuthorizeAsync,
            async (token, ct) =>
            {
                var account = await ReadAccountAsync(token, ct);
                return new GoogleDriveNativeAccount(account.Id, account.Email
                    ?? throw new InvalidOperationException("Google did not identify the Drive account email."));
            });
        var current = await _androidAuthorization.ConnectAsync(interactive, cancellationToken);
        _store ??= new GoogleDriveCharacterDocumentStore(_http, _androidAuthorization);
        AccountId = current.Id;
        AccountName = current.Email;
#else
        if (!IsConfigured) throw new InvalidOperationException("Google Drive sign-in needs the app's desktop client configuration.");
        if (_authorization is null)
        {
            var options = JsonSerializer.Deserialize<GoogleDriveClientOptions>(File.ReadAllText(ConfigurationPath))!;
            _authorization = new GoogleDriveAuthorization(_http, new SecureTokenStore(), options);
        }
        _store ??= new GoogleDriveCharacterDocumentStore(_http, _authorization);
        if (interactive)
        {
            string? expectedAccount = AccountId;
            await _authorization.SignInAsync(
                async uri =>
                {
                    if (!await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred))
                        throw new InvalidOperationException("Could not open your browser for Google sign-in. Please try again.");
                },
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
#endif
    }

    private async Task<(string Id, string? Name, string? Email)> ReadAccountAsync(string token, CancellationToken cancellationToken)
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
            user.TryGetProperty("emailAddress", out var email) ? email.GetString() : user.GetProperty("displayName").GetString(),
            user.TryGetProperty("emailAddress", out var address) ? address.GetString() : null);
    }

    public Task<IReadOnlyList<CharacterDocumentMetadata>> ListAsync(CancellationToken ct = default) =>
        (_store ?? throw new InvalidOperationException("Connect Google Drive first.")).ListAsync(ct);

    public Task<CloudCharacterSession> OpenAsync(CharacterDocumentReference reference, CancellationToken ct = default) =>
        CloudCharacterSession.OpenAsync(_store ?? throw new InvalidOperationException("Connect Google Drive first."),
            WorkspaceRoot, AccountId ?? throw new InvalidOperationException("Connect Google Drive first."), reference, ct);

    public Task<CharacterDocumentMetadata> UploadCharacterAsync(CharacterTab tab, CancellationToken ct = default) =>
        LocalCharacterCloudUpload.UploadAsync(
            _store ?? throw new InvalidOperationException("Connect Google Drive first."), tab, ct);

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
#if ANDROID
        if (_androidAuthorization is not null) await _androidAuthorization.DisconnectAsync();
#else
        if (_authorization is not null) await _authorization.DisconnectAsync();
#endif
        AccountId = null;
        AccountName = null;
        _store = null;
    }

    private sealed class SecureTokenStore : IGoogleDriveTokenStore
    {
        public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);
        public Task WriteAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
        public void Remove(string key) => SecureStorage.Default.Remove(key);
    }
}
