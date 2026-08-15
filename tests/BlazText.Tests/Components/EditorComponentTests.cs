using BlazText.Liquid;
using BlazText.Models;
using BlazText.Plugins;
using Bunit;

namespace BlazText.Tests.Components;

public class EditorComponentTests : TestContext
{
    private readonly BunitJSModuleInterop _module;

    public EditorComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Bare_editor_renders_surface_without_toolbar()
    {
        var cut = RenderComponent<BlazTextEditor>();

        Assert.NotNull(cut.Find(".blaztext-surface"));
        Assert.Empty(cut.FindAll(".blaztext-toolbar"));
    }

    [Fact]
    public void Formatting_plugin_contributes_toolbar_buttons()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<BasicFormattingPlugin>());

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".blaztext-toolbar button"));
            Assert.Contains(cut.FindAll(".blaztext-toolbar button"), b => b.GetAttribute("title") == "Bold");
        });
    }

    [Fact]
    public void Search_plugin_contributes_search_box()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<SearchPlugin>());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".blaztext-toolbar input[type=search]")));
    }

    [Fact]
    public void Plugins_are_discoverable_through_the_context()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<SearchPlugin>());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Instance.Context.GetPlugin<SearchPlugin>()));
        Assert.Null(cut.Instance.Context.GetPlugin<ImagePlugin>());
    }

    [Fact]
    public void Liquid_plugin_detects_drops_in_initial_content()
    {
        var document = new BlazTextDocument { Content = "<p>Hi {{ user.name }} from {{ company }}</p>" };

        var cut = RenderComponent<BlazTextEditor>(p => p
            .Add(e => e.Document, document)
            .AddChildContent<LiquidPlugin>());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(document.DetectedDrops, d => d.Path == "user.name");
            Assert.Contains(document.DetectedDrops, d => d.Path == "company");
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Autocomplete_rejects_a_non_positive_item_cap(int maxItems)
    {
        // Silently showing nothing is worse to debug than a loud failure at the point of the
        // mistake, so the misconfiguration is rejected rather than absorbed.
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderComponent<BlazTextEditor>(p => p
            .AddChildContent<AutoCompletePlugin>(a => a.Add(x => x.MaxItems, maxItems))));
    }

    [Fact]
    public async Task Autocomplete_without_a_caret_does_not_intercept_keys()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<AutoCompletePlugin>());

        await cut.InvokeAsync(() => cut.Instance.Context.RegisterSuggestionProvider(new StubSuggestionProvider()));
        // caretRect() returns null whenever the selection sits outside the editing surface.
        await cut.InvokeAsync(() => cut.Instance.NotifyContentChangedAsync("<p>us</p>", "us", null));

        // Popup absence alone is too weak an assertion: an invisible-but-intercepting popup
        // satisfies it. Assert that no keys were handed to the browser for interception.
        Assert.Empty(cut.FindAll(".blaztext-autocomplete"));
        Assert.All(
            _module.Invocations["setInterceptKeys"],
            i => Assert.Empty((string[])i.Arguments[1]!));

        // And that Enter cannot commit an item the user never saw.
        await cut.InvokeAsync(() => cut.Instance.NotifyKeyInterceptedAsync("Enter"));
        Assert.Empty(_module.Invocations["replaceTextBeforeCaret"]);
    }

    [Fact]
    public async Task Autocomplete_arrow_keys_wrap_around_the_item_list()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<AutoCompletePlugin>());

        await cut.InvokeAsync(() => cut.Instance.Context.RegisterSuggestionProvider(
            new StubSuggestionProvider("a", "b", "c")));
        await cut.InvokeAsync(() => cut.Instance.NotifyContentChangedAsync("<p>us</p>", "us", new CaretRect(10, 10, 20)));

        // The modulo arithmetic the crash guard protects is otherwise untested.
        Assert.Equal("a", ActiveItem());
        await cut.InvokeAsync(() => cut.Instance.NotifyKeyInterceptedAsync("ArrowDown"));
        Assert.Equal("b", ActiveItem());
        await cut.InvokeAsync(() => cut.Instance.NotifyKeyInterceptedAsync("ArrowUp"));
        await cut.InvokeAsync(() => cut.Instance.NotifyKeyInterceptedAsync("ArrowUp"));
        Assert.Equal("c", ActiveItem());

        string ActiveItem() => cut.Find(".blaztext-autocomplete-item.active").TextContent.Trim();
    }

    private sealed class StubSuggestionProvider(params string[] labels) : ISuggestionProvider
    {
        public Task<SuggestionResult?> GetSuggestionsAsync(SuggestionRequest request) =>
            Task.FromResult<SuggestionResult?>(new SuggestionResult(
                (labels.Length > 0 ? labels : ["user"]).Select(l => new Suggestion(l, l)).ToList(),
                2));
    }

    [Fact]
    public void Disposing_a_plugin_removes_its_toolbar_item()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<BasicFormattingPlugin>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".blaztext-toolbar button")));

        cut.SetParametersAndRender(p => p.AddChildContent(builder => { }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".blaztext-toolbar button")));
    }
}
