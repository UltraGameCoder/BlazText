using BlazText.Liquid;
using BlazText.Models;
using BlazText.Plugins;
using Bunit;
using Microsoft.JSInterop;

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

    [Fact]
    public void Disposing_a_plugin_removes_its_toolbar_item()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<BasicFormattingPlugin>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".blaztext-toolbar button")));

        cut.SetParametersAndRender(p => p.AddChildContent(builder => { }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".blaztext-toolbar button")));
    }
}
