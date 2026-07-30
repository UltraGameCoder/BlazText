using BlazText.Models;

namespace BlazText;

/// <summary>Caret position in viewport coordinates, for anchoring popups.</summary>
public record CaretRect(double Top, double Left, double Bottom);

/// <summary>A character range in the editor's plain text, used for highlighting.</summary>
public record TextRange(int Start, int Length);

public class ContentChangedEventArgs : EventArgs
{
    public required string Html { get; init; }

    public required BlazTextDocument Document { get; init; }

    public string TextBeforeCaret { get; init; } = string.Empty;

    public CaretRect? Caret { get; init; }
}

public class SelectionChangedEventArgs : EventArgs
{
    public string TextBeforeCaret { get; init; } = string.Empty;

    public CaretRect? Caret { get; init; }

    public bool IsCollapsed { get; init; }
}
