#nullable enable

namespace Builder.Data;

/// <summary>Builder infrastructure, independent of the broader core-rulebook category.</summary>
public static class RequiredContentPolicy
{
    public static bool IsRequiredSource(string? name) => name?.Trim().ToLowerInvariant() is
        "internal" or "core" or "aurora essentials" or "aurora legacy essentials";

    public static bool IsRequiredPackage(string? key, string? name) =>
        IsRequiredSource(name) || key?.ToLowerInvariant() is
            "core-ale-xml" or "core-internal-xml" or "core:internal" or "core:core" or
            "core:aurora-legacy-essentials" or "core:aurora-essentials" or "runtime-builtins";
}
