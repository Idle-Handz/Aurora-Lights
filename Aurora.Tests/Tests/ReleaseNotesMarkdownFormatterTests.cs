using Aurora.Components.Formatting;

namespace Aurora.Tests.Tests;

public sealed class ReleaseNotesMarkdownFormatterTests
{
    [Fact]
    public void ToSafeHtml_RendersReleaseNoteStructureAndSafeLinks()
    {
        const string markdown = """
            ## What’s new

            ### Fixed

            - Render **rich item details** correctly.
            - See the [full release](https://example.test/releases/v1.3.0).
            """;

        string html = ReleaseNotesMarkdownFormatter.ToSafeHtml(markdown);

        html.Should().Contain("<h2>What’s new</h2>");
        html.Should().Contain("<ul><li>Render <strong>rich item details</strong> correctly.</li>");
        html.Should().Contain("href=\"https://example.test/releases/v1.3.0\"");
        html.Should().Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void ToSafeHtml_EncodesRemoteHtmlAndRejectsUnsafeLinkSchemes()
    {
        const string markdown = "- <script>alert('nope')</script> [open](javascript:alert('nope'))";

        string html = ReleaseNotesMarkdownFormatter.ToSafeHtml(markdown);

        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<script>");
        html.Should().NotContain("href=");
        html.Should().Contain("[open](javascript:alert(&#39;nope&#39;))");
    }

    [Fact]
    public void GetSummary_UsesFirstReleaseNoteBulletAndRemovesMarkdown()
    {
        const string markdown = """
            ## What’s new

            ### Improved

            - Show **release notes** in the [update notice](https://example.test).
            - A later item.
            """;

        ReleaseNotesMarkdownFormatter.GetSummary(markdown)
            .Should().Be("Show release notes in the update notice.");
    }
}
