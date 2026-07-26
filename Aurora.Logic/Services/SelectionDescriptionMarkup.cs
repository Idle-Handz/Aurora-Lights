using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Builder.Presentation.Services;

/// <summary>
/// Adds a compact level-by-level feature summary to class-like picker descriptions when the
/// source content does not already provide one. The summary is derived from the same grant rules
/// the progression engine processes, so imported classes and subclasses receive the same treatment
/// as bundled content.
/// </summary>
public static partial class SelectionDescriptionMarkup
{
    private static readonly HashSet<string> ClassLikeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class",
        "Multiclass",
        "Archetype",
    };

    private static readonly HashSet<string> FeatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class Feature",
        "Archetype Feature",
    };

    public static string WithFeatureProgression(ElementBase element, string? descriptionMarkup)
    {
        string markup = descriptionMarkup?.Trim() ?? string.Empty;
        if (!ClassLikeTypes.Contains(element.Type) || HasFeatureProgression(markup))
            return markup;

        ElementBase progressionOwner = ResolveProgressionOwner(element);
        IReadOnlyList<SelectionFeatureLevel> levels = BuildFeatureProgression(progressionOwner);
        if (levels.Count == 0)
            return markup;

        var builder = new StringBuilder(markup.Length + 256);
        if (!string.IsNullOrWhiteSpace(markup))
            builder.Append(markup);

        builder.Append("<h5>Features by Level</h5>");
        builder.Append("<table><thead><tr><th>Level</th><th>Features</th></tr></thead><tbody>");
        foreach (SelectionFeatureLevel level in levels)
        {
            builder.Append("<tr><td>")
                .Append(FormatOrdinal(level.Level))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(string.Join(", ", level.Features)))
                .Append("</td></tr>");
        }

        builder.Append("</tbody></table>");
        return builder.ToString();
    }

    public static bool HasFeatureProgression(string? descriptionMarkup)
    {
        if (string.IsNullOrWhiteSpace(descriptionMarkup))
            return false;

        foreach (Match block in ProgressionBlockRegex().Matches(descriptionMarkup))
        {
            string plain = WebUtility.HtmlDecode(HtmlTagRegex().Replace(block.Value, " "));
            if (plain.Contains("level", StringComparison.OrdinalIgnoreCase)
                && plain.Contains("feature", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<SelectionFeatureLevel> BuildFeatureProgression(ElementBase element)
    {
        var featuresByLevel = new SortedDictionary<int, SortedSet<string>>();

        void AddFeature(int level, string name)
        {
            if (level < 1 || string.IsNullOrWhiteSpace(name))
                return;

            if (!featuresByLevel.TryGetValue(level, out SortedSet<string>? names))
            {
                names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                featuresByLevel[level] = names;
            }

            names.Add(name);
        }

        foreach (var rule in element.GetGrantRules())
        {
            int level = rule.Attributes.RequiredLevel;
            if (level < 1 || string.IsNullOrWhiteSpace(rule.Attributes.Name))
                continue;

            ElementBase? feature = DataManager.Current.ElementsCollection.GetElement(rule.Attributes.Name);
            if (feature is null
                || !FeatureTypes.Contains(feature.Type)
                || string.IsNullOrWhiteSpace(feature.Name))
            {
                continue;
            }

            AddFeature(level, feature.Name);
        }

        foreach (var rule in element.GetSelectRules())
        {
            if (FeatureTypes.Contains(rule.Attributes.Type))
                AddFeature(rule.Attributes.RequiredLevel, rule.Attributes.Name);
        }

        return featuresByLevel
            .Select(pair => new SelectionFeatureLevel(pair.Key, pair.Value.ToList()))
            .ToList();
    }

    private static ElementBase ResolveProgressionOwner(ElementBase element)
    {
        if (!element.Type.Equals("Multiclass", StringComparison.OrdinalIgnoreCase))
            return element;

        return DataManager.Current.ElementsCollection
                   .Where(candidate =>
                       string.Equals(candidate.Type, "Class", StringComparison.OrdinalIgnoreCase)
                       && string.Equals(candidate.Name, element.Name, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(candidate =>
                       string.Equals(candidate.Source, element.Source, StringComparison.OrdinalIgnoreCase))
                   .FirstOrDefault()
               ?? element;
    }

    private static string FormatOrdinal(int level)
    {
        int tens = level % 100;
        if (tens is >= 11 and <= 13)
            return $"{level}th";

        return (level % 10) switch
        {
            1 => $"{level}st",
            2 => $"{level}nd",
            3 => $"{level}rd",
            _ => $"{level}th",
        };
    }

    [GeneratedRegex(
        @"<(table|ul|ol)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ProgressionBlockRegex();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();
}

public sealed record SelectionFeatureLevel(int Level, IReadOnlyList<string> Features);
