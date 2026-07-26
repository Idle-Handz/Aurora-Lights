using System.Globalization;

namespace Builder.Presentation.Utilities;

public static class SourceReleaseTextSelector
{
    public static string? SelectLatest(IEnumerable<string?> releaseTexts)
    {
        string? latest = null;
        DateTimeOffset? latestDate = null;

        foreach (string? value in releaseTexts)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string candidate = value.Trim();
            DateTimeOffset? candidateDate = TryParseDate(candidate);
            if (latest == null ||
                candidateDate.HasValue && (!latestDate.HasValue || candidateDate > latestDate) ||
                candidateDate == latestDate &&
                StringComparer.OrdinalIgnoreCase.Compare(candidate, latest) > 0)
            {
                latest = candidate;
                latestDate = candidateDate;
            }
        }

        return latest;
    }

    public static DateTimeOffset? TryParseDate(string? releaseText)
    {
        if (string.IsNullOrWhiteSpace(releaseText))
            return null;

        return DateTimeOffset.TryParse(
            releaseText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset parsed)
            ? parsed
            : null;
    }
}
