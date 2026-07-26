using System.IO.Compression;
using Aurora.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aurora.Tests.Tests;

public sealed class PhaseZeroSessionWorkspaceServiceTests
{
    [Fact]
    public async Task ResolveWorkspacePathAsync_RejectsSiblingWithSharedPrefix()
    {
        string root = CreateTempDirectory();
        var service = CreateService(root);

        try
        {
            PhaseZeroSessionWorkspace workspace = await service.GetWorkspaceAsync();
            string siblingName = Path.GetFileName(workspace.WorkspacePath) + "-escape";
            string relativePath = Path.Combine("..", siblingName, "character.dnd5e");

            Func<Task> act = () => service.ResolveWorkspacePathAsync(relativePath);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            await service.ClearAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportFilesAsync_RejectsArchiveTraversalUsingSharedPrefix()
    {
        string root = CreateTempDirectory();
        var service = CreateService(root);
        byte[] archive = CreateArchive("../pack-escape/character.dnd5e", "<character />");

        try
        {
            PhaseZeroImportResult result = await service.ImportFilesAsync(
                [new MemoryBrowserFile("pack.zip", archive)]);
            string escaped = Path.Combine(
                result.Workspace.WorkspacePath,
                "imports",
                "pack-escape",
                "character.dnd5e");

            result.Warnings.Should().ContainSingle(message =>
                message.Contains("suspicious archive entry", StringComparison.OrdinalIgnoreCase));
            File.Exists(escaped).Should().BeFalse();
        }
        finally
        {
            await service.ClearAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportFilesAsync_StopsWhenExpandedArchiveLimitIsExceeded()
    {
        string root = CreateTempDirectory();
        var service = CreateService(root, options => options.MaxArchiveExpandedBytes = 16);
        byte[] archive = CreateArchive("content.xml", "<elements><element id=\"test\" /></elements>");

        try
        {
            PhaseZeroImportResult result = await service.ImportFilesAsync(
                [new MemoryBrowserFile("pack.zip", archive)]);

            result.Warnings.Should().ContainSingle(message =>
                message.Contains("expanded content", StringComparison.OrdinalIgnoreCase));
            result.DiscoveredElements.Should().Be(0);
        }
        finally
        {
            await service.ClearAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private static PhaseZeroSessionWorkspaceService CreateService(
        string root,
        Action<PhaseZeroSessionOptions>? configure = null)
    {
        var options = new PhaseZeroSessionOptions
        {
            RootDirectory = root,
            MaxSingleFileBytes = 1024 * 1024,
            MaxArchiveEntryCount = 32,
            MaxArchiveExpandedBytes = 1024 * 1024
        };
        configure?.Invoke(options);

        return new PhaseZeroSessionWorkspaceService(
            new TestWebHostEnvironment { ContentRootPath = root },
            Options.Create(options),
            NullLogger<PhaseZeroSessionWorkspaceService>.Instance);
    }

    private static byte[] CreateArchive(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return stream.ToArray();
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aurora-web-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class MemoryBrowserFile(string name, byte[] content) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
        public long Size => content.LongLength;
        public string ContentType => "application/zip";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
                throw new IOException("File exceeds the allowed size.");

            return new MemoryStream(content, writable: false);
        }
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
