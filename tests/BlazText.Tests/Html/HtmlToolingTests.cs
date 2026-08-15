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

    [Theory]
    // A browser ignores whitespace and control characters inside the scheme, so the check has to too.
    [InlineData("<a href=\"java\tscript:x()\">link</a>")]
    [InlineData("<a href=\"java\nscript:x()\">link</a>")]
    [InlineData("<a href=\"  JaVaScRiPt:x()\">link</a>")]
    // Script URLs are not exclusive to href/src.
    [InlineData("<button formaction=\"javascript:x()\">go</button>")]
    [InlineData("<video poster=\"javascript:x()\"></video>")]
    [InlineData("<svg><a xlink:href=\"javascript:x()\">link</a></svg>")]
    public void Sanitize_strips_obfuscated_and_non_href_script_urls(string html)
    {
        var sanitized = HtmlTooling.Sanitize(html);

        Assert.DoesNotContain("script:", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_keeps_inline_image_data_uris()
    {
        var sanitized = HtmlTooling.Sanitize("<img src=\"data:image/png;base64,iVBORw0KGgo=\">");

        Assert.Contains("data:image/png;base64,iVBORw0KGgo=", sanitized);
    }

    [Fact]
    public void Sanitize_removes_external_resource_and_refresh_tags()
    {
        var sanitized = HtmlTooling.Sanitize(
            "<link rel=\"stylesheet\" href=\"https://attacker.example/x.css\"><meta http-equiv=\"refresh\" content=\"0\"><p>Hi</p>");

        Assert.DoesNotContain("<link", sanitized);
        Assert.DoesNotContain("<meta", sanitized);
        Assert.Contains("<p>Hi</p>", sanitized);
    }
}
