using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Components.Formatting;

/// <summary>
/// Converts Aurora's HTML-like element descriptions into a small, safe subset of HTML that can be
/// rendered by the shared clients. Attributes are intentionally discarded because content packs
/// are user-supplied and must not be able to inject event handlers or styles.
/// </summary>
public static partial class MagicDescriptionFormatter
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "span", "center", "br", "hr",
        "ul", "ol", "li",
        "strong", "b", "em", "i", "u", "s",
        "blockquote", "code", "pre", "sub", "sup",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td",
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "hr",
    };

    public static string FromAuroraHtml(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        string withoutUnsafeBlocks = UnsafeBlockRegex().Replace(source, string.Empty);
        string withoutElementReferences = ElementReferenceRegex().Replace(withoutUnsafeBlocks, string.Empty);
        var builder = new StringBuilder(withoutElementReferences.Length);
        int position = 0;
        bool foundAllowedMarkup = false;

        foreach (Match token in TagTokenRegex().Matches(withoutElementReferences))
        {
            AppendEncodedText(builder, withoutElementReferences[position..token.Index]);
            position = token.Index + token.Length;

            Match tag = ParsedTagRegex().Match(token.Value);
            if (!tag.Success)
            {
                continue;
            }

            string name = tag.Groups["name"].Value.ToLowerInvariant();
            if (!AllowedTags.Contains(name))
            {
                continue;
            }

            foundAllowedMarkup = true;
            bool closing = tag.Groups["closing"].Success;
            bool selfClosing = tag.Groups["selfclosing"].Success;
            if (VoidTags.Contains(name))
            {
                builder.Append('<').Append(name).Append(" />");
            }
            else if (closing)
            {
                builder.Append("</").Append(name).Append('>');
            }
            else if (selfClosing)
            {
                AppendOpeningTag(builder, name, token.Value);
                builder.Append("</").Append(name).Append('>');
            }
            else
            {
                AppendOpeningTag(builder, name, token.Value);
            }
        }

        AppendEncodedText(builder, withoutElementReferences[position..]);

        return foundAllowedMarkup
            ? builder.ToString().Trim()
            : FromPlainText(WebUtility.HtmlDecode(withoutElementReferences));
    }

    public static string FromPlainText(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return string.Join(
            string.Empty,
            ParagraphBreakRegex()
                .Split(normalized)
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
                .Select(paragraph =>
                    $"<p>{WebUtility.HtmlEncode(paragraph.Trim()).Replace("\n", "<br />", StringComparison.Ordinal)}</p>"));
    }

    private static void AppendEncodedText(StringBuilder builder, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        builder.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(text)));
    }

    private static void AppendOpeningTag(StringBuilder builder, string name, string sourceToken)
    {
        builder.Append('<').Append(name);
        if (name is "td" or "th")
        {
            var appended = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in TableCellSpanAttributeRegex().Matches(sourceToken))
            {
                string attributeName = attribute.Groups["name"].Value.ToLowerInvariant();
                string rawValue = attribute.Groups["double"].Success
                    ? attribute.Groups["double"].Value
                    : attribute.Groups["single"].Success
                        ? attribute.Groups["single"].Value
                        : attribute.Groups["bare"].Value;

                if (!appended.Add(attributeName)
                    || !int.TryParse(
                        rawValue,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int value)
                    || value is < 1 or > 1000)
                {
                    continue;
                }

                builder.Append(' ')
                    .Append(attributeName)
                    .Append("=\"")
                    .Append(value.ToString(CultureInfo.InvariantCulture))
                    .Append('"');
            }
        }

        builder.Append('>');
    }

    [GeneratedRegex(
        @"<\s*(script|style|iframe|object|embed)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeBlockRegex();

    [GeneratedRegex(
        @"<\s*div\b[^>]*\belement\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)[^>]*/\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ElementReferenceRegex();

    [GeneratedRegex(@"<!--.*?-->|<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TagTokenRegex();

    [GeneratedRegex(
        @"^<\s*(?<closing>/)?\s*(?<name>[A-Za-z][A-Za-z0-9]*)\b[^>]*?(?<selfclosing>/)?\s*>$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParsedTagRegex();

    [GeneratedRegex(
        @"\s+(?<name>colspan|rowspan)\s*=\s*(?:""(?<double>\d+)""|'(?<single>\d+)'|(?<bare>\d+)(?=\s|/?>))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableCellSpanAttributeRegex();

    [GeneratedRegex(@"\n\s*\n", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreakRegex();
}
