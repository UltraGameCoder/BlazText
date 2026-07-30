namespace BlazText.Liquid;

/// <summary>
/// A drop (or drop member path) the developer declares as available to template authors,
/// used for autocomplete suggestions. Example: <c>new("user.name", "The recipient's name")</c>.
/// </summary>
public record LiquidDropDefinition(string Path, string? Description = null);
