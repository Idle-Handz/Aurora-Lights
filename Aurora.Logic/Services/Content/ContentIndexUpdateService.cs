using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Builder.Presentation.Services.Content;

public sealed record ContentIndexUpdateRequest(
    string RootDirectory,
    IReadOnlyList<string> IndexFileNames,
    int MaxConcurrency = 8);

public sealed record ContentIndexUpdateProgress(
    string StatusMessage,
    int? ProgressPercentage,
    int ProcessedEntryCount,
    int EstimatedEntryCount,
    int UpdatedFileCount,
    int FailedFileCount,
    string? CurrentFileName = null);

public sealed record ContentIndexUpdateResult(
    bool Updated,
    int UpdatedFileCount,
    int CheckedEntryCount,
    int EstimatedEntryCount,
    int IndexFileCount,
    int FailedFileCount,
    TimeSpan Duration);

/// <summary>
/// Updates Aurora .index content trees without depending on any UI framework.
/// The storage convention mirrors Aurora Legacy: an index's child files live in
/// a sibling directory named after the index file without the .index extension.
/// </summary>
public sealed class ContentIndexUpdateService
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient _httpClient;

    public ContentIndexUpdateService()
        : this(SharedHttpClient)
    {
    }

    public ContentIndexUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ContentIndexUpdateResult> UpdateAsync(
        ContentIndexUpdateRequest request,
        IProgress<ContentIndexUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RootDirectory))
            throw new ArgumentException("Root directory is required.", nameof(request));

        string rootDirectory = Path.GetFullPath(request.RootDirectory);
        Directory.CreateDirectory(rootDirectory);

        var indexNames = request.IndexFileNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var state = new UpdateState(rootDirectory, progress);
        state.AddEstimatedEntries(EstimateLocalEntryCount(rootDirectory, indexNames));
        state.Report("Checking installed content sources...");

        var started = DateTimeOffset.UtcNow;
        int maxConcurrency = Math.Clamp(request.MaxConcurrency, 1, 16);
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        foreach (string indexName in indexNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string indexPath = ResolveChildPath(rootDirectory, indexName);
            await ProcessIndexAsync(indexPath, gate, state, cancellationToken).ConfigureAwait(false);
        }

        state.Report("Content source check complete.", forceComplete: true);
        return new ContentIndexUpdateResult(
            Updated: state.UpdatedFileCount > 0,
            UpdatedFileCount: state.UpdatedFileCount,
            CheckedEntryCount: state.ProcessedEntryCount,
            EstimatedEntryCount: state.EstimatedEntryCount,
            IndexFileCount: state.IndexFileCount,
            FailedFileCount: state.FailedFileCount,
            Duration: DateTimeOffset.UtcNow - started);
    }

    private async Task ProcessIndexAsync(
        string indexPath,
        SemaphoreSlim gate,
        UpdateState state,
        CancellationToken cancellationToken)
    {
        indexPath = Path.GetFullPath(indexPath);
        if (!state.TryVisitIndex(indexPath))
            return;

        state.IncrementIndexCount();
        state.Report($"Reading {Path.GetFileName(indexPath)}...");

        if (!File.Exists(indexPath))
        {
            state.MarkFailure(indexPath, "Index file is missing.");
            return;
        }

        if (!TryParseIndex(indexPath, state, out ParsedIndex parsed))
            return;

        if (parsed.UpdateFile is { } updateFile)
        {
            state.AddEstimatedEntries(1);
            var updateResult = await DownloadEntryAsync(
                updateFile with { Name = Path.GetFileName(indexPath) },
                indexPath,
                gate,
                state,
                cancellationToken).ConfigureAwait(false);

            if (updateResult == DownloadResult.Updated &&
                !TryParseIndex(indexPath, state, out parsed))
            {
                return;
            }
        }

        string contentDirectory = GetContentDirectory(indexPath);
        Directory.CreateDirectory(contentDirectory);

        ApplyObsoleteEntries(parsed.ObsoleteEntries, contentDirectory, state);

        if (parsed.FileEntries.Count == 0)
            return;

        state.AddEstimatedEntries(parsed.FileEntries.Count);

        var tasks = parsed.FileEntries.Select(async entry =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string childPath = ResolveChildPath(contentDirectory, entry.Name);
            var result = await DownloadEntryAsync(entry, childPath, gate, state, cancellationToken)
                .ConfigureAwait(false);

            if (entry.IsIndex && File.Exists(childPath) && result != DownloadResult.Failed)
                await ProcessIndexAsync(childPath, gate, state, cancellationToken).ConfigureAwait(false);
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<DownloadResult> DownloadEntryAsync(
        IndexEntry entry,
        string destinationPath,
        SemaphoreSlim gate,
        UpdateState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Url))
        {
            state.MarkProcessed(entry.Name, updated: false, failed: true, "Missing download URL.");
            return DownloadResult.Failed;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string url = NormalizeUrl(entry.Url);
            string cachePath = GetCachePath(state.RootDirectory, url);
            bool localFileExists = File.Exists(destinationPath);
            HttpCacheEntry? cache = localFileExists
                ? await ReadCacheAsync(cachePath, cancellationToken).ConfigureAwait(false)
                : null;

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(cache?.ETag))
                message.Headers.TryAddWithoutValidation("If-None-Match", cache.ETag);
            if (cache?.LastModified is { } lastModified)
                message.Headers.IfModifiedSince = lastModified;
            else if (localFileExists)
                message.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(destinationPath);

            state.Report($"Checking {entry.Name}...", currentFileName: entry.Name);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                state.MarkProcessed(entry.Name, updated: false);
                return DownloadResult.Unchanged;
            }

            if (!response.IsSuccessStatusCode)
            {
                state.MarkProcessed(
                    entry.Name,
                    updated: false,
                    failed: true,
                    $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim());
                return DownloadResult.Failed;
            }

            byte[] remoteBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            bool changed = true;
            if (File.Exists(destinationPath))
            {
                byte[] localBytes = await File.ReadAllBytesAsync(destinationPath, cancellationToken)
                    .ConfigureAwait(false);
                changed = !remoteBytes.SequenceEqual(localBytes);
            }

            if (changed)
                await WriteFileAtomicallyAsync(destinationPath, remoteBytes, cancellationToken).ConfigureAwait(false);

            try
            {
                await WriteCacheAsync(cachePath, CreateCacheEntry(response), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                state.Report($"{entry.Name} cache metadata was not saved: {ex.Message}", currentFileName: entry.Name);
            }

            state.MarkProcessed(entry.Name, updated: changed);
            return changed ? DownloadResult.Updated : DownloadResult.Unchanged;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            state.MarkProcessed(entry.Name, updated: false, failed: true, ex.Message);
            return DownloadResult.Failed;
        }
        finally
        {
            gate.Release();
        }
    }

    private static ParsedIndex ParseIndex(string indexPath)
    {
        XDocument document = XDocument.Load(indexPath, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidDataException($"'{indexPath}' is not a valid index file.");

        XElement? updateFile = root.Element("info")?.Element("update")?.Elements("file").FirstOrDefault();
        var files = root.Element("files")?.Elements() ?? Enumerable.Empty<XElement>();

        var entries = new List<IndexEntry>();
        var obsoleteEntries = new List<IndexEntry>();

        foreach (XElement element in files)
        {
            string elementName = element.Name.LocalName;
            if (!elementName.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                !elementName.Equals("obsolete", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = CreateEntry(element, isObsolete: elementName.Equals("obsolete", StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                continue;

            if (entry.IsObsolete)
                obsoleteEntries.Add(entry);
            else
                entries.Add(entry);
        }

        return new ParsedIndex(
            UpdateFile: updateFile is null ? null : CreateEntry(updateFile, isObsolete: false),
            FileEntries: entries,
            ObsoleteEntries: obsoleteEntries);
    }

    private static bool TryParseIndex(string indexPath, UpdateState state, out ParsedIndex parsed)
    {
        try
        {
            parsed = ParseIndex(indexPath);
            return true;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            parsed = new ParsedIndex(null, [], []);
            state.MarkFailure(indexPath, ex.Message);
            return false;
        }
    }

    private static IndexEntry? CreateEntry(XElement element, bool isObsolete)
    {
        string? name = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string url = (string?)element.Attribute("url") ?? string.Empty;
        bool obsolete = isObsolete ||
                        string.Equals((string?)element.Attribute("obsolete"), "true", StringComparison.OrdinalIgnoreCase);

        return new IndexEntry(name, url, obsolete);
    }

    private static void ApplyObsoleteEntries(
        IReadOnlyList<IndexEntry> obsoleteEntries,
        string contentDirectory,
        UpdateState state)
    {
        foreach (var obsolete in obsoleteEntries)
        {
            string targetPath = ResolveChildPath(contentDirectory, obsolete.Name);
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                    state.MarkLocalChange(obsolete.Name);
                }
            }
            catch (Exception ex)
            {
                state.MarkFailure(obsolete.Name, ex.Message);
            }
        }
    }

    private static int EstimateLocalEntryCount(string rootDirectory, IReadOnlyList<string> indexNames)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        foreach (string indexName in indexNames)
        {
            string path = ResolveChildPath(rootDirectory, indexName);
            CountIndex(path);
        }

        return Math.Max(count, indexNames.Count);

        void CountIndex(string indexPath)
        {
            indexPath = Path.GetFullPath(indexPath);
            if (!visited.Add(indexPath) || !File.Exists(indexPath))
                return;

            ParsedIndex parsed;
            try { parsed = ParseIndex(indexPath); }
            catch { return; }

            count += parsed.FileEntries.Count + (parsed.UpdateFile is null ? 0 : 1);
            string contentDirectory = GetContentDirectory(indexPath);
            foreach (var entry in parsed.FileEntries.Where(e => e.IsIndex))
                CountIndex(ResolveChildPath(contentDirectory, entry.Name));
        }
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? "https://" + url[7..]
            : url;
    }

    private static string GetContentDirectory(string indexPath) =>
        Path.Combine(Path.GetDirectoryName(indexPath) ?? string.Empty, Path.GetFileNameWithoutExtension(indexPath));

    private static string ResolveChildPath(string directory, string childName)
    {
        string root = Path.GetFullPath(directory);
        string normalizedChild = childName.Replace('/', Path.DirectorySeparatorChar)
                                         .Replace('\\', Path.DirectorySeparatorChar);
        string combined = Path.GetFullPath(Path.Combine(root, normalizedChild));
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !combined.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Index entry path escapes its content directory: {childName}");
        }

        return combined;
    }

    private static async Task WriteFileAtomicallyAsync(
        string destinationPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        string tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private static string GetCachePath(string rootDirectory, string url)
    {
        string cacheDirectory = Path.Combine(rootDirectory, ".aurora-index-cache");
        Directory.CreateDirectory(cacheDirectory);
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Path.Combine(cacheDirectory, Convert.ToHexString(hash).ToLowerInvariant() + ".json");
    }

    private static async Task<HttpCacheEntry?> ReadCacheAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<HttpCacheEntry>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(
        string path,
        HttpCacheEntry cacheEntry,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, cacheEntry, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static HttpCacheEntry CreateCacheEntry(HttpResponseMessage response) =>
        new(
            ETag: response.Headers.ETag?.ToString(),
            LastModified: response.Content.Headers.LastModified ?? response.Headers.Date,
            ContentLength: response.Content.Headers.ContentLength);

    private sealed record ParsedIndex(
        IndexEntry? UpdateFile,
        IReadOnlyList<IndexEntry> FileEntries,
        IReadOnlyList<IndexEntry> ObsoleteEntries);

    private sealed record IndexEntry(string Name, string Url, bool IsObsolete)
    {
        public bool IsIndex => Name.EndsWith(".index", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HttpCacheEntry(string? ETag, DateTimeOffset? LastModified, long? ContentLength);

    private enum DownloadResult
    {
        Unchanged,
        Updated,
        Failed,
    }

    private sealed class UpdateState
    {
        private readonly object _gate = new();
        private readonly ConcurrentDictionary<string, byte> _visitedIndexes = new(StringComparer.OrdinalIgnoreCase);
        private readonly IProgress<ContentIndexUpdateProgress>? _progress;

        public UpdateState(string rootDirectory, IProgress<ContentIndexUpdateProgress>? progress)
        {
            RootDirectory = rootDirectory;
            _progress = progress;
        }

        public string RootDirectory { get; }
        public int ProcessedEntryCount { get; private set; }
        public int EstimatedEntryCount { get; private set; }
        public int UpdatedFileCount { get; private set; }
        public int FailedFileCount { get; private set; }
        public int IndexFileCount { get; private set; }

        public bool TryVisitIndex(string indexPath) => _visitedIndexes.TryAdd(indexPath, 0);

        public void IncrementIndexCount()
        {
            lock (_gate)
                IndexFileCount++;
        }

        public void AddEstimatedEntries(int count)
        {
            if (count <= 0) return;
            lock (_gate)
                EstimatedEntryCount = Math.Max(EstimatedEntryCount, ProcessedEntryCount + count);
        }

        public void MarkProcessed(string fileName, bool updated, bool failed = false, string? failure = null)
        {
            lock (_gate)
            {
                ProcessedEntryCount++;
                if (updated) UpdatedFileCount++;
                if (failed) FailedFileCount++;
            }

            string status = failed
                ? $"{fileName} failed: {failure ?? "unknown error"}"
                : updated
                    ? $"{fileName} updated"
                    : $"{fileName} is up to date";
            Report(status, currentFileName: fileName);
        }

        public void MarkLocalChange(string fileName)
        {
            lock (_gate)
                UpdatedFileCount++;
            Report($"{fileName} removed as obsolete", currentFileName: fileName);
        }

        public void MarkFailure(string fileName, string failure)
        {
            lock (_gate)
                FailedFileCount++;
            Report($"{fileName} failed: {failure}", currentFileName: fileName);
        }

        public void Report(string status, bool forceComplete = false, string? currentFileName = null)
        {
            if (_progress == null)
                return;

            int processed;
            int estimated;
            int updated;
            int failed;
            lock (_gate)
            {
                processed = ProcessedEntryCount;
                estimated = EstimatedEntryCount;
                updated = UpdatedFileCount;
                failed = FailedFileCount;
            }

            int? percentage = estimated > 0
                ? Math.Clamp((int)Math.Round(processed * 100d / estimated), 0, forceComplete ? 100 : 99)
                : null;

            if (forceComplete)
                percentage = 100;

            _progress.Report(new ContentIndexUpdateProgress(
                status,
                percentage,
                processed,
                estimated,
                updated,
                failed,
                currentFileName));
        }
    }
}
