using AngleSharp.Html.Parser;
using BlazText.Liquid;
using BlazText.Models;
using BlazText.Plugins;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

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
    // Circuit teardown disposes the JS runtime, which surfaces as none of these being
    // JSDisconnectedException — the original bug was catching only that one.
    [InlineData("ObjectDisposed")]
    [InlineData("Disconnected")]
    [InlineData("Cancelled")]
    public async Task Disposing_the_editor_releases_the_dotnet_reference_when_js_teardown_fails(string failure)
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("dispose", _ => true).SetException<Exception>(failure switch
        {
            "ObjectDisposed" => new ObjectDisposedException("JSRuntime"),
            "Disconnected" => new JSDisconnectedException("circuit gone"),
            _ => new TaskCanceledException(),
        });

        var cut = RenderComponent<BlazTextEditor>();
        var selfRef = (DotNetObjectReference<BlazTextEditor>)module.Invocations["init"].Single().Arguments[1]!;

        await cut.Instance.DisposeAsync();

        // "Did not throw" is not the invariant — the reference must actually be released, or
        // it stays rooted in the JS runtime's object table along with the editor it captures.
        Assert.Throws<ObjectDisposedException>(() => _ = selfRef.Value);
    }

    [Fact]
    public async Task An_unexpected_teardown_failure_still_propagates()
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("dispose", _ => true).SetException(new InvalidOperationException("boom"));

        var cut = RenderComponent<BlazTextEditor>();
        var selfRef = (DotNetObjectReference<BlazTextEditor>)module.Invocations["init"].Single().Arguments[1]!;

        // Guards against someone later widening the filter to catch (Exception).
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await cut.Instance.DisposeAsync());

        // …and the reference is released on that path too, because it is in a finally.
        Assert.Throws<ObjectDisposedException>(() => _ = selfRef.Value);
    }

    [Fact]
    public async Task Disposing_the_editor_twice_is_a_no_op()
    {
        var cut = RenderComponent<BlazTextEditor>();

        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();
    }

    [Fact]
    public void Inserted_image_html_encodes_the_file_name()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<ImagePlugin>());
        var input = cut.FindComponent<InputFile>();

        input.UploadFiles(InputFileContent.CreateFromBinary(
            [1, 2, 3],
            "a\" onerror=\"alert(1).png",
            null,
            "image/png"));

        var html = (string)_module.Invocations["insertHtml"].Single().Arguments[1]!;

        Assert.Contains("alt=\"a&quot; onerror=&quot;alert(1).png\"", html);
        AssertNoEventHandlers(html);
    }
    [Fact]
    public void File_name_with_an_ampersand_is_encoded_exactly_once()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<ImagePlugin>());

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary([1], "kuroko @ kopie & fun.gif", null, "image/gif"));

        var html = (string)_module.Invocations["insertHtml"].Single().Arguments[1]!;

        // & is the character where over- and under-encoding both look plausible in a source
        // view: &amp; is a correctly serialized literal &, while &amp;amp; would mean the value
        // was encoded twice. The benign-name test cannot catch either, having nothing to encode.
        Assert.Contains("alt=\"kuroko @ kopie &amp; fun.gif\"", html);
        Assert.DoesNotContain("&amp;amp;", html);

        // The assertion that actually matters: it parses back to the name that was uploaded.
        var document = new HtmlParser().ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");
        Assert.Equal("kuroko @ kopie & fun.gif", document.QuerySelector("img")!.GetAttribute("alt"));
    }


    [Fact]
    public void Benign_file_name_survives_without_entity_noise()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<ImagePlugin>());

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary([1], "my photo (1).png", null, "image/png"));

        var html = (string)_module.Invocations["insertHtml"].Single().Arguments[1]!;

        // Guards against someone swapping in an over-aggressive encoder.
        Assert.Contains("alt=\"my photo (1).png\"", html);
    }

    [Fact]
    public void Hostile_content_type_is_rejected_rather_than_emitted()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<ImagePlugin>());

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(
            [1], "ok.png", null, "image/png\" onerror=\"alert(1)"));

        // A prefix check on "image/" accepts this; the upload must not reach the document at all.
        Assert.Empty(_module.Invocations["insertHtml"]);
        Assert.Equal("Not an image file.", cut.Find("[role=alert]").TextContent);
    }

    /// <summary>Parses the emitted HTML and asserts no element carries an on* handler.</summary>
    private static void AssertNoEventHandlers(string html)
    {
        var document = new HtmlParser().ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");

        Assert.All(
            document.QuerySelectorAll("*"),
            e => Assert.DoesNotContain(e.Attributes, a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Disposing_a_plugin_survives_a_disconnected_circuit()
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        // SearchPlugin clears its highlights in OnDisposingAsync. On a circuit that is being
        // torn down, that interop call reaches a browser which is already gone.
        module.SetupVoid("clearHighlights", _ => true).SetException(new JSDisconnectedException("circuit gone"));

        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<SearchPlugin>());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".blaztext-toolbar input[type=search]")));

        cut.SetParametersAndRender(p => p.AddChildContent(builder => { }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".blaztext-toolbar")));
    }

    [Theory]
    // The value-returning members bypassed the guarded helper entirely, and docs/extending.md
    // presents GetContentAsync/SetContentAsync as *the* plugin surface.
    [InlineData("getContent")]
    [InlineData("getPlainText")]
    [InlineData("setContent")]
    [InlineData("clearHighlights")]
    public async Task Api_calls_on_a_dead_circuit_do_not_throw(string method)
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string>(method, _ => true).SetException(new JSDisconnectedException("circuit gone"));
        module.SetupVoid(method, _ => true).SetException(new JSDisconnectedException("circuit gone"));

        var cut = RenderComponent<BlazTextEditor>();
        var api = cut.Instance.Api;

        await (method switch
        {
            "getContent" => api.GetContentAsync(),
            "getPlainText" => api.GetPlainTextAsync(),
            "setContent" => api.SetContentAsync("<p>x</p>"),
            _ => api.ClearHighlightsAsync(),
        });
    }

    [Fact]
    public async Task Api_falls_back_to_the_known_content_when_the_circuit_is_gone()
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string>("getContent", _ => true).SetException(new JSDisconnectedException("circuit gone"));

        var document = new BlazTextDocument { Content = "<p>known</p>" };
        var cut = RenderComponent<BlazTextEditor>(p => p.Add(e => e.Document, document));

        Assert.Equal("<p>known</p>", await cut.Instance.Api.GetContentAsync());
    }

    [Fact]
    public async Task A_module_error_on_a_live_circuit_still_surfaces()
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("applyFormat", _ => true).SetException(new JSException("bug in the module"));

        var cut = RenderComponent<BlazTextEditor>();

        // An explicit design commitment: a genuine module bug is not a disconnect, and this
        // pins it so a later widening to catch (Exception) cannot pass CI silently.
        await Assert.ThrowsAsync<JSException>(() => cut.Instance.Api.ApplyFormatAsync("bold"));
    }

    [Fact]
    public async Task A_timed_out_call_on_a_live_circuit_still_surfaces()
    {
        var module = JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("applyFormat", _ => true).SetException(new TaskCanceledException());

        var cut = RenderComponent<BlazTextEditor>();

        // The interop timeout fires on a slow-but-alive browser. Absorbing it outside teardown
        // would turn a congested circuit into an unbounded silent no-op.
        await Assert.ThrowsAsync<TaskCanceledException>(() => cut.Instance.Api.ApplyFormatAsync("bold"));
    }

    [Fact]
    public async Task Api_calls_after_disposal_are_skipped()
    {
        var cut = RenderComponent<BlazTextEditor>();
        var api = cut.Instance.Api;
        await cut.Instance.DisposeAsync();

        // Exercises the _disposed fast path rather than the catch: no interop is attempted.
        await api.ApplyFormatAsync("bold");
        Assert.Equal(string.Empty, await api.GetPlainTextAsync());
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
