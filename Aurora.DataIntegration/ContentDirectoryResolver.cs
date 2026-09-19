using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Aurora.App.Services;

internal static class ContentDirectoryResolver
{
    public static IReadOnlyList<string> GetContentDirectories()
    {
        var result = new List<string>();
        AddIfValid(result, DataManager.Current.UserDocumentsCustomElementsDirectory);

        foreach (string dir in ApplicationContext.Current.Settings.AdditionalCustomDirectories)
            AddIfValid(result, dir);

        return result;
    }

    public static string GetPrimaryContentDirectory() =>
        DataManager.Current.UserDocumentsCustomElementsDirectory ?? string.Empty;

    private static void AddIfValid(List<string> result, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string normalized = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!Directory.Exists(normalized))
            return;

        if (result.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        result.Add(normalized);
    }
}
