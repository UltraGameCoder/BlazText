namespace BlazText;

/// <summary>What the user is typing, handed to suggestion providers to decide whether to offer completions.</summary>
/// <param name="TextBeforeCaret">Plain text from the start of the document up to the caret (tail-truncated).</param>
public record SuggestionRequest(string TextBeforeCaret);

/// <summary>One completion the user can pick.</summary>
/// <param name="Label">Shown in the popup.</param>
/// <param name="InsertText">Inserted into the document when picked.</param>
/// <param name="Description">Optional secondary text shown next to the label.</param>
public record Suggestion(string Label, string InsertText, string? Description = null);

/// <summary>The completions offered for a request.</summary>
/// <param name="Items">Suggestions to show; the popup stays closed when empty.</param>
/// <param name="ReplaceLength">How many characters before the caret the insertion replaces (the partially typed token).</param>
public record SuggestionResult(IReadOnlyList<Suggestion> Items, int ReplaceLength);

/// <summary>
/// Supplies completions to the AutoCompletePlugin. A provider inspects the
/// text before the caret and returns null when it has nothing to offer (its trigger isn't present).
/// </summary>
public interface ISuggestionProvider
{
    Task<SuggestionResult?> GetSuggestionsAsync(SuggestionRequest request);
}
