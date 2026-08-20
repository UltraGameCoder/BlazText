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
    public void Disposing_a_plugin_removes_its_toolbar_item()
    {
        var cut = RenderComponent<BlazTextEditor>(p => p.AddChildContent<BasicFormattingPlugin>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".blaztext-toolbar button")));

        cut.SetParametersAndRender(p => p.AddChildContent(builder => { }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".blaztext-toolbar button")));
    }
}
