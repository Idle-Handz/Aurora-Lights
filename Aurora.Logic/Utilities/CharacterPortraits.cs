using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Utilities;

internal static class CharacterPortraits
{
    internal const string DefaultFileName = "default-portrait.png";

    internal static string GetFileName(string? path) =>
        Path.GetFileName((path ?? string.Empty).Replace('\\', '/'));

    internal static string EnsureDefaultPortrait()
    {
        string directory = DataManager.Current.UserDocumentsPortraitsDirectory;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, DefaultFileName);
        // Repair empty placeholders as well as missing files, while leaving an
        // existing, nonempty user portrait alone.
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            using var resource = typeof(CharacterPortraits).Assembly.GetManifestResourceStream(
                "Builder.Presentation.Resources.default-portrait.png")
                ?? throw new InvalidOperationException("The default character portrait is not bundled.");
            using var output = File.Create(path);
            resource.CopyTo(output);
        }
        return path;
    }
}
