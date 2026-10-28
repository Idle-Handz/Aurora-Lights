namespace Aurora.Reflections.Services;

/// <summary>Owns only temporary PDF exports, never character files or saved user documents.</summary>
public static class PdfExportFiles
{
    public static string CreatePath(string cacheDirectory, string characterName)
    {
        string root = Path.Combine(cacheDirectory, "pdf-exports");
        Directory.CreateDirectory(root);
        // External viewers may read after returning to Aurora. Retain recent exports.
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-2))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        string safeName = string.Concat(characterName.Select(c =>
            char.IsControl(c) || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ? '_' : c)).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Character";
        // Android limits a filename component by bytes. Keep whole Unicode characters.
        var bounded = new System.Text.StringBuilder();
        int bytes = 0;
        foreach (var rune in safeName.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 180) break;
            bounded.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }
        safeName = bounded.ToString();
        string folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, safeName + ".pdf");
    }

    public static void DeleteFailed(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
