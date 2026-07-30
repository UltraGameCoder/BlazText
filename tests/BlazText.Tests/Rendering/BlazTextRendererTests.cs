using BlazText.Models;
using BlazText.Rendering;

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
}
