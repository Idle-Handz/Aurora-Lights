using System.Text;
using System.Text.Json;
using Aurora.App.Services;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveClientConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aurora-drive-config-" + Guid.NewGuid());
    private string OverridePath => Path.Combine(_directory, "google-drive-client.json");
    private GoogleDriveClientConfiguration Configuration => new(OverridePath, GetType().Assembly);

    [Fact]
    public void FreshInstallation_UsesEmbeddedConfigurationWithoutCreatingAnOverride()
    {
        Configuration.IsConfigured.Should().BeTrue();
        Configuration.HasBundledConfiguration.Should().BeTrue();
        Configuration.HasOverride.Should().BeFalse();
        Configuration.Read().ClientId.Should().Be("bundled-test.apps.googleusercontent.com");
        File.Exists(OverridePath).Should().BeFalse();
    }

    [Fact]
    public void ExistingInstallation_KeepsImportedClientInsteadOfChangingItsDriveProject()
    {
        var options = new GoogleDriveClientOptions("existing.apps.googleusercontent.com", "existing-secret");
        WriteOverride(JsonSerializer.Serialize(options));
        Configuration.Read().Should().Be(options);
    }

    [Fact]
    public void InvalidExistingConfiguration_DoesNotSilentlySwitchToBundledProject()
    {
        WriteOverride("{ broken config");
        var read = () => Configuration.Read();
        read.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void DeveloperBuildWithoutEmbeddedConfiguration_AllowsManualSetup()
    {
        var configuration = new GoogleDriveClientConfiguration(OverridePath, typeof(object).Assembly);
        configuration.IsConfigured.Should().BeFalse();
        var read = () => configuration.Read();
        read.Should().Throw<InvalidOperationException>();
        WriteOverride(JsonSerializer.Serialize(new GoogleDriveClientOptions("manual.apps.googleusercontent.com", null)));
        configuration.IsConfigured.Should().BeTrue();
        configuration.Read().ClientId.Should().Be("manual.apps.googleusercontent.com");
    }

    [Fact]
    public async Task DesktopDownload_IsAccepted()
    {
        using var stream = JsonStream("""
            {"installed":{"client_id":"desktop.apps.googleusercontent.com","client_secret":"fake-secret",
            "redirect_uris":["http://localhost"]}}
            """);
        var options = await GoogleDriveClientConfiguration.ReadDownloadAsync(stream);
        options.Should().Be(new GoogleDriveClientOptions("desktop.apps.googleusercontent.com", "fake-secret"));
    }

    [Theory]
    [InlineData("{\"installed\":{\"client_id\":\"android.apps.googleusercontent.com\"}}")]
    [InlineData("{\"web\":{\"client_id\":\"web.apps.googleusercontent.com\",\"client_secret\":\"fake\"}}")]
    [InlineData("{\"installed\":{\"client_id\":42}}")]
    [InlineData("{ broken")]
    [InlineData("[]")]
    public async Task NonDesktopOrMalformedDownloads_AreRejected(string json)
    {
        using var stream = JsonStream(json);
        Func<Task> read = () => GoogleDriveClientConfiguration.ReadDownloadAsync(stream);
        await read.Should().ThrowAsync<InvalidDataException>();
    }

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));
    private void WriteOverride(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(OverridePath, json);
    }
    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
