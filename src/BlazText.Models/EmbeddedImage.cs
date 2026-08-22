using System.Buffers;

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

    /// <summary>
    /// The image as a data: URI, usable directly in an img src attribute. A <see cref="ContentType"/>
    /// that is not a well-formed <c>image/*</c> type is replaced with a generic one rather than
    /// emitted — see <see cref="IsImageContentType"/>.
    /// </summary>
    public string ToDataUri()
    {
        var contentType = IsImageContentType(ContentType) ? ContentType : FallbackContentType;
        return $"data:{contentType};base64,{Convert.ToBase64String(Data)}";
    }

    private const string ContentTypePrefix = "image/";

    private const string FallbackContentType = "application/octet-stream";

    /// <summary>Characters allowed in a MIME subtype (the RFC 2045 token grammar).</summary>
    private static readonly SearchValues<char> SubtypeCharacters = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$&^_.+-");

    /// <summary>
    /// True when <paramref name="contentType"/> is a well-formed <c>image/*</c> MIME type,
    /// i.e. the subtype is a single RFC 2045 token.
    /// </summary>
    public static bool IsImageContentType(string? contentType)
    {
        if (contentType is null || !contentType.StartsWith(ContentTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var subtype = contentType.AsSpan(ContentTypePrefix.Length);
        return subtype.Length > 0 && !subtype.ContainsAnyExcept(SubtypeCharacters);
    }
}
