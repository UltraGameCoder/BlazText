using BlazText.Html;
using BlazText.Models;
using Bunit;

namespace BlazText.Tests.Components;

public class EmailPreviewPluginTests : TestContext
{
    private const string Layout = "<html><body>{{ body }}<p>Sent by {{ company }}.</p></body></html>";

    public EmailPreviewPluginTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/BlazText/BlazTextEditor.razor.js").Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Preview_resolves_drops_used_by_the_layout()
    {
        var cut = RenderPreview(p => p
            .Add(e => e.LayoutContent, Layout)
            .Add(e => e.Drops, new Dictionary<string, object?> { ["company"] = "BlazText Inc." }));

        Assert.Contains("Sent by BlazText Inc..", PreviewHtml(cut));
    }

    [Fact]
    public void Preview_wraps_the_document_in_the_layout()
    {
        var cut = RenderPreview(p => p.Add(e => e.LayoutContent, Layout));

        Assert.Contains("Hello", PreviewHtml(cut));
    }

    private IRenderedComponent<BlazTextEditor> RenderPreview(Action<ComponentParameterCollectionBuilder<EmailPreviewPlugin>> configure)
    {
        var document = new BlazTextDocument { Content = "<p>Hello</p>" };

        var cut = RenderComponent<BlazTextEditor>(p => p
            .Add(e => e.Document, document)
            .AddChildContent(configure));

        cut.WaitForAssertion(() => cut.Find(".blaztext-toolbar button[title='E-mail preview']").Click());
        return cut;
    }

    private static string PreviewHtml(IRenderedComponent<BlazTextEditor> cut)
    {
        var frame = cut.WaitForElement(".blaztext-email-preview-frame");
        return frame.GetAttribute("srcdoc") ?? string.Empty;
    }
}
