using System.Text;
using Aurora.Web.Components.Pages;
using Aurora.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aurora.Tests.Tests;

public sealed class WebImportPageTests : BunitContext
{
    [Fact]
    public async Task ImportAcceptsMoreThanTenFilesWithinConfiguredLimit()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-web-tests", Guid.NewGuid().ToString("N"));
        var workspace = RegisterServices(root, out _);
        try
        {
            var cut = Render<Import>();
            IBrowserFile[] files = Enumerable.Range(0, 11)
                .Select(index => (IBrowserFile)new MemoryBrowserFile($"content-{index}.xml"))
                .ToArray();

            await cut.InvokeAsync(() => cut.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(files)));

            cut.Find(".status-banner").TextContent.Should().Contain("Imported 11 file(s)");
            (await workspace.GetWorkspaceAsync()).ImportedFiles.Should().HaveCount(11);
        }
        finally
        {
            await workspace.ClearAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ClearImportsAlsoClearsCharacterSession()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-web-tests", Guid.NewGuid().ToString("N"));
        var workspace = RegisterServices(root, out var characterSession);
        int characterChanges = 0;
        characterSession.CurrentCharacterChanged += () => characterChanges++;
        try
        {
            var cut = Render<Import>();
            string originalWorkspace = (await workspace.GetWorkspaceAsync()).WorkspacePath;

            cut.Find("button.danger").Click();

            characterChanges.Should().Be(1, "clearing files must also reset the active character");
            Directory.Exists(originalWorkspace).Should().BeFalse();
            cut.Find(".status-banner").TextContent.Should().Contain("Session workspace cleared");
        }
        finally
        {
            await workspace.ClearAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private PhaseZeroSessionWorkspaceService RegisterServices(string root, out WebCharacterSessionService characterSession)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var workspace = new PhaseZeroSessionWorkspaceService(
            new TestWebHostEnvironment { ContentRootPath = root },
            Options.Create(new PhaseZeroSessionOptions { RootDirectory = root, MaxFileCount = 64 }),
            NullLogger<PhaseZeroSessionWorkspaceService>.Instance);
        // Clearing a session does not invoke the process-global character engine.
        characterSession = new WebCharacterSessionService(workspace, null!,
            new WebCharacterEngineSessionGuard(), NullLogger<WebCharacterSessionService>.Instance);
        Services.AddSingleton(workspace);
        Services.AddSingleton(characterSession);
        return workspace;
    }

    private sealed class MemoryBrowserFile(string name) : IBrowserFile
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes("<elements />");
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => Content.Length;
        public string ContentType => "application/xml";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(Content, writable: false);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Aurora.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
