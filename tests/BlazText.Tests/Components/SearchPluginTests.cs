using BlazText;
using BlazText.Plugins;
using Bunit;

namespace BlazText.Tests.Components;

public class SearchPluginTests : TestContext
{
    private readonly BunitJSModuleInterop _module;

    public SearchPluginTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<BlazTextEditor> RenderWithText(string plainText, int resolved)
    {
        _module.Setup<string>("getPlainText", _ => true).SetResult(plainText);
        // How many ranges the browser could map back onto live text nodes.
        _module.Setup<int>("highlightRanges", _ => true).SetResult(resolved);

        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<SearchPlugin>());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("input[type=search]")));
        return cut;
    }

    private IReadOnlyList<TextRange> LastRanges() =>
        (IReadOnlyList<TextRange>)_module.Invocations["highlightRanges"].Last().Arguments[1]!;

    private int LastActiveIndex() => (int)_module.Invocations["highlightRanges"].Last().Arguments[2]!;

    [Fact]
    public void Overlapping_matches_are_all_found()
    {
        var cut = RenderWithText("aaa", resolved: 2);

        cut.Find("input[type=search]").Input("aa");

        // "aa" occurs twice in "aaa" once overlaps are counted.
        cut.WaitForAssertion(() => Assert.Equal([new TextRange(0, 2), new TextRange(1, 2)], LastRanges()));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    public void Next_wraps_around_the_match_list(int presses, int expectedActive)
    {
        var cut = RenderWithText("a a a", resolved: 3);
        cut.Find("input[type=search]").Input("a");

        for (var i = 0; i < presses; i++)
        {
            cut.FindAll("button").Single(b => b.GetAttribute("title") == "Next match").Click();
        }

        cut.WaitForAssertion(() => Assert.Equal(expectedActive, LastActiveIndex()));
    }

    [Fact]
    public void Previous_from_the_first_match_wraps_to_the_last()
    {
        var cut = RenderWithText("a a a", resolved: 3);
        cut.Find("input[type=search]").Input("a");

        cut.FindAll("button").Single(b => b.GetAttribute("title") == "Previous match").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, LastActiveIndex()));
    }

    [Fact]
    public void Counter_reports_what_resolved_not_what_matched()
    {
        // The DOM moved on between reading the text and highlighting it, so the offsets are
        // stale. The plugin must not claim three hits that nothing on screen backs up.
        var cut = RenderWithText("a a a", resolved: 0);

        cut.Find("input[type=search]").Input("a");

        cut.WaitForAssertion(() => Assert.Equal("0/0", cut.Find(".blaztext-muted").TextContent.Trim()));
    }

    [Fact]
    public void Counter_reflects_a_partial_resolve()
    {
        var cut = RenderWithText("a a a", resolved: 2);

        cut.Find("input[type=search]").Input("a");

        cut.WaitForAssertion(() => Assert.Equal("1/2", cut.Find(".blaztext-muted").TextContent.Trim()));
    }
}
