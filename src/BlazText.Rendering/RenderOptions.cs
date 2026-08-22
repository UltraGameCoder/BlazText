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
    /// Fluid options used to build the render context, unless <see cref="LiquidContextFactory"/>
    /// overrides it. Defaults to Fluid's registered-members-only access: templates can read
    /// dictionaries and the members of types you allow explicitly — nothing else. Deliberately
    /// restrictive, because BlazText documents are authored by end users; see
    /// docs/save-load-and-rendering.md.
    /// </summary>
    public TemplateOptions LiquidTemplateOptions { get; set; } = new();

    /// <summary>
    /// Advanced override: builds a fully configured Fluid <see cref="TemplateContext"/> to render
    /// with. When set, <see cref="LiquidTemplateOptions"/> is ignored and <see cref="LiquidValues"/>
    /// are applied on top of whatever it returns.
    /// </summary>
    /// <remarks>
    /// A factory rather than a context, because rendering writes to the context it is given.
    /// This is called once per render, so the values of one document — including the layout's
    /// body variable — can never be observed by another. Returning the <em>same</em> instance
    /// every time gives that guarantee up: values leak between sequential renders, and
    /// <see cref="TemplateContext"/> is not thread-safe, so overlapping renders corrupt it.
    /// Build a new context inside the factory.
    /// </remarks>
    public Func<TemplateContext>? LiquidContextFactory { get; set; }

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

    /// <summary>
    /// Lets Liquid read the public members of <typeparamref name="T"/>. Only that type: members
    /// whose own type isn't allowed too render as nil, so a drop can't be walked into the wider
    /// object graph.
    /// </summary>
    public RenderOptions AllowMembersOf<T>() => AllowMembersOf(typeof(T));

    /// <summary>
    /// Type-based overload of <see cref="AllowMembersOf{T}"/>, for types you only have at runtime
    /// (anonymous types in particular: <c>options.AllowMembersOf(drop.GetType())</c>).
    /// </summary>
    public RenderOptions AllowMembersOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        LiquidTemplateOptions.MemberAccessStrategy.Register(type);
        return this;
    }

    /// <summary>
    /// Lets Liquid read every public member of every object reachable from <see cref="LiquidValues"/>,
    /// however deep. Only appropriate when template authors are as trusted as your own code: a template
    /// can otherwise walk from a drop into whatever the graph reaches (DbContext, services, configuration).
    /// Prefer <see cref="AllowMembersOf{T}"/>.
    /// </summary>
    public RenderOptions AllowAllMembersUnsafe()
    {
        LiquidTemplateOptions.MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance;
        return this;
    }
}
