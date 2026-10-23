using System.Text.RegularExpressions;

namespace Aurora.App.Services;

/// <summary>Serializes imports across service instances/processes before reclaiming interrupted candidates.</summary>
internal sealed class ContentImportWorkspace : IDisposable
{
    internal const string LockFileName = ".aurora-import.lock";
    private static readonly Regex CandidateName = new(
        @"^\.aurora-candidate-[0-9a-f]{32}\.sqlite(?:-journal|-wal|-shm)?$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PreparationName = new(@"^translator-preparation-[0-9a-f]{32}$", RegexOptions.CultureInvariant);
    private readonly FileStream _lease;
    private readonly FileStream? _preparationLease;
    private readonly string _directory;
    private readonly string? _preparationDirectory;

    internal ContentImportWorkspace(string directory, string? preparationDirectory = null)
    {
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        _preparationDirectory = preparationDirectory ?? (OperatingSystem.IsAndroid() ? Path.GetTempPath() : null);
        if (_preparationDirectory is not null)
        {
            _preparationDirectory = Path.GetFullPath(_preparationDirectory);
            Directory.CreateDirectory(_preparationDirectory);
            // Preparation uses an app-wide temp location, independent of the selected content root.
            // Lock it first for every Android import so switching roots cannot race its cleanup.
            _preparationLease = new FileStream(Path.Combine(_preparationDirectory, ".aurora-preparation.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        // Keep this empty file permanently: deleting a lock file can create two independently
        // locked inodes. Process termination releases the OS lock without needing a finally block.
        try
        {
            _lease = new FileStream(Path.Combine(_directory, LockFileName), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch { _preparationLease?.Dispose(); throw; }
    }

    internal (int Files, long Bytes) CleanCandidates(Action<string, Exception>? onFailure = null)
    {
        int count = 0;
        long bytes = 0;
        // Only the primary root, never character folders, correction archives, or arbitrary SQLite.
        var groups = Directory.EnumerateFiles(_directory).Where(path => CandidateName.IsMatch(Path.GetFileName(path)))
            .GroupBy(path => Path.GetFileName(path)[..(".aurora-candidate-".Length + 32 + ".sqlite".Length)]);
        foreach (var group in groups)
        {
            var handles = new List<(string Path, FileStream Stream)>();
            try
            {
                // Acquire every member before deleting any. In particular, a locked candidate's
                // journal must survive too. Reparse points are never followed or removed.
                foreach (string path in group)
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Candidate is a link; cleanup skipped it.");
                    handles.Add((path, new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete)));
                }
                foreach (var (path, stream) in handles)
                {
                    long length = stream.Length;
                    File.Delete(path);
                    count++;
                    bytes += length;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onFailure?.Invoke(group.Key, ex);
            }
            finally
            {
                foreach (var (_, stream) in handles) stream.Dispose();
            }
        }
        return (count, bytes);
    }

    internal (int Directories, long Bytes) CleanPreparationDirectories(Action<string, Exception>? onFailure = null)
    {
        if (_preparationDirectory is null) return (0, 0);
        int count = 0;
        long bytes = 0;
        foreach (string path in Directory.EnumerateDirectories(_preparationDirectory)
                     .Where(path => PreparationName.IsMatch(Path.GetFileName(path))))
        {
            try
            {
                long size = 0;
                var pending = new Stack<string>();
                pending.Push(path);
                // Validate the whole tree before deletion; never follow a link out of app temp.
                while (pending.TryPop(out string? entry))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Preparation contains a link; cleanup skipped it.");
                    if ((attributes & FileAttributes.Directory) != 0)
                        foreach (string child in Directory.EnumerateFileSystemEntries(entry)) pending.Push(child);
                    else size += new FileInfo(entry).Length;
                }
                Directory.Delete(path, recursive: true);
                count++;
                bytes += size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onFailure?.Invoke(path, ex);
            }
        }
        return (count, bytes);
    }

    public void Dispose()
    {
        _lease.Dispose();
        _preparationLease?.Dispose();
    }
}
