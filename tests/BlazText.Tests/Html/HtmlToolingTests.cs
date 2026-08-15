using BlazText.Html;

namespace BlazText.Tests.Html;

public class HtmlToolingTests
{
    [Fact]
    public void Valid_html_produces_no_issues()
    {
        var result = HtmlTooling.Validate("<p>Hello <b>world</b></p>");

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Malformed_html_reports_issues_with_positions()
    {
        var result = HtmlTooling.Validate("<p>Hello <b>world</i></p>");

        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, i => Assert.True(i.Line >= 1));
    }

    [Fact]
    public void Issue_positions_are_relative_to_the_supplied_html()
    {
        const string html = "<p>Hello <b>world</i></p>";

        var result = HtmlTooling.Validate(html);

        // Positions are shown to the user next to their own source, so they have to index into
        // that source — not into the wrapper document the parser is handed internally.
        // Column 18 is the "</i>" and column 22 the "</p>".
        Assert.All(result.Issues, i => Assert.InRange(i.Column, 1, html.Length));
        Assert.Equal([18, 22], result.Issues.Select(i => i.Column));
        Assert.All(result.Issues, i => Assert.Equal(1, i.Line));
    }

    [Fact]
    public void Format_pretty_prints_nested_markup()
    {
        var formatted = HtmlTooling.Format("<div><p>Hi</p></div>");

        Assert.Contains("\n", formatted);
        Assert.Contains("<p>Hi</p>", formatted);
    }

    [Fact]
    public void Sanitize_strips_active_content_but_keeps_styles()
    {
        var sanitized = HtmlTooling.Sanitize(
            "<style>p{color:red}</style><script>alert(1)</script><p onclick=\"x()\">Hi</p><a href=\"javascript:x()\">link</a>");

        Assert.Contains("<style>", sanitized);
        Assert.DoesNotContain("<script>", sanitized);
        Assert.DoesNotContain("onclick", sanitized);
        Assert.DoesNotContain("javascript:", sanitized);
        Assert.Contains("<p>Hi</p>", sanitized);
    }
}
