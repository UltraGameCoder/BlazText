# LiquidPlugin

Package: `BlazText.Liquid` · Namespace: `BlazText.Liquid`

Makes the editor Liquid-aware ([Shopify Liquid](https://shopify.github.io/liquid/) via [Fluid](https://github.com/sebastienros/fluid)):

- **Preview rendering** — registers an `IContentRenderer` that renders Liquid with your `Drops`, so the HTML preview and e-mail preview show real output instead of `{{ ... }}` syntax.
- **Drop detection** — keeps `Document.DetectedDrops` up to date as the author types (name, full path, occurrence count), so your code knows which values a template uses before rendering or accepting it. Detection is heuristic (regex-based), so it works on half-typed templates.
- **Autocomplete** — registers a suggestion provider: typing `{{ ` (or `{{ pre`) offers your declared drops. Requires `AutoCompletePlugin`.
- **Parse status** — a toolbar badge appears while the content isn't a valid Liquid template; the message is in `ParseError`.

```razor
<BlazTextEditor @bind-Document="doc">
    <AutoCompletePlugin />
    <LiquidPlugin Drops="_drops"
                  DropDefinitions="_definitions"
                  DetectedDropsChanged="drops => _used = drops" />
</BlazTextEditor>

@code {
    private readonly Dictionary<string, object?> _drops = new()
    {
        ["user"] = new { name = "Ada", email = "ada@example.com" },
        ["company"] = "BlazText Inc.",
    };

    private readonly List<LiquidDropDefinition> _definitions =
    [
        new("user.name", "Recipient's display name"),
        new("user.email", "Recipient's e-mail address"),
        new("company", "Your company name"),
        new("body", "Inner content (for layout templates)"),
    ];
}
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Drops` | `Dictionary<string, object?>` | empty | Values used when rendering previews |
| `DropDefinitions` | `IReadOnlyList<LiquidDropDefinition>?` | `Drops` keys | Paths offered by autocomplete (declare deep paths like `user.name`) |
| `DetectedDropsChanged` | `EventCallback<IReadOnlyList<DetectedDrop>>` | — | Raised when the used-drop set changes |
| `ParseErrorChanged` | `EventCallback<string?>` | — | Raised when the parse error appears/changes/clears |
| `Order` | `int` | `80` | Toolbar position of the status badge |

Backend rendering of the saved document uses the same values through `BlazText.Rendering` — see [save-load-and-rendering.md](../save-load-and-rendering.md), including the `{{ body }}` layout-in-layout pattern.
