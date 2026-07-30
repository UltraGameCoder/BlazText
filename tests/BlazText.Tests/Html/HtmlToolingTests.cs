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
