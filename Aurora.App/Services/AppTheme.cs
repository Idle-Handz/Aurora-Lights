namespace Aurora.App.Services;

/// <summary>Stable identifiers for the visual themes available in Aurora: Reflections.</summary>
public enum AppTheme
{
    ReflectionsDark,
    ReflectionsLight,
    AuroraClassic,
    AuroraBorealis
}

/// <summary>Display and rendering metadata for an application theme.</summary>
public sealed record AppThemeDefinition(
    AppTheme Theme,
    string Id,
    string DisplayName,
    string Description,
    bool IsDark);

public static class AppThemes
{
    public static readonly AppThemeDefinition ReflectionsDark = new(
        AppTheme.ReflectionsDark,
        "reflections-dark",
        "Reflections Dark",
        "The familiar cool-blue Reflections palette, tuned for low-light use.",
        IsDark: true);

    public static readonly AppThemeDefinition ReflectionsLight = new(
        AppTheme.ReflectionsLight,
        "reflections-light",
        "Reflections Light",
        "A clean paper-white workspace with crisp blue and violet accents.",
        IsDark: false);

    public static readonly AppThemeDefinition AuroraClassic = new(
        AppTheme.AuroraClassic,
        "aurora-classic",
        "Aurora Classic",
        "Legacy charcoal surfaces, warm gold details, and Aurora's original teal.",
        IsDark: true);

    public static readonly AppThemeDefinition AuroraBorealis = new(
        AppTheme.AuroraBorealis,
        "aurora-borealis",
        "Aurora Borealis",
        "A deep blue-green night palette with luminous teal and violet accents.",
        IsDark: true);

    public static IReadOnlyList<AppThemeDefinition> All { get; } =
    [
        ReflectionsDark,
        ReflectionsLight,
        AuroraClassic,
        AuroraBorealis
    ];

    public static AppThemeDefinition Get(AppTheme theme) => theme switch
    {
        AppTheme.ReflectionsLight => ReflectionsLight,
        AppTheme.AuroraClassic    => AuroraClassic,
        AppTheme.AuroraBorealis   => AuroraBorealis,
        _                         => ReflectionsDark
    };

    public static AppTheme FromId(string? id) =>
        All.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))?.Theme
        ?? AppTheme.ReflectionsDark;
}
