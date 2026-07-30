using Microsoft.AspNetCore.Components;

namespace BlazText;

public enum PanelPosition
{
    Bottom,
    Right,
}

/// <summary>A fragment rendered next to (or below) the editing surface, contributed by a plugin.</summary>
public class EditorPanel
{
    public required string Id { get; init; }

    public int Order { get; init; }

    public PanelPosition Position { get; init; } = PanelPosition.Bottom;

    public required RenderFragment Content { get; init; }
}
