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
        var options = new RenderOptions { LiquidValues = { ["user"] = new { name = "Mike" } } };

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
    public async Task Image_id_that_prefixes_another_id_resolves_independently()
    {
        var logo = new EmbeddedImage { Id = "logo", ContentType = "image/png", Data = [1] };
        var logo2 = new EmbeddedImage { Id = "logo2", ContentType = "image/png", Data = [2] };
        var document = new BlazTextDocument
        {
            Content = $"<img src=\"{BlazTextImageUri.Create(logo.Id)}\"><img src=\"{BlazTextImageUri.Create(logo2.Id)}\">",
            Images = [logo, logo2],
        };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.Equal($"<img src=\"{logo.ToDataUri()}\"><img src=\"{logo2.ToDataUri()}\">", result.Html);
    }

    [Theory]
    // A reference to an id that is not registered must survive untouched. Ordering the known
    // ids by length cannot achieve this: there is no longer id to sort ahead of the reference.
    [InlineData("logo", "<img src=\"blaztext:logo2\">")]
    [InlineData("img1", "<img src=\"blaztext:img10\">")]
    // Lookup is ordinal and case-sensitive, matching how the editor maps ids to images.
    [InlineData("logo", "<img src=\"blaztext:Logo\">")]
    public async Task Unregistered_image_reference_is_left_untouched(string registeredId, string content)
    {
        var image = new EmbeddedImage { Id = registeredId, ContentType = "image/png", Data = [1] };
        var document = new BlazTextDocument { Content = content, Images = [image] };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.Equal(content, result.Html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Image_with_no_id_does_not_break_rendering(string? id)
    {
        var image = new EmbeddedImage { Id = id!, ContentType = "image/png", Data = [1] };
        var document = new BlazTextDocument { Content = "<p>hi</p>", Images = [image] };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.Equal("<p>hi</p>", result.Html);
    }

    [Fact]
    public void Resolve_image_references_is_shared_with_the_preview_path()
    {
        // HtmlPlugin's preview calls this same method, so preview and sent e-mail cannot drift.
        var image = new EmbeddedImage { Id = "logo", ContentType = "image/png", Data = [1] };

        var html = BlazTextRenderer.ResolveImageReferences(
            "<img src=\"blaztext:logo\"><img src=\"blaztext:logo2\">",
            [image]);

        Assert.Equal($"<img src=\"{image.ToDataUri()}\"><img src=\"blaztext:logo2\">", html);
    }

    [Fact]
    public async Task Hostile_image_content_type_cannot_break_out_of_the_src_attribute()
    {
        // A document deserialized from storage never passes the plugin's upload guard, so the
        // content type has to be safe at the sink. This is the path that reaches e-mail output.
        var image = new EmbeddedImage
        {
            Id = "img1",
            ContentType = "image/png\" onerror=\"alert(1)",
            Data = [1],
        };
        var document = new BlazTextDocument
        {
            Content = $"<img src=\"{BlazTextImageUri.Create(image.Id)}\">",
            Images = [image],
        };

        var result = await BlazTextRenderer.RenderAsync(document);

        Assert.DoesNotContain("onerror", result.Html);
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
}
