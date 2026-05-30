using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Aurora.Web.Services;

public sealed class PhaseZeroSessionWorkspaceService
{
    private readonly IWebHostEnvironment _environment;
    private readonly PhaseZeroSessionOptions _options;
    private readonly ILogger<PhaseZeroSessionWorkspaceService> _logger;
    private PhaseZeroSessionWorkspace? _workspace;

    public PhaseZeroSessionWorkspaceService(
        IWebHostEnvironment environment,
        IOptions<PhaseZeroSessionOptions> options,
        ILogger<PhaseZeroSessionWorkspaceService> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public Task<PhaseZeroSessionWorkspace> GetWorkspaceAsync()
    {
        var workspace = EnsureWorkspace();
        Touch(workspace);
        return Task.FromResult(workspace);
    }

    public async Task<PhaseZeroImportResult> ImportFilesAsync(IReadOnlyList<IBrowserFile> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            return new PhaseZeroImportResult(await GetWorkspaceAsync(), 0, 0, 0, []);

        if (files.Count > _options.MaxFileCount)
            throw new InvalidOperationException($"You selected {files.Count} files, but this Phase 0 host currently allows up to {_options.MaxFileCount} files per import.");

        var workspace = EnsureWorkspace();
        string importsRoot = Path.Combine(workspace.WorkspacePath, "imports");
        Directory.CreateDirectory(importsRoot);

        int importedFiles = 0;
        int discoveredElements = 0;
        List<string> warnings = [];

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Size > _options.MaxSingleFileBytes)
            {
                warnings.Add($"{file.Name} was skipped because it exceeds the single-file limit.");
                continue;
            }

            string extension = Path.GetExtension(file.Name).ToLowerInvariant();
            switch (extension)
            {
                case ".xml":
                    {
                        string stored = await SaveBrowserFileAsync(file, importsRoot, cancellationToken);
                        discoveredElements += RegisterImportedFile(workspace, stored, ImportedContentKind.Xml);
                        importedFiles++;
                        break;
                    }

                case ".dnd5e":
                    {
                        RegisterNonIndexedFile(workspace, file.Name, ImportedContentKind.CharacterFile, file.Size);
                        await SaveBrowserFileAsync(file, importsRoot, cancellationToken);
                        importedFiles++;
                        break;
                    }

                case ".zip":
                    {
                        string storedArchive = await SaveBrowserFileAsync(file, importsRoot, cancellationToken);
                        RegisterNonIndexedFile(workspace, file.Name, ImportedContentKind.Archive, file.Size);
                        discoveredElements += ExtractArchive(workspace, storedArchive, warnings);
                        importedFiles++;
                        break;
                    }

                default:
                    warnings.Add($"{file.Name} was skipped because its extension is not supported.");
                    break;
            }
        }

        Touch(workspace);
        return new PhaseZeroImportResult(workspace, importedFiles, discoveredElements, workspace.Index.ElementTypes.Count, warnings);
    }

    public Task ClearAsync()
    {
        if (_workspace is { } workspace)
        {
            try
            {
                if (Directory.Exists(workspace.WorkspacePath))
                    Directory.Delete(workspace.WorkspacePath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear phase 0 workspace {WorkspacePath}", workspace.WorkspacePath);
            }
        }

        _workspace = null;
        return Task.CompletedTask;
    }

    public Task<string> ResolveWorkspacePathAsync(string relativePath)
    {
        PhaseZeroSessionWorkspace workspace = EnsureWorkspace();
        string fullPath = Path.GetFullPath(Path.Combine(workspace.WorkspacePath, relativePath));
        string rootPath = Path.GetFullPath(workspace.WorkspacePath);

        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Requested workspace file is outside the current session workspace.");

        return Task.FromResult(fullPath);
    }

    public Task<string> TrackGeneratedCharacterAsync(string absolutePath)
    {
        PhaseZeroSessionWorkspace workspace = EnsureWorkspace();
        string fullPath = Path.GetFullPath(absolutePath);
        string rootPath = Path.GetFullPath(workspace.WorkspacePath);

        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated character file must remain inside the current session workspace.");

        string relativePath = Path.GetRelativePath(workspace.WorkspacePath, fullPath);
        if (!workspace.ImportedFiles.Any(file =>
                file.Kind == ImportedContentKind.CharacterFile &&
                string.Equals(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)))
        {
            FileInfo info = new(fullPath);
            workspace.ImportedFiles.Add(new ImportedSessionFile(
                info.Name,
                relativePath,
                ImportedContentKind.CharacterFile,
                info.Length,
                0));
            Touch(workspace);
        }

        return Task.FromResult(relativePath);
    }

    public Task<bool> DeleteCharacterFileAsync(string relativePath)
    {
        PhaseZeroSessionWorkspace workspace = EnsureWorkspace();
        ImportedSessionFile? file = workspace.ImportedFiles.FirstOrDefault(candidate =>
            candidate.Kind == ImportedContentKind.CharacterFile &&
            string.Equals(candidate.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            return Task.FromResult(false);
        }

        string fullPath = Path.GetFullPath(Path.Combine(workspace.WorkspacePath, file.RelativePath));
        string rootPath = Path.GetFullPath(workspace.WorkspacePath);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Requested workspace file is outside the current session workspace.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        workspace.ImportedFiles.Remove(file);
        Touch(workspace);
        return Task.FromResult(true);
    }

    private PhaseZeroSessionWorkspace EnsureWorkspace()
    {
        if (_workspace != null)
            return _workspace;

        string root = ResolveRootDirectory();
        Directory.CreateDirectory(root);

        Guid id = Guid.NewGuid();
        string workspacePath = Path.Combine(root, id.ToString("N"));
        Directory.CreateDirectory(workspacePath);

        _workspace = new PhaseZeroSessionWorkspace
        {
            WorkspaceId = id,
            WorkspacePath = workspacePath,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastTouchedUtc = DateTimeOffset.UtcNow
        };

        Touch(_workspace);
        return _workspace;
    }

    private string ResolveRootDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.RootDirectory))
            return Path.GetFullPath(_options.RootDirectory);

        return Path.Combine(_environment.ContentRootPath, "App_Data", "phase0-sessions");
    }

    private void Touch(PhaseZeroSessionWorkspace workspace)
    {
        workspace.LastTouchedUtc = DateTimeOffset.UtcNow;

        try
        {
            Directory.SetLastWriteTimeUtc(workspace.WorkspacePath, workspace.LastTouchedUtc.UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to update workspace timestamp for {WorkspacePath}", workspace.WorkspacePath);
        }
    }

    private async Task<string> SaveBrowserFileAsync(IBrowserFile file, string root, CancellationToken cancellationToken)
    {
        string safeName = SanitizeFileName(file.Name);
        string targetPath = MakeUniquePath(root, safeName);

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var source = file.OpenReadStream(_options.MaxSingleFileBytes, cancellationToken);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken);

        return targetPath;
    }

    private int RegisterImportedFile(PhaseZeroSessionWorkspace workspace, string path, ImportedContentKind kind)
    {
        FileInfo info = new(path);
        List<ImportedElementHeader> elements = ReadElementHeaders(path);
        workspace.Index.AddRange(elements);
        workspace.ImportedFiles.Add(new ImportedSessionFile(info.Name, Path.GetRelativePath(workspace.WorkspacePath, path), kind, info.Length, elements.Count));
        return elements.Count;
    }

    private void RegisterNonIndexedFile(PhaseZeroSessionWorkspace workspace, string fileName, ImportedContentKind kind, long sizeBytes)
    {
        workspace.ImportedFiles.Add(new ImportedSessionFile(fileName, fileName, kind, sizeBytes, 0));
    }

    private int ExtractArchive(PhaseZeroSessionWorkspace workspace, string archivePath, List<string> warnings)
    {
        string extractRoot = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath));
        Directory.CreateDirectory(extractRoot);

        int discoveredElements = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            string extension = Path.GetExtension(entry.FullName).ToLowerInvariant();
            if (extension is not ".xml" and not ".dnd5e")
                continue;

            string relativePath = SanitizeRelativePath(entry.FullName);
            string destination = Path.GetFullPath(Path.Combine(extractRoot, relativePath));
            if (!destination.StartsWith(Path.GetFullPath(extractRoot), StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped suspicious archive entry: {entry.FullName}");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);

            if (extension == ".xml")
                discoveredElements += RegisterImportedFile(workspace, destination, ImportedContentKind.Xml);
            else
            {
                FileInfo info = new(destination);
                workspace.ImportedFiles.Add(new ImportedSessionFile(info.Name, Path.GetRelativePath(workspace.WorkspacePath, destination), ImportedContentKind.CharacterFile, info.Length, 0));
            }
        }

        return discoveredElements;
    }

    private static List<ImportedElementHeader> ReadElementHeaders(string path)
    {
        XDocument document = XDocument.Load(path, LoadOptions.None);
        return document.Root?
            .Elements("element")
            .Select(element => new ImportedElementHeader(
                element.Attribute("id")?.Value ?? string.Empty,
                element.Attribute("name")?.Value ?? "(unnamed)",
                element.Attribute("type")?.Value ?? "(unknown)",
                element.Attribute("source")?.Value ?? "(unspecified)",
                Path.GetFileName(path)))
            .ToList()
            ?? [];
    }

    private static string MakeUniquePath(string root, string fileName)
    {
        string path = Path.Combine(root, fileName);
        if (!File.Exists(path))
            return path;

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int index = 1;

        do
        {
            path = Path.Combine(root, $"{baseName}-{index}{extension}");
            index++;
        }
        while (File.Exists(path));

        return path;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private static string SanitizeRelativePath(string path)
    {
        string[] parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(parts.Select(SanitizeFileName).ToArray());
    }
}
