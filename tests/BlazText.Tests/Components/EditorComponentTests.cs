using BlazText.Liquid;
using BlazText.Models;
using BlazText.Plugins;
using Bunit;

namespace BlazText.Tests.Components;

public class EditorComponentTests : TestContext
{
    public EditorComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js").Mode = JSRuntimeMode.Loose;
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

    [Fact]
    public async Task Autocomplete_with_a_non_positive_item_cap_stays_closed()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p
            .AddChildContent<AutoCompletePlugin>(a => a.Add(x => x.MaxItems, 0)));

        await cut.InvokeAsync(() => cut.Instance.Context.RegisterSuggestionProvider(new StubSuggestionProvider()));
        await cut.InvokeAsync(() => cut.Instance.NotifyContentChangedAsync("<p>us</p>", "us", new CaretRect(10, 10, 20)));

        // Without the guard the popup opens with zero items, still intercepts the arrow keys,
        // and then divides by _items.Count.
        await cut.InvokeAsync(() => cut.Instance.NotifyKeyInterceptedAsync("ArrowDown"));

        Assert.Empty(cut.FindAll(".blaztext-autocomplete"));
    }

    private sealed class StubSuggestionProvider : ISuggestionProvider
    {
        public Task<SuggestionResult?> GetSuggestionsAsync(SuggestionRequest request) =>
            Task.FromResult<SuggestionResult?>(new SuggestionResult([new Suggestion("user", "user")], 2));
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
