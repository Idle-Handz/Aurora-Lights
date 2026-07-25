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
}
