using System.Diagnostics.CodeAnalysis;

namespace BlazText.Models;

/// <summary>
/// The <c>blaztext:{id}</c> URI scheme used inside document HTML to reference an
/// <see cref="EmbeddedImage"/> without inlining its bytes into the content.
/// </summary>
public static class BlazTextImageUri
{
    public const string Scheme = "blaztext:";

    public static string Create(string imageId) => Scheme + imageId;

    /// <summary>Extracts the image id from a <c>blaztext:{id}</c> src value.</summary>
    public static bool TryGetId(string? src, [NotNullWhen(true)] out string? imageId)
    {
        if (src is not null && src.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) && src.Length > Scheme.Length)
        {
            imageId = src[Scheme.Length..];
            return true;
        }

        imageId = null;
        return false;
    }
}
