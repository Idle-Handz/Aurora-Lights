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
    public void FromAuroraHtmlPreservesTableStructureButDropsTableAttributes()
    {
        const string source =
            "<table class=\"class-features\" style=\"width: 100%\"><thead><tr><th>Level</th><th>Feature</th></tr></thead><tbody><tr><td>1</td><td>Spellcasting</td></tr></tbody></table>";

        string html = MagicDescriptionFormatter.FromAuroraHtml(source);

        html.Should().Be(
            "<table><thead><tr><th>Level</th><th>Feature</th></tr></thead><tbody><tr><td>1</td><td>Spellcasting</td></tr></tbody></table>");
        html.Should().NotContain("class=");
        html.Should().NotContain("style=");
    }
}
