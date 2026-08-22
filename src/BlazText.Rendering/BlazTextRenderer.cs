using BlazText.Models;
using Fluid;
using PreMailerNet = PreMailer.Net.PreMailer;

namespace BlazText.Rendering;

/// <summary>
/// Turns a <see cref="BlazTextDocument"/> into final HTML: Liquid rendering, embedded image
/// resolution, and (for e-mail) CSS inlining. Blazor-free, so the editor's previews and your
/// backend run the exact same pipeline.
/// </summary>
public static class BlazTextRenderer
{
    private static readonly FluidParser Parser = new();

    public static async Task<RenderResult> RenderAsync(BlazTextDocument document, RenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new RenderOptions();

        var warnings = new List<string>();
        var html = document.Content;

        if (options.RenderLiquid)
        {
            html = await RenderLiquidAsync(html, options, warnings, body: null);
        }

        if (options.LayoutContent is { } layout)
        {
            html = await RenderLiquidAsync(layout, options, warnings, body: html);
        }

        if (options.ResolveImages)
        {
            html = ResolveImageReferences(html, document.Images, options.ImageResolver);
        }

        if (options.InlineCss)
        {
            var inlined = PreMailerNet.MoveCssInline(html, removeStyleElements: options.RemoveStyleElements);
            warnings.AddRange(inlined.Warnings);
            html = inlined.Html;
        }

        return new RenderResult { Html = html, Warnings = warnings };
    }

    private static async Task<string> RenderLiquidAsync(string source, RenderOptions options, List<string> warnings, string? body)
    {
        if (!Parser.TryParse(source, out var template, out var error))
        {
            warnings.Add($"Liquid parse error: {error}");
            return source;
        }

        // Rendering writes into the context, so every render gets its own. Scoping the writes on
        // a shared context instead would only hold sequentially: scope push/pop is stack
        // discipline, and two overlapping renders do not release in LIFO order — one render's
        // release pops the other's scope, and the values fall through to the wrong document.
        // Without a factory, the context is built from LiquidTemplateOptions, which restricts
        // member access to dictionaries and explicitly allowed types.
        var context = options.LiquidContextFactory?.Invoke()
            ?? new TemplateContext(options.LiquidTemplateOptions);

        foreach (var (name, value) in options.LiquidValues)
        {
            context.SetValue(name, value);
        }

        if (body is not null)
        {
            context.SetValue(options.BodyVariableName, body);
        }

        return await template.RenderAsync(context);
    }

    /// <summary>
    /// Replaces every <c>blaztext:{id}</c> reference in <paramref name="html"/> with a real URI.
    /// References to ids not present in <paramref name="images"/> are left alone.
    /// </summary>
    /// <param name="html">Document HTML that may contain <c>blaztext:{id}</c> references.</param>
    /// <param name="images">The images available to resolve against.</param>
    /// <param name="resolver">
    /// How an image becomes a URL. Defaults to inlining it as a data: URI.
    /// </param>
    public static string ResolveImageReferences(
        string html,
        IEnumerable<EmbeddedImage> images,
        Func<EmbeddedImage, string>? resolver = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        var byId = new Dictionary<string, EmbeddedImage>(StringComparer.Ordinal);

        foreach (var image in images)
        {
            // Id is settable and survives a JSON round trip, so it can be null or empty.
            if (!string.IsNullOrEmpty(image.Id))
            {
                byId[image.Id] = image;
            }
        }

        if (byId.Count == 0)
        {
            return html;
        }

        return BlazTextImageUri.ReplaceReferences(
            html,
            id => byId.TryGetValue(id, out var image) ? resolver?.Invoke(image) ?? image.ToDataUri() : null);
    }
}
