using BlazText.Html;
using BlazText.Models;
using Bunit;
using Fluid;

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

    [Fact]
    public void Preview_cannot_read_an_unregistered_type_without_template_options()
    {
        // Values alone are not enough: the default strategy allows dictionaries and registered
        // types only, so the member renders empty rather than throwing.
        var cut = RenderPreview(p => p
            .Add(e => e.LayoutContent, "<html><body>{{ body }}<p>To {{ user.name }}.</p></body></html>")
            .Add(e => e.Drops, new Dictionary<string, object?> { ["user"] = new PreviewRecipient("Ada") }));

        Assert.Contains("To .", PreviewHtml(cut));
    }

    [Fact]
    public void Preview_reads_a_registered_type_when_given_the_backend_template_options()
    {
        // Passing the same TemplateOptions the backend renders with is what keeps the preview
        // and the sent e-mail agreeing about which drops resolve.
        var options = new TemplateOptions();
        options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
        options.MemberAccessStrategy.Register<PreviewRecipient>();

        var cut = RenderPreview(p => p
            .Add(e => e.LayoutContent, "<html><body>{{ body }}<p>To {{ user.name }}.</p></body></html>")
            .Add(e => e.Drops, new Dictionary<string, object?> { ["user"] = new PreviewRecipient("Ada") })
            .Add(e => e.LiquidTemplateOptions, options));

        Assert.Contains("To Ada.", PreviewHtml(cut));
    }

    private sealed record PreviewRecipient(string Name);

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
