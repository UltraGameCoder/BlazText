using BlazText.Models;

namespace BlazText;

/// <summary>
/// The editor's programmatic surface, used by plugins (and app code holding a
/// <c>@ref</c> to the editor). Implemented by <see cref="BlazTextEditor"/>.
/// </summary>
public abstract class EditorApi
{
    /// <summary>The document currently being edited. Mutate it via <see cref="UpdateDocumentAsync"/>.</summary>
    public abstract BlazTextDocument Document { get; }

    /// <summary>The current content as normalized HTML (embedded images referenced as <c>blaztext:{id}</c>).</summary>
    public abstract Task<string> GetContentAsync();

    /// <summary>Replaces the whole content.</summary>
    public abstract Task SetContentAsync(string html);

    /// <summary>The content as plain text, matching what highlight ranges index into.</summary>
    public abstract Task<string> GetPlainTextAsync();

    /// <summary>Inserts HTML at the caret, replacing the selection if there is one.</summary>
    public abstract Task InsertHtmlAtSelectionAsync(string html);

    /// <summary>Deletes <paramref name="charCount"/> characters before the caret, then inserts <paramref name="text"/>.</summary>
    public abstract Task ReplaceTextBeforeCaretAsync(int charCount, string text);

    /// <summary>
    /// Applies an inline/block format to the selection. Commands are the browser editing commands:
    /// "bold", "italic", "underline", "strikeThrough", "insertUnorderedList", "insertOrderedList",
    /// "foreColor" (with value), "removeFormat", ...
    /// </summary>
    public abstract Task ApplyFormatAsync(string command, string? value = null);

    /// <summary>Highlights character ranges of the plain text (e.g. search matches); pass the active one to emphasize it.</summary>
    public abstract Task HighlightRangesAsync(IReadOnlyList<TextRange> ranges, int activeIndex = -1);

    public abstract Task ClearHighlightsAsync();

    /// <summary>Scrolls the highlight at <paramref name="index"/> into view.</summary>
    public abstract Task ScrollToHighlightAsync(int index);

    public abstract Task FocusAsync();

    /// <summary>
    /// Mutates the document outside of a content edit (images, detected drops, plugin state)
    /// and notifies the developer's <c>@bind-Document</c> binding.
    /// </summary>
    public abstract Task UpdateDocumentAsync(Action<BlazTextDocument> mutate);
}
