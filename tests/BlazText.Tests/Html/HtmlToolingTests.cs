using AngleSharp.Html.Parser;
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
        Assert.DoesNotContain("http-equiv", sanitized);
        Assert.Contains("<p>Hi</p>", sanitized);
    }

    [Fact]
    public void Sanitize_keeps_meta_that_the_email_preview_depends_on()
    {
        var sanitized = HtmlTooling.Sanitize(
            "<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><p>Hi</p>");

        // EmailPreviewPlugin renders a 375px-wide mobile preview; without the viewport meta it
        // no longer reflects the sent e-mail, which is the fidelity that plugin exists for.
        Assert.Contains("viewport", sanitized);
        Assert.Contains("charset", sanitized);
    }

    [Theory]
    // mXSS: markup that is inert as parsed here, but becomes live when the output is parsed
    // again by a browser. Grepping the output string is not enough to catch these — the
    // assertion has to re-parse the way a consumer would.
    [InlineData("<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\">x</p></noscript>")]
    [InlineData("<svg><style>&lt;/style&gt;&lt;img src=x onerror=alert(1)&gt;</style></svg>")]
    [InlineData("<template><script>alert(1)</script><img src=\"x\" onerror=\"alert(1)\"></template>")]
    [InlineData("<template><template><img src=\"x\" onerror=\"alert(1)\"></template></template>")]
    public void Sanitize_output_is_inert_when_reparsed_by_a_browser(string html)
    {
        var sanitized = HtmlTooling.Sanitize(html);

        var reparsed = new HtmlParser(new HtmlParserOptions { IsScripting = true })
            .ParseDocument($"<!DOCTYPE html><html><body>{sanitized}</body></html>");

        Assert.Empty(reparsed.QuerySelectorAll("script"));
        Assert.All(
            reparsed.QuerySelectorAll("*"),
            e => Assert.DoesNotContain(e.Attributes, a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)));

        // QuerySelectorAll does not descend into a template's content fragment, so the two
        // assertions above are blind to anything hidden in one. The element has to be gone.
        Assert.Empty(reparsed.QuerySelectorAll("template"));
    }

    [Theory]
    // srcset is a comma-separated candidate list and ping a space-separated one, so a payload
    // in any position but the first is invisible to a whole-value prefix check.
    [InlineData("<img srcset=\"ok.png 1x, javascript:alert(1) 2x\">")]
    [InlineData("<a ping=\"https://ok.example javascript:alert(1)\" href=\"#\">x</a>")]
    public void Sanitize_checks_every_candidate_in_a_url_list(string html)
    {
        var sanitized = HtmlTooling.Sanitize(html);

        Assert.DoesNotContain("javascript:", sanitized, StringComparison.OrdinalIgnoreCase);
    }
}
