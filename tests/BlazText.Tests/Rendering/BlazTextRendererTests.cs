using BlazText.Models;
using BlazText.Rendering;
using Fluid;

namespace BlazText.Tests.Rendering;

public class BlazTextRendererTests
{
    [Fact]
    public async Task Renders_liquid_with_supplied_values()
    {
        var document = new BlazTextDocument { Content = "<p>Hi {{ user.name }}!</p>" };
        var options = new RenderOptions { LiquidValues = { ["user"] = new Dictionary<string, object?> { ["name"] = "Mike" } } };

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>Hi Mike!</p>", result.Html);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Invalid_liquid_keeps_content_and_warns()
    {
        var document = new BlazTextDocument { Content = "<p>{% if %}</p>" };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.Equal(document.Content, result.Html);
        Assert.Contains(result.Warnings, w => w.Contains("parse error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Wraps_content_in_layout_via_body_variable()
    {
        var document = new BlazTextDocument { Content = "<p>{{ greeting }}</p>" };
        var options = new RenderOptions
        {
            LiquidValues = { ["greeting"] = "Hello" },
            LayoutContent = "<html><body>{{ body }}</body></html>",
        };

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<html><body><p>Hello</p></body></html>", result.Html);
    }

    private static TemplateContext NewContext() =>
        new(new TemplateOptions { MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance });

    [Fact]
    public async Task Values_do_not_leak_from_one_render_into_the_next()
    {
        var options = new RenderOptions { LiquidContextFactory = NewContext };

        await BlazTextRenderer.RenderAsync(
            new BlazTextDocument { Content = "<p>{{ greeting }}</p>" },
            new RenderOptions
            {
                LiquidContextFactory = options.LiquidContextFactory,
                LiquidValues = { ["greeting"] = "Hello" },
                LayoutContent = "<html>{{ body }}</html>",
            });

        // Neither the values nor the layout's body variable may survive the render above.
        var second = await BlazTextRenderer.RenderAsync(
            new BlazTextDocument { Content = "<p>[{{ body }}][{{ greeting }}]</p>" },
            options);

        Assert.Equal("<p>[][]</p>", second.Html);
    }

    [Fact]
    public async Task Layout_only_render_does_not_retain_the_body_variable()
    {
        var options = new RenderOptions
        {
            LiquidContextFactory = NewContext,
            RenderLiquid = false,
            LayoutContent = "<html>{{ body }}</html>",
        };

        await BlazTextRenderer.RenderAsync(new BlazTextDocument { Content = "<p>secret</p>" }, options);

        var second = await BlazTextRenderer.RenderAsync(
            new BlazTextDocument { Content = "<p>[{{ body }}]</p>" },
            new RenderOptions { LiquidContextFactory = options.LiquidContextFactory });

        Assert.Equal("<p>[]</p>", second.Html);
    }

    [Fact]
    public async Task Concurrent_renders_do_not_leak_between_documents()
    {
        // The bulk-mail pattern: many overlapping renders sharing one configuration. A context
        // shared across them cannot stay isolated, however carefully its scopes are managed.
        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
        {
            var result = await BlazTextRenderer.RenderAsync(
                new BlazTextDocument { Content = "<p>{{ recipient }}</p>" },
                new RenderOptions
                {
                    LiquidContextFactory = NewContext,
                    LayoutContent = "<html>{{ body }}</html>",
                    LiquidValues = { ["recipient"] = $"user{i}" },
                });

            return (Index: i, result.Html);
        })));

        Assert.All(results, r => Assert.Equal($"<html><p>user{r.Index}</p></html>", r.Html));
    }

    [Fact]
    public async Task Context_factory_is_invoked_once_per_liquid_pass()
    {
        var invocations = 0;
        var options = new RenderOptions
        {
            LiquidContextFactory = () => { invocations++; return NewContext(); },
            LayoutContent = "<html>{{ body }}</html>",
        };

        await BlazTextRenderer.RenderAsync(new BlazTextDocument { Content = "<p>hi</p>" }, options);

        // Content pass and layout pass, each with its own context — never a reused one.
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task Resolves_embedded_images_to_data_uris_by_default()
    {
        var image = new EmbeddedImage { ContentType = "image/png", Data = [1, 2, 3] };
        var document = new BlazTextDocument
        {
            Content = $"<img src=\"{BlazTextImageUri.Create(image.Id)}\">",
            Images = [image],
        };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.Equal($"<img src=\"{image.ToDataUri()}\">", result.Html);
    }

    [Fact]
    public async Task Custom_image_resolver_wins()
    {
        var image = new EmbeddedImage { ContentType = "image/png", Data = [1] };
        var document = new BlazTextDocument
        {
            Content = $"<img src=\"{BlazTextImageUri.Create(image.Id)}\">",
            Images = [image],
        };
        var options = new RenderOptions { ImageResolver = i => $"https://cdn.example.com/{i.Id}.png" };

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal($"<img src=\"https://cdn.example.com/{image.Id}.png\">", result.Html);
    }

    [Fact]
    public async Task Email_preset_inlines_css()
    {
        var document = new BlazTextDocument
        {
            Content = "<style>p { color: red; }</style><p>Hi</p>",
        };

        var result = await BlazTextRenderer.RenderAsync(document, RenderOptions.ForEmail());

        Assert.Contains("<p style=\"color: red\">", result.Html);
    }

    [Fact]
    public async Task Webpage_preset_keeps_css_untouched()
    {
        var document = new BlazTextDocument
        {
            Content = "<style>p { color: red; }</style><p>Hi</p>",
        };

        var result = await BlazTextRenderer.RenderAsync(document, RenderOptions.ForWebPage());

        Assert.Equal(document.Content, result.Html);
    }

    [Fact]
    public async Task Members_of_unregistered_types_are_not_readable_by_default()
    {
        var document = new BlazTextDocument { Content = "<p>[{{ order.Reference }}]</p>" };
        var options = new RenderOptions { LiquidValues = { ["order"] = new Order() } };

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>[]</p>", result.Html);
    }

    [Fact]
    public async Task Allowed_type_does_not_open_up_the_types_it_reaches()
    {
        var document = new BlazTextDocument { Content = "<p>[{{ order.Reference }}][{{ order.Internals.ConnectionString }}]</p>" };
        var options = new RenderOptions { LiquidValues = { ["order"] = new Order() } }
            .AllowMembersOf<Order>();

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>[A-1][]</p>", result.Html);
    }

    [Fact]
    public async Task Unsafe_opt_in_walks_the_whole_object_graph()
    {
        var document = new BlazTextDocument { Content = "<p>[{{ order.Internals.ConnectionString }}]</p>" };
        var options = new RenderOptions { LiquidValues = { ["order"] = new Order() } }
            .AllowAllMembersUnsafe();

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>[secret]</p>", result.Html);
    }

    [Fact]
    public async Task Anonymous_drops_are_readable_once_their_runtime_type_is_allowed()
    {
        var user = new { name = "Ada" };
        var document = new BlazTextDocument { Content = "<p>{{ user.name }}</p>" };
        var options = new RenderOptions { LiquidValues = { ["user"] = user } }
            .AllowMembersOf(user.GetType());

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>Ada</p>", result.Html);
    }

    [Fact]
    public async Task Supplied_liquid_context_keeps_full_control_of_member_access()
    {
        var document = new BlazTextDocument { Content = "<p>{{ order.Reference }}</p>" };
        var templateOptions = new TemplateOptions { MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
        var options = new RenderOptions
        {
            LiquidContextFactory = () => new TemplateContext(templateOptions),
            LiquidValues = { ["order"] = new Order() },
        };

        var result = await BlazTextRenderer.RenderAsync(document, options);

        Assert.Equal("<p>A-1</p>", result.Html);
    }

    private sealed class Order
    {
        public string Reference { get; set; } = "A-1";

        /// <summary>Stands in for the service/DbContext references a real entity drags along.</summary>
        public Infrastructure Internals { get; set; } = new();
    }

    private sealed class Infrastructure
    {
        public string ConnectionString { get; set; } = "secret";
    }
}
