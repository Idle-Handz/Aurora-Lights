namespace Builder.Presentation.Utilities;

/// <summary>
/// Canonical path containment checks shared by content and session workspaces.
/// </summary>
public static class PathContainment
{
    public static bool IsPathWithinDirectory(string directoryPath, string candidatePath)
    {
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(directory, candidate, comparison))
            return true;

        return candidate.StartsWith(directory + Path.DirectorySeparatorChar, comparison);
    }
}
