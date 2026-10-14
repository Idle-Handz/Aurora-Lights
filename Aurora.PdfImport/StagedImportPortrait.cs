namespace Aurora.PdfImport;

/// <summary>Owns the portrait files created by one import attempt.</summary>
public sealed class StagedImportPortrait : IDisposable
{
    private string? _pendingDestination;
    private bool _disposed;

    private StagedImportPortrait(string path) => Path = path;

    public string Path { get; }

    public static async Task<StagedImportPortrait> CreateAsync(byte[] bytes, string extension)
    {
        // Only the image suffix is taken from the source; names never identify previews.
        string suffix = extension.ToLowerInvariant() switch
        {
            ".png" => ".png",
            ".gif" => ".gif",
            ".webp" => ".webp",
            _ => ".jpg"
        };
        var portrait = new StagedImportPortrait(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"aurora-import-portrait-{Guid.NewGuid():N}{suffix}"));
        try
        {
            await using var stream = new FileStream(portrait.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(bytes);
            return portrait;
        }
        catch
        {
            portrait.Dispose();
            throw;
        }
    }

    public string CopyForCommit(string directory, string? characterName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RollbackCommit();
        Directory.CreateDirectory(directory);
        string safeName = string.Concat((characterName ?? "portrait")
            .Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c))).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "portrait";
        // Keep the complete filename within common filesystem limits.
        safeName = safeName[..Math.Min(safeName.Length, 100)];
        string destination = System.IO.Path.Combine(directory,
            $"{safeName}-{Guid.NewGuid():N}{System.IO.Path.GetExtension(Path)}");
        File.Copy(Path, destination, overwrite: false);
        _pendingDestination = destination;
        return destination;
    }

    /// <summary>Called only after the character referencing this portrait has been published.</summary>
    public void AcceptCommit()
    {
        _pendingDestination = null;
        Dispose();
    }

    public void RollbackCommit()
    {
        if (_pendingDestination is null) return;
        DeleteOwnedFile(_pendingDestination);
        _pendingDestination = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RollbackCommit();
        DeleteOwnedFile(Path);
    }

    private static void DeleteOwnedFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
