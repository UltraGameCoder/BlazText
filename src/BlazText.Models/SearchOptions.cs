namespace BlazText.Models;

/// <summary>Options for searching document text by a character sequence.</summary>
public class SearchOptions
{
    public string Query { get; set; } = string.Empty;

    /// <summary>Controls case sensitivity and culture behavior of the search.</summary>
    public StringComparison Comparison { get; set; } = StringComparison.OrdinalIgnoreCase;
}
