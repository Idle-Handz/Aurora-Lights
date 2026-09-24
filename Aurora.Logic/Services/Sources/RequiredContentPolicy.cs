namespace Builder.Presentation.Services.Sources;

/// <summary>
/// Builder infrastructure, independent of the broader core-rulebook category. These sources define
/// the pieces the builder itself needs, so a character can never restrict them.
/// </summary>
public static class RequiredContentPolicy
{
    public static bool IsRequiredSource(string? name) => name?.Trim().ToLowerInvariant() is
        "internal" or "core" or "aurora essentials" or "aurora legacy essentials";

    // Preserve the lock if an author changes the display name of the installed Essentials source.
    public static bool IsRequiredSource(string? name, string? id) =>
        IsRequiredSource(name) || string.Equals(id?.Trim(), "ID_SOURCE_AURORA_LEGACY_ESSENTIALS",
            System.StringComparison.OrdinalIgnoreCase);
}
