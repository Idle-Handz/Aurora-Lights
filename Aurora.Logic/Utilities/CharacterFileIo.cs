using System.Runtime.ExceptionServices;
using System.Text;
using System.Xml;

namespace Builder.Presentation.Utilities;

public readonly record struct CharacterFileDiskStamp(DateTime LastWriteTimeUtc, long Length)
{
    public static CharacterFileDiskStamp? Capture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var info = new FileInfo(path);
        return new CharacterFileDiskStamp(info.LastWriteTimeUtc, info.Length);
    }
}

public sealed class CharacterFileExternalChangeException : IOException
{
    public CharacterFileExternalChangeException(string path)
        : base($"{Path.GetFileName(path)} changed on disk after it was loaded. Reload the character, save a copy, or close the other Aurora window before overwriting it.")
    {
    }
}

public sealed class CharacterFileAccessException : IOException
{
    public CharacterFileAccessException(string path, string operation, Exception innerException)
        : base($"Could not {operation} {Path.GetFileName(path)} because another app still has the file open. Try again after the other Aurora window finishes reading or saving it.", innerException)
    {
    }
}

public static class CharacterFileIo
{
    private const int MaxAttempts = 6;
    private const int InitialDelayMs = 50;
    private const int MaxDelayMs = 500;

    public static XmlDocument LoadXmlDocument(string path) =>
        LoadXmlDocument(path, out _);

    public static XmlDocument LoadXmlDocument(string path, out CharacterFileDiskStamp? stamp)
    {
        XmlDocument document = ExecuteWithRetry(
            () =>
            {
                var doc = new XmlDocument();
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    FileOptions.SequentialScan);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { XmlResolver = null });
                doc.Load(reader);
                return doc;
            },
            path,
            "read",
            static ex => IsTransientFileAccess(ex) || ex is XmlException);

        stamp = CharacterFileDiskStamp.Capture(path);
        return document;
    }

    public static void SaveXmlDocumentAtomic(string path, XmlDocument document)
    {
        string tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            ExecuteWithRetry(
                () =>
                {
                    using var writer = new XmlTextWriter(tmp, Encoding.UTF8)
                    {
                        Formatting = Formatting.Indented,
                        IndentChar = '\t',
                        Indentation = 1,
                    };
                    document.Save(writer);
                    return true;
                },
                tmp,
                "write");

            ExecuteWithRetry(
                () =>
                {
                    File.Move(tmp, path, overwrite: true);
                    return true;
                },
                path,
                "replace");
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }

    public static bool HasChangedSince(string path, CharacterFileDiskStamp? expectedStamp)
    {
        if (!expectedStamp.HasValue)
            return false;

        CharacterFileDiskStamp? currentStamp = CharacterFileDiskStamp.Capture(path);
        return currentStamp.HasValue && currentStamp.Value != expectedStamp.Value;
    }

    public static void ThrowIfChangedSince(string path, CharacterFileDiskStamp? expectedStamp)
    {
        if (HasChangedSince(path, expectedStamp))
            throw new CharacterFileExternalChangeException(path);
    }

    private static T ExecuteWithRetry<T>(
        Func<T> action,
        string path,
        string operation,
        Func<Exception, bool>? isRetryable = null)
    {
        isRetryable ??= IsTransientFileAccess;
        int delayMs = InitialDelayMs;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (isRetryable(ex))
            {
                lastException = ex;
                if (attempt == MaxAttempts)
                    break;

                Thread.Sleep(delayMs);
                delayMs = Math.Min(delayMs * 2, MaxDelayMs);
            }
        }

        if (lastException is IOException or UnauthorizedAccessException)
            throw new CharacterFileAccessException(path, operation, lastException);

        ExceptionDispatchInfo.Capture(lastException!).Throw();
        throw new InvalidOperationException("Unreachable retry state.");
    }

    private static bool IsTransientFileAccess(Exception ex) =>
        ex is UnauthorizedAccessException
        || ex is IOException
            and not FileNotFoundException
            and not DirectoryNotFoundException
            and not PathTooLongException;
}
