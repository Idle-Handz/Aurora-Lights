namespace Aurora.Components.Models;

public static class MagicCastingTimeClassifier
{
    public static bool Matches(string? castingTime, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)
            || string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(Classify(castingTime), filter, StringComparison.Ordinal);
    }

    public static string Classify(string? castingTime)
    {
        if (string.IsNullOrWhiteSpace(castingTime))
            return string.Empty;

        string normalized = castingTime.Trim().ToLowerInvariant();
        return normalized switch
        {
            _ when normalized.Contains("bonus action", StringComparison.Ordinal) => "bonus-action",
            _ when normalized.Contains("reaction", StringComparison.Ordinal) => "reaction",
            "action" => "action",
            _ when normalized.StartsWith("1 action", StringComparison.Ordinal) => "action",
            _ when normalized.StartsWith("an action", StringComparison.Ordinal) => "action",
            _ => "longer",
        };
    }
}
