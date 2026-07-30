using System.Text.Json;
using BlazText.Models;

namespace BlazText.Tests.Models;

public class BlazTextDocumentTests
{
    [Fact]
    public void Document_round_trips_through_json()
    {
        var image = new EmbeddedImage
        {
            FileName = "logo.png",
            ContentType = "image/png",
            Data = [1, 2, 3, 4],
        };

        var document = new BlazTextDocument
        {
            Content = $"<p>Hello {{{{ user.name }}}}</p><img src=\"{BlazTextImageUri.Create(image.Id)}\">",
            Images = [image],
            DetectedDrops = [new DetectedDrop { Name = "user", Path = "user.name", Occurrences = 1 }],
            PluginState = new Dictionary<string, JsonElement>
            {
                ["liquid"] = JsonSerializer.SerializeToElement(new { Strict = true }),
            },
        };

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<BlazTextDocument>(json);

        Assert.NotNull(restored);
        Assert.Equal(document.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(document.Content, restored.Content);
        Assert.Equal([1, 2, 3, 4], restored.Images.Single().Data);
        Assert.Equal("image/png", restored.Images.Single().ContentType);
        Assert.Equal("user.name", restored.DetectedDrops.Single().Path);
        Assert.True(restored.PluginState["liquid"].GetProperty("Strict").GetBoolean());
    }

    [Fact]
    public void ImageUri_creates_and_parses_ids()
    {
        var src = BlazTextImageUri.Create("abc123");

        Assert.Equal("blaztext:abc123", src);
        Assert.True(BlazTextImageUri.TryGetId(src, out var id));
        Assert.Equal("abc123", id);

        Assert.False(BlazTextImageUri.TryGetId("https://example.com/x.png", out _));
        Assert.False(BlazTextImageUri.TryGetId("blaztext:", out _));
        Assert.False(BlazTextImageUri.TryGetId(null, out _));
    }

    [Fact]
    public void EmbeddedImage_produces_data_uri()
    {
        var image = new EmbeddedImage { ContentType = "image/gif", Data = [71, 73, 70] };

        Assert.Equal($"data:image/gif;base64,{Convert.ToBase64String(image.Data)}", image.ToDataUri());
    }

    [Fact]
    public void ValidationResult_is_invalid_only_on_errors()
    {
        var result = new HtmlValidationResult
        {
            Issues = [new ValidationIssue { Severity = ValidationSeverity.Warning, Message = "w" }],
        };

        Assert.True(result.IsValid);

        result.Issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, Message = "e" });

        Assert.False(result.IsValid);
    }
}
