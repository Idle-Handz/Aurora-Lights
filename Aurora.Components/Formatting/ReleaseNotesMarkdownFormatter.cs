using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Components.Formatting;

/// <summary>
/// Renders the small Markdown subset used by GitHub release bodies without allowing
/// remote HTML or unsafe link schemes into the Blazor render tree.
/// </summary>
public static partial class ReleaseNotesMarkdownFormatter
{
    public static string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var builder = new StringBuilder(normalized.Length + 64);
        string? activeList = null;

        foreach (string rawLine in normalized.Split('\n'))
        {
            string line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                CloseList(builder, ref activeList);
                continue;
            }

            Match heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                CloseList(builder, ref activeList);
                int level = heading.Groups["level"].Value.Length;
                builder.Append('<').Append('h').Append(level).Append('>');
                AppendInline(builder, heading.Groups["text"].Value);
                builder.Append("</h").Append(level).Append('>');
                continue;
            }

            Match unordered = UnorderedListRegex().Match(line);
            if (unordered.Success)
            {
                EnsureList(builder, ref activeList, "ul");
                builder.Append("<li>");
                AppendInline(builder, unordered.Groups["text"].Value);
                builder.Append("</li>");
                continue;
            }

            Match ordered = OrderedListRegex().Match(line);
            if (ordered.Success)
            {
                EnsureList(builder, ref activeList, "ol");
                builder.Append("<li>");
                AppendInline(builder, ordered.Groups["text"].Value);
                builder.Append("</li>");
                continue;
            }

            CloseList(builder, ref activeList);

            Match quote = BlockquoteRegex().Match(line);
            if (quote.Success)
            {
                builder.Append("<blockquote>");
                AppendInline(builder, quote.Groups["text"].Value);
                builder.Append("</blockquote>");
            }
            else if (HorizontalRuleRegex().IsMatch(line))
            {
                builder.Append("<hr />");
            }
            else
            {
                builder.Append("<p>");
                AppendInline(builder, line.Trim());
                builder.Append("</p>");
            }
        }

        CloseList(builder, ref activeList);
        return builder.ToString();
    }

    public static string GetSummary(string? markdown, int maxLength = 180)
    {
        if (string.IsNullOrWhiteSpace(markdown) || maxLength < 1)
            return string.Empty;

        string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string? fallback = null;

        foreach (string rawLine in normalized.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || HeadingRegex().IsMatch(line))
                continue;

            Match bullet = UnorderedListRegex().Match(line);
            string candidate = bullet.Success
                ? bullet.Groups["text"].Value
                : OrderedListRegex().Match(line) is { Success: true } ordered
                    ? ordered.Groups["text"].Value
                    : BlockquoteRegex().Match(line) is { Success: true } quote
                        ? quote.Groups["text"].Value
                        : line;

            candidate = PlainTextLinkRegex().Replace(candidate, "$1");
            candidate = InlineMarkdownDelimiterRegex().Replace(candidate, string.Empty);
            candidate = WebUtility.HtmlDecode(candidate).Trim();

            if (candidate.Length == 0)
                continue;

            if (bullet.Success)
                return Truncate(candidate, maxLength);

            fallback ??= candidate;
        }

        return Truncate(fallback ?? string.Empty, maxLength);
    }

    private static void EnsureList(StringBuilder builder, ref string? activeList, string requestedList)
    {
        if (activeList == requestedList)
            return;

        CloseList(builder, ref activeList);
        builder.Append('<').Append(requestedList).Append('>');
        activeList = requestedList;
    }

    private static void CloseList(StringBuilder builder, ref string? activeList)
    {
        if (activeList is null)
            return;

        builder.Append("</").Append(activeList).Append('>');
        activeList = null;
    }

    private static void AppendInline(StringBuilder builder, string text)
    {
        int position = 0;
        foreach (Match link in MarkdownLinkRegex().Matches(text))
        {
            AppendFormattedText(builder, text[position..link.Index]);

            string url = link.Groups["url"].Value;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                && uri.Scheme is "http" or "https")
            {
                builder.Append("<a href=\"")
                    .Append(WebUtility.HtmlEncode(uri.AbsoluteUri))
                    .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">");
                AppendFormattedText(builder, link.Groups["label"].Value);
                builder.Append("</a>");
            }
            else
            {
                AppendFormattedText(builder, link.Value);
            }

            position = link.Index + link.Length;
        }

        AppendFormattedText(builder, text[position..]);
    }

    private static void AppendFormattedText(StringBuilder builder, string text)
    {
        if (text.Length == 0)
            return;

        string encoded = WebUtility.HtmlEncode(text);
        encoded = InlineCodeRegex().Replace(encoded, "<code>$1</code>");
        encoded = BoldAsteriskRegex().Replace(encoded, "<strong>$1</strong>");
        encoded = BoldUnderscoreRegex().Replace(encoded, "<strong>$1</strong>");
        builder.Append(encoded);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : value[..Math.Max(1, maxLength - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<text>.+?)\s*#*\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex UnorderedListRegex();

    [GeneratedRegex(@"^\s*\d+[.)]\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^\s*>\s?(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^\s*(?:-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"\[(?<label>[^\]\r\n]+)\]\((?<url>[^)\s]+)(?:\s+""[^""\r\n]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\[(?<label>[^\]\r\n]+)\]\([^)\r\n]+\)", RegexOptions.CultureInvariant)]
    private static partial Regex PlainTextLinkRegex();

    [GeneratedRegex(@"\x60(?<text>[^\x60\r\n]+)\x60", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(?<text>.+?)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex BoldAsteriskRegex();

    [GeneratedRegex(@"__(?<text>.+?)__", RegexOptions.CultureInvariant)]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"(?:\*\*|__|\x60)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineMarkdownDelimiterRegex();
}
