namespace BlazText.Models;

/// <summary>
/// A Liquid drop usage detected in a document's content, so developer code can
/// recognize which values a template depends on before rendering it.
/// </summary>
public class DetectedDrop
{
    /// <summary>Root variable name, e.g. "user" for <c>{{ user.name }}</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full member path as written, e.g. "user.name".</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>How many times this exact path occurs in the content.</summary>
    public int Occurrences { get; set; }
}
