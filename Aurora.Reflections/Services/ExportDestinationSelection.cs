namespace Aurora.Reflections.Services;

public enum ExistingExportChoice { Replace, ChangeName, Cancel }

/// <summary>Names a file inside a selected folder and makes replacement an explicit decision.</summary>
public static class ExportDestinationSelection
{
    public static async Task<string?> ChooseAsync(string folder, string suggestedName,
        Func<string, string?, Task<string?>> requestName,
        Func<string, Task<ExistingExportChoice>> confirmExisting,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        string extension = Path.GetExtension(suggestedName);
        string? error = null;
        while (true)
        {
            string? name = await requestName(suggestedName, error);
            if (name is null) return null;
            name = name.Trim();
            if (name.Length == 0 || name is "." or ".." || Path.IsPathRooted(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
            {
                error = "Enter a file name without folders or invalid characters.";
                continue;
            }
            if (Path.GetExtension(name).Length == 0) name += extension;
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                suggestedName = name;
                error = $"The file name must end in {extension}.";
                continue;
            }
            string destination = Path.Combine(folder, name);
            if (!fileExists(destination)) return destination;
            switch (await confirmExisting(destination))
            {
                case ExistingExportChoice.Replace: return destination;
                case ExistingExportChoice.Cancel: return null;
                default: suggestedName = name; error = "Choose a different file name."; break;
            }
        }
    }
}
