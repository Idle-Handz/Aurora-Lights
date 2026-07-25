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
        "p", "div", "span", "br", "hr",
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
        var builder = new StringBuilder(withoutUnsafeBlocks.Length);
        int position = 0;
        bool foundAllowedMarkup = false;

        foreach (Match token in TagTokenRegex().Matches(withoutUnsafeBlocks))
        {
            AppendEncodedText(builder, withoutUnsafeBlocks[position..token.Index]);
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
            if (VoidTags.Contains(name))
            {
                builder.Append('<').Append(name).Append(" />");
            }
            else if (closing)
            {
                builder.Append("</").Append(name).Append('>');
            }
            else
            {
                builder.Append('<').Append(name).Append('>');
            }
        }

        AppendEncodedText(builder, withoutUnsafeBlocks[position..]);

        return foundAllowedMarkup
            ? builder.ToString().Trim()
            : FromPlainText(WebUtility.HtmlDecode(withoutUnsafeBlocks));
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

    [GeneratedRegex(
        @"<\s*(script|style|iframe|object|embed)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeBlockRegex();

    [GeneratedRegex(@"<!--.*?-->|<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TagTokenRegex();

    [GeneratedRegex(
        @"^<\s*(?<closing>/)?\s*(?<name>[A-Za-z][A-Za-z0-9]*)\b[^>]*>$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParsedTagRegex();

    [GeneratedRegex(@"\n\s*\n", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreakRegex();
}
