using System.Reflection;
using System.Text.Json;
using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>Keep an existing installation's client; otherwise use the release's embedded client.</summary>
public sealed class GoogleDriveClientConfiguration(string overridePath, Assembly bundleAssembly)
{
    public const string ResourceName = "Aurora.GoogleDriveClient.json";
    private const string InvalidConfiguration = "Choose a Google OAuth Desktop app client configuration.";

    public bool HasOverride => File.Exists(overridePath);
    public bool HasBundledConfiguration => bundleAssembly.GetManifestResourceInfo(ResourceName) is not null;
    public bool IsConfigured => HasOverride || HasBundledConfiguration;

    public GoogleDriveClientOptions Read()
    {
        // Never silently change the Google project used by an existing installation.
        using Stream? stream = HasOverride
            ? File.OpenRead(overridePath)
            : bundleAssembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
            throw new InvalidOperationException("Google Drive sign-in needs the app's desktop client configuration.");
        try
        {
            var options = JsonSerializer.Deserialize<GoogleDriveClientOptions>(stream);
            if (options is null || !IsClientId(options.ClientId))
                throw new InvalidDataException(InvalidConfiguration);
            return options;
        }
        catch (JsonException)
        {
            throw new InvalidDataException(InvalidConfiguration);
        }
    }

    public static async Task<GoogleDriveClientOptions> ReadDownloadAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("installed", out var client) || client.ValueKind != JsonValueKind.Object
                || !client.TryGetProperty("client_id", out var id) || id.ValueKind != JsonValueKind.String
                || !IsClientId(id.GetString())
                || !client.TryGetProperty("client_secret", out var secret) || secret.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(secret.GetString())
                || !client.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array
                || !redirects.EnumerateArray().Any(IsLoopbackRedirect))
                throw new InvalidDataException(InvalidConfiguration);
            return new GoogleDriveClientOptions(id.GetString()!, secret.GetString());
        }
        catch (JsonException)
        {
            throw new InvalidDataException(InvalidConfiguration);
        }
    }

    private static bool IsClientId(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length > ".apps.googleusercontent.com".Length
        && value.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal);

    private static bool IsLoopbackRedirect(JsonElement value) => value.ValueKind == JsonValueKind.String
        && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
}
