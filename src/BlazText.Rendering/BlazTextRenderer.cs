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
            html = ResolveImages(html, document.Images, options.ImageResolver);
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

        var context = options.LiquidContext
            ?? new TemplateContext(new TemplateOptions { MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance });

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

    private static string ResolveImages(string html, IEnumerable<EmbeddedImage> images, Func<EmbeddedImage, string>? resolver)
    {
        // Longest id first: ids are settable, so one can be a prefix of another. Replacing
        // "blaztext:logo" before "blaztext:logo2" would eat the longer reference's prefix and
        // leave the second image pointing at the first image's data with a stray "2" appended.
        foreach (var image in images.OrderByDescending(i => i.Id.Length))
        {
            var reference = BlazTextImageUri.Create(image.Id);
            if (html.Contains(reference, StringComparison.OrdinalIgnoreCase))
            {
                html = html.Replace(reference, resolver?.Invoke(image) ?? image.ToDataUri(), StringComparison.OrdinalIgnoreCase);
            }
        }

        return html;
    }
}
