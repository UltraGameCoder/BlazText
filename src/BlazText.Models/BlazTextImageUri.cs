using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

    /// <summary>
    /// Characters an id may contain. A reference ends at the first character outside this set,
    /// which is what makes the match anchored: an id can never swallow the text that follows it.
    /// An id using other characters simply fails to resolve rather than resolving to the wrong
    /// image — see <see cref="ReplaceReferences"/>.
    /// </summary>
    private static readonly SearchValues<char> IdCharacters = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~%+");

    /// <summary>
    /// Rewrites every <c>blaztext:{id}</c> reference in <paramref name="html"/> with whatever
    /// <paramref name="resolve"/> returns for that id. Returning <see langword="null"/> leaves
    /// the reference untouched, which is what happens for an id that is not registered.
    /// </summary>
    /// <remarks>
    /// The id is extracted and looked up, rather than each known id being string-replaced into
    /// the document. Replacing is unanchored: an id that is a prefix of another one ("logo" and
    /// "logo2") consumes the longer reference's prefix and corrupts it, and that happens even
    /// when the longer id is not registered at all, so ordering by length cannot fix it.
    /// Lookup is ordinal and case-sensitive, matching how the editor maps ids to images.
    /// <para>
    /// A reference is resolved wherever it appears, not only in a <c>src</c> attribute. Scoping
    /// it would mean parsing and re-serializing the document, which would reformat output the
    /// renderer otherwise preserves byte-for-byte.
    /// </para>
    /// </remarks>
    public static string ReplaceReferences(string html, Func<string, string?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var index = html.IndexOf(Scheme, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return html;
        }

        var builder = new StringBuilder(html.Length);
        var position = 0;

        while (index >= 0)
        {
            var idStart = index + Scheme.Length;
            var idEnd = idStart;

            while (idEnd < html.Length && IdCharacters.Contains(html[idEnd]))
            {
                idEnd++;
            }

            var replacement = idEnd > idStart ? resolve(html[idStart..idEnd]) : null;

            builder.Append(html, position, index - position);

            if (replacement is not null)
            {
                builder.Append(replacement);
            }
            else
            {
                builder.Append(html, index, idEnd - index);
            }

            position = idEnd;
            index = html.IndexOf(Scheme, position, StringComparison.OrdinalIgnoreCase);
        }

        builder.Append(html, position, html.Length - position);
        return builder.ToString();
    }
}
