using Microsoft.AspNetCore.Components;

namespace BlazText;

/// <summary>A fragment rendered in the editor's toolbar, contributed by a plugin.</summary>
public class ToolbarItem
{
    public required string Id { get; init; }

    /// <summary>Items render sorted ascending by order; built-in plugins use 0–100.</summary>
    public int Order { get; init; }

    public required RenderFragment Content { get; init; }
}
