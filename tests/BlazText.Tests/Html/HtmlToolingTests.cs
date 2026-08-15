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
        Assert.Equal([18, 22], result.Issues.Select(i => i.Column));
        Assert.All(result.Issues, i => Assert.Equal(1, i.Line));
        Assert.All(result.Issues, i => Assert.InRange(i.Column, 1, html.Length));
    }

    [Fact]
    public void Issue_positions_on_later_lines_are_not_corrected()
    {
        // The wrapper occupies line 1 only, so lines 2+ must pass through untouched. Without a
        // multi-line case a refactor to a blanket subtraction would pass the whole suite.
        var result = HtmlTooling.Validate("<p>line one</p>\n<p>Hello <b>world</i></p>");

        Assert.Equal([(2, 18), (2, 22)], result.Issues.Select(i => (i.Line, i.Column)));
    }

    [Theory]
    [InlineData("<b>x", 5)]
    [InlineData("<div>", 6)]
    [InlineData("hello <i>there", 15)]
    public void Unclosed_tag_is_reported_one_past_the_end_of_input(string html, int expectedColumn)
    {
        // The problem is at end of input, so the position is one past the last character — the
        // usual EOF convention. Pinned because it is the one case that leaves 1..Length.
        var result = HtmlTooling.Validate(html);

        Assert.All(result.Issues, i => Assert.Equal(expectedColumn, i.Column));
        Assert.Equal(html.Length + 1, expectedColumn);
    }

    [Fact]
    public void Empty_content_produces_no_issues()
    {
        Assert.Empty(HtmlTooling.Validate(string.Empty).Issues);
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
