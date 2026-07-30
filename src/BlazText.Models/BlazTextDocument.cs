using System.Text.Json;

namespace BlazText.Models;

/// <summary>
/// The persistable unit of BlazText: everything needed to save an editor's state and
/// restore it later, or to render its output outside the editor (webpages, emails).
/// Serializes cleanly with System.Text.Json.
/// </summary>
public class BlazTextDocument
{
    /// <summary>Version of the document schema, for forward-compatible migrations.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>The authored HTML content. May contain Liquid syntax and blaztext:{id} image references.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Images inserted into the document, stored as blobs alongside the content.</summary>
    public List<EmbeddedImage> Images { get; set; } = [];

    /// <summary>Liquid drops detected in <see cref="Content"/>, maintained by the Liquid plugin.</summary>
    public List<DetectedDrop> DetectedDrops { get; set; } = [];

    /// <summary>Per-plugin persisted state, keyed by plugin identifier.</summary>
    public Dictionary<string, JsonElement> PluginState { get; set; } = [];
}
