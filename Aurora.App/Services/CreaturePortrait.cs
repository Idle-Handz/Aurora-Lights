namespace Aurora.App.Services;

/// <summary>Small embedded raster portraits travel with the character and never depend on a local path.</summary>
public static class CreaturePortrait
{
    public const int MaximumBytes = 5 * 1024 * 1024;

    public static async Task<string> ReadAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + count > MaximumBytes)
                throw new InvalidDataException("Choose an image smaller than 5 MB.");
            buffer.Write(chunk, 0, count);
        }
        return Encode(buffer.ToArray());
    }

    private static string Encode(byte[] bytes)
    {
        string? mime = Mime(bytes);
        if (mime == null) throw new InvalidDataException("Choose a PNG, JPEG, GIF, BMP, or WebP image.");
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string? Mime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12) return null;
        if (bytes[..8].SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10})) return "image/png";
        if (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)) return "image/gif";
        if (bytes[..2].SequenceEqual("BM"u8)) return "image/bmp";
        if (bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static string? DisplaySource(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumBytes * 4 / 3 + 128) return null;
        int comma = value.IndexOf(',');
        if (comma < 0 || !value.StartsWith("data:image/", StringComparison.Ordinal)) return null;
        try
        {
            var bytes = Convert.FromBase64String(value[(comma + 1)..]);
            return bytes.Length <= MaximumBytes && Mime(bytes) is { } mime
                && value[..comma] == $"data:{mime};base64" ? value : null;
        }
        catch (FormatException) { return null; }
    }

    public static string LegacySource(string? path)
    {
        if (DisplaySource(path) is { } embedded) return embedded;
        try
        {
            if (!string.IsNullOrEmpty(path) && new FileInfo(path) is { Exists: true, Length: > 0 and <= MaximumBytes })
                return Encode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        return "";
    }
}
