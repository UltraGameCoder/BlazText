using BlazText.Rendering;

namespace BlazText.Tests.Rendering;

public class LiquidDropDetectorTests
{
    [Fact]
    public void Detects_output_drops_with_paths_and_counts()
    {
        var drops = LiquidDropDetector.Detect("<p>{{ user.name }} and {{ user.name }} / {{ company }}</p>");

        var userName = Assert.Single(drops, d => d.Path == "user.name");
        Assert.Equal("user", userName.Name);
        Assert.Equal(2, userName.Occurrences);

        var company = Assert.Single(drops, d => d.Path == "company");
        Assert.Equal(1, company.Occurrences);
    }

    [Fact]
    public void Ignores_filters_string_literals_and_keywords()
    {
        var drops = LiquidDropDetector.Detect("{{ user.name | upcase | default: 'guest' }}{% if user.age and true %}x{% endif %}");

        Assert.Equal(["user.age", "user.name"], drops.Select(d => d.Path));
    }

    [Fact]
    public void Excludes_loop_and_assigned_variables_but_keeps_their_sources()
    {
        var drops = LiquidDropDetector.Detect("{% assign top = items %}{% for item in orders.lines %}{{ item.total }}{% endfor %}");

        Assert.Equal(["items", "orders.lines"], drops.Select(d => d.Path));
    }

    [Fact]
    public void Empty_or_plain_content_yields_nothing()
    {
        Assert.Empty(LiquidDropDetector.Detect(null));
        Assert.Empty(LiquidDropDetector.Detect("<p>no liquid here</p>"));
    }
}
