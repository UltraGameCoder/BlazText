# HtmlPlugin

Package: `BlazText.Html` · Namespace: `BlazText.Html`

HTML tooling for the editor:

- **Source view** (`</>`): edit the raw HTML in a textarea; Apply writes it back to the surface.
- **Formatting** (`⇥`): pretty-prints the content (AngleSharp).
- **Validation**: AngleSharp parses the content and reports parser errors with line/column; a toolbar badge shows the issue count, and the source panel lists them.
- **Preview** (`👁`): sandboxed iframe preview of the (sanitized) content. Content renderers are applied first, so with `LiquidPlugin` attached the preview shows rendered output.

```razor
<BlazTextEditor @bind-Document="doc">
    <HtmlPlugin PreviewPosition="PanelPosition.Right" ValidationChanged="OnValidated" />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `PreviewPosition` | `PanelPosition` | `Right` | Where the preview panel renders |
| `AutoValidate` | `bool` | `true` | Validate on every content change |
| `Order` | `int` | `60` | Toolbar position |
| `ValidationChanged` | `EventCallback<HtmlValidationResult>` | — | Raised after each validation |

`Validation` (property) exposes the latest `HtmlValidationResult`. Issues are `Warning` severity: the HTML5 parser recovers from everything, so the content still renders — just possibly not as intended.

The underlying operations are available without the UI as `HtmlTooling.Validate / Format / Sanitize`.
