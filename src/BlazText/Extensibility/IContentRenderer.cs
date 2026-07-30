using BlazText.Models;

namespace BlazText;

/// <summary>
/// Transforms document HTML for previews. Renderers are applied in <see cref="Order"/> as a
/// chain (each receives the previous output), which lets e.g. an email preview pick up Liquid
/// rendering without the two plugins referencing each other.
/// </summary>
public interface IContentRenderer
{
    int Order { get; }

    Task<string> RenderAsync(string html, BlazTextDocument document);
}
