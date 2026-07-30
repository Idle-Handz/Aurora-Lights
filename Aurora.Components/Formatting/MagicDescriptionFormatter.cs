using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Components.Formatting;

/// <summary>
/// Converts Aurora's HTML-like element descriptions into a small, safe subset of HTML that can be
/// rendered by the shared clients. Attributes are discarded unless they belong to a narrowly
/// validated structural or presentational allowlist because content packs are user-supplied.
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

    private static readonly HashSet<string> AllowedTextAlignments = new(StringComparer.OrdinalIgnoreCase)
    {
        "left", "right", "center", "justify", "start", "end",
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
        IReadOnlyDictionary<string, string> attributes = ParseAttributes(sourceToken);

        string alignment = GetSafeTextAlignment(attributes);
        if (!string.IsNullOrEmpty(alignment))
        {
            builder.Append(" data-rich-align=\"")
                .Append(alignment)
                .Append('"');
        }

        if (name is "td" or "th")
        {
            foreach (string attributeName in new[] { "colspan", "rowspan" })
            {
                if (!attributes.TryGetValue(attributeName, out string? rawValue)
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

    private static string GetSafeTextAlignment(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (string attributeName in new[] { "data-rich-align", "align" })
        {
            if (attributes.TryGetValue(attributeName, out string? value)
                && AllowedTextAlignments.Contains(value.Trim()))
            {
                return value.Trim().ToLowerInvariant();
            }
        }

        if (!attributes.TryGetValue("style", out string? style))
            return string.Empty;

        foreach (string declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = declaration.IndexOf(':');
            if (separator <= 0
                || !declaration[..separator].Trim().Equals(
                    "text-align",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = declaration[(separator + 1)..].Trim();
            return AllowedTextAlignments.Contains(value)
                ? value.ToLowerInvariant()
                : string.Empty;
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ParseAttributes(string sourceToken)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int position = 0;

        if (position < sourceToken.Length && sourceToken[position] == '<')
            position++;
        SkipWhitespace(sourceToken, ref position);
        if (position < sourceToken.Length && sourceToken[position] == '/')
            position++;
        SkipWhitespace(sourceToken, ref position);

        while (position < sourceToken.Length
               && !char.IsWhiteSpace(sourceToken[position])
               && sourceToken[position] is not '>' and not '/')
        {
            position++;
        }

        while (position < sourceToken.Length)
        {
            SkipWhitespace(sourceToken, ref position);
            if (position >= sourceToken.Length || sourceToken[position] is '>' or '/')
                break;

            int nameStart = position;
            while (position < sourceToken.Length
                   && !char.IsWhiteSpace(sourceToken[position])
                   && sourceToken[position] is not '=' and not '>' and not '/')
            {
                position++;
            }

            if (position == nameStart)
            {
                position++;
                continue;
            }

            string name = sourceToken[nameStart..position];
            SkipWhitespace(sourceToken, ref position);
            string value = string.Empty;

            if (position < sourceToken.Length && sourceToken[position] == '=')
            {
                position++;
                SkipWhitespace(sourceToken, ref position);
                if (position < sourceToken.Length
                    && sourceToken[position] is '"' or '\'')
                {
                    char quote = sourceToken[position];
                    position++;
                    int valueStart = position;
                    while (position < sourceToken.Length && sourceToken[position] != quote)
                        position++;
                    value = sourceToken[valueStart..position];
                    if (position < sourceToken.Length)
                        position++;
                }
                else
                {
                    int valueStart = position;
                    while (position < sourceToken.Length
                           && !char.IsWhiteSpace(sourceToken[position])
                           && sourceToken[position] is not '>' and not '/')
                    {
                        position++;
                    }

                    value = sourceToken[valueStart..position];
                }
            }

            attributes.TryAdd(name, value);
        }

        return attributes;
    }

    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position]))
            position++;
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

    [GeneratedRegex(@"\n\s*\n", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreakRegex();
}
