using BlazText.Models;
using Fluid;

namespace BlazText.Rendering;

/// <summary>Controls how <see cref="BlazTextRenderer"/> turns a document into final HTML.</summary>
public class RenderOptions
{
    /// <summary>Render Liquid syntax in the document content. On parse failure the raw content is kept and a warning is added.</summary>
    public bool RenderLiquid { get; set; } = true;

    /// <summary>Values made available to Liquid, keyed by drop name (e.g. "user" for <c>{{ user.name }}</c>).</summary>
    public Dictionary<string, object?> LiquidValues { get; set; } = [];

    /// <summary>
    /// Advanced override: a fully configured Fluid <see cref="TemplateContext"/> to render with.
    /// When set, <see cref="LiquidValues"/> are applied on top of it.
    /// </summary>
    public TemplateContext? LiquidContext { get; set; }

    /// <summary>
    /// Optional Liquid layout template wrapping the rendered content, which is exposed to it
    /// as <c>{{ body }}</c> (see <see cref="BodyVariableName"/>). Useful for e-mail layouts
    /// that wrap a separately authored body document.
    /// </summary>
    public string? LayoutContent { get; set; }

    /// <summary>Name of the variable the rendered content is exposed as inside <see cref="LayoutContent"/>.</summary>
    public string BodyVariableName { get; set; } = "body";

    /// <summary>Replace <c>blaztext:{id}</c> image references with real URIs.</summary>
    public bool ResolveImages { get; set; } = true;

    /// <summary>
    /// How an <see cref="EmbeddedImage"/> becomes a URL. Defaults to inlining as a data: URI;
    /// supply your own to upload to a CDN, use cid: attachments, etc.
    /// </summary>
    public Func<EmbeddedImage, string>? ImageResolver { get; set; }

    /// <summary>
    /// Inline CSS rules onto style attributes (PreMailer). Required for e-mail because most
    /// clients strip &lt;style&gt; blocks; leave off for webpage output.
    /// </summary>
    public bool InlineCss { get; set; }

    /// <summary>When inlining CSS, also remove the original &lt;style&gt; elements.</summary>
    public bool RemoveStyleElements { get; set; }

    /// <summary>Preset for e-mail output: Liquid + image resolution + CSS inlining.</summary>
    public static RenderOptions ForEmail() => new() { InlineCss = true };

    /// <summary>Preset for webpage output: Liquid + image resolution, no CSS inlining.</summary>
    public static RenderOptions ForWebPage() => new();
}
