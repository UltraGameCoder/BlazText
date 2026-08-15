using Fluid;

namespace BlazText.DemoApp;

/// <summary>
/// The single place that decides what a demo template may read. Editor previews
/// (<c>LiquidPlugin</c>) and backend-style renders (<c>BlazTextRenderer</c>) both use
/// <see cref="Options"/>, so the preview and the sent e-mail resolve identically.
/// </summary>
public static class TemplateDrops
{
    /// <summary>Values the templates are rendered with, keyed by drop name.</summary>
    public static Dictionary<string, object?> Values { get; } = new()
    {
        ["user"] = new Recipient("Ada", "ada@example.com"),
        ["company"] = "BlazText Inc.",
    };

    /// <summary>
    /// Templates here are authored in the editor, so they get Fluid's registered-members-only
    /// access: <see cref="Recipient"/>'s own properties resolve, anything else the object graph
    /// reaches stays invisible.
    /// </summary>
    public static TemplateOptions Options { get; } = CreateOptions();

    private static TemplateOptions CreateOptions()
    {
        var options = new TemplateOptions();

        // Liquid authors write {{ user.name }}, not {{ user.Name }}.
        options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
        options.MemberAccessStrategy.Register<Recipient>();

        return options;
    }
}

/// <summary>A drop type: exactly the recipient fields templates are allowed to use.</summary>
public sealed record Recipient(string Name, string Email);
