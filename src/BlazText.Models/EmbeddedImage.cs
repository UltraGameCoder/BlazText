namespace BlazText.Models;

/// <summary>
/// An image inserted into a document, stored as a blob so it travels with the
/// <see cref="BlazTextDocument"/>. The document's HTML references it via
/// <c>src="blaztext:{Id}"</c> (see <see cref="BlazTextImageUri"/>).
/// </summary>
public class EmbeddedImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. "image/png".</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Raw image bytes. System.Text.Json serializes this as base64.</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>The image as a data: URI, usable directly in an img src attribute.</summary>
    public string ToDataUri() => $"data:{ContentType};base64,{Convert.ToBase64String(Data)}";
}
