# EmailPreviewPlugin

Package: `BlazText.Html` · Namespace: `BlazText.Html`

Previews the document as the e-mail your backend would send: content renderers run first (so Liquid renders if that plugin is attached), then the `BlazText.Rendering` e-mail pipeline — embedded image resolution and **CSS inlining** — and the result renders in a sandboxed iframe with desktop/mobile width presets. PreMailer warnings surface as a badge.

Because preview and backend share one pipeline, "looks right in the preview" means "looks right in the inbox" (per what CSS inlining can guarantee).

```razor
<BlazTextEditor @bind-Document="doc">
    <LiquidPlugin Drops="sampleDrops" />
    <EmailPreviewPlugin LayoutContent="@layoutHtml" />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Position` | `PanelPosition` | `Right` | Where the preview panel renders |
| `LayoutContent` | `string?` | `null` | Optional `{{ body }}` Liquid layout wrapping the document |
| `MobileWidth` | `int` | `375` | Mobile preset width (px) |
| `Order` | `int` | `70` | Toolbar position |

Set `LayoutContent` to the same layout template your backend passes to `RenderOptions.LayoutContent` so authors preview the fully wrapped e-mail. See [save-load-and-rendering.md](../save-load-and-rendering.md) for the pipeline details.
