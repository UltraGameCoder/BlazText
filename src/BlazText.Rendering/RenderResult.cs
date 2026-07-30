namespace BlazText.Rendering;

/// <summary>Final HTML produced by <see cref="BlazTextRenderer"/> plus anything non-fatal that happened on the way.</summary>
public class RenderResult
{
    public required string Html { get; init; }

    /// <summary>Non-fatal problems (Liquid parse errors, CSS inlining warnings). Empty on a clean render.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
