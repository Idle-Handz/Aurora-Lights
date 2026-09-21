namespace Builder.Presentation.Services.Sources;

/// <summary>
/// Builder infrastructure, independent of the broader core-rulebook category. These sources define
/// the pieces the builder itself needs, so a character can never restrict them.
/// </summary>
public static class RequiredContentPolicy
{
    public static bool IsRequiredSource(string? name) => name?.Trim().ToLowerInvariant() is
        "internal" or "core" or "aurora essentials" or "aurora legacy essentials";
}
