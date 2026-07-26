using Aurora.Components.Formatting;

namespace Aurora.Tests.Tests;

public sealed class MagicDescriptionFormatterTests
{
    [Fact]
    public void FromAuroraHtmlPreservesStructureButDropsUnsafeMarkupAndAttributes()
    {
        const string source = """
            <p class="lead">A <strong>formatted</strong> paragraph.</p>
            <ul><li>First</li><li>Second</li></ul>
            <script>alert('nope')</script>
            """;

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Contain("<p>A <strong>formatted</strong> paragraph.</p>");
        html.Should().Contain("<ul><li>First</li><li>Second</li></ul>");
        html.Should().NotContain("class=");
        html.Should().NotContain("<script");
        html.Should().NotContain("alert");
    }

    [Fact]
    public void FromPlainTextKeepsParagraphAndLineBreakBoundaries()
    {
        string html = MagicDescriptionFormatter.FromPlainText("First line\nsecond line\n\nNext paragraph");

        html.Should().Be("<p>First line<br />second line</p><p>Next paragraph</p>");
    }

    [Fact]
    public void FromAuroraHtmlPreservesSafeTableSpansButDropsOtherTableAttributes()
    {
        const string source =
            "<table class=\"class-features\" style=\"width: 100%; text-align: center; margin-top: 2px\"><thead><tr><td colspan=\"2\" /><th rowspan=\"2\">Level</th><th colspan='9'>Feature</th></tr></thead><tbody><tr><td onclick=\"alert('nope')\">1</td><td>Spellcasting</td><td title=\"colspan=7\" data-rowspan=\"4\">Safe</td></tr></tbody></table>";

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Be(
            "<table data-rich-align=\"center\"><thead><tr><td colspan=\"2\"></td><th rowspan=\"2\">Level</th><th colspan=\"9\">Feature</th></tr></thead><tbody><tr><td>1</td><td>Spellcasting</td><td>Safe</td></tr></tbody></table>");
        html.Should().NotContain("class=");
        html.Should().NotContain("style=");
        html.Should().NotContain("onclick=");
        MagicDescriptionFormatter.FromAuroraHtml(html).Should().Be(html);
    }

    [Fact]
    public void FromAuroraHtmlOnlyPreservesAllowlistedTextAlignment()
    {
        const string source =
            "<div style=\"color: red; text-align: RIGHT; position: fixed\">Right</div><p style=\"text-align: expression(alert('nope'))\">Unsafe</p>";

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Be("<div data-rich-align=\"right\">Right</div><p>Unsafe</p>");
        html.Should().NotContain("style=");
        html.Should().NotContain("position");
        html.Should().NotContain("expression");
        html.Should().NotContain("alert");
        MagicDescriptionFormatter.FromAuroraHtml(html).Should().Be(html);
    }

    [Fact]
    public void FromAuroraHtmlPreservesLegacyCenteringWithoutItsAttributes()
    {
        const string source =
            "<center style=\"color: red\" onclick=\"alert('nope')\"><p>Spell save DC</p></center>";

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Be("<center><p>Spell save DC</p></center>");
        html.Should().NotContain("style=");
        html.Should().NotContain("onclick=");
        MagicDescriptionFormatter.FromAuroraHtml(html).Should().Be(html);
    }

    [Fact]
    public void FromAuroraHtmlDropsLegacyElementReferencePlaceholders()
    {
        const string source =
            "<p>Subclass overview.</p><div element=\"ID_TEST_FEATURE\" /><h5>Features by Level</h5>";

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Be("<p>Subclass overview.</p><h5>Features by Level</h5>");
        html.Should().NotContain("ID_TEST_FEATURE");
        html.Should().NotContain("<div>");
    }
}
