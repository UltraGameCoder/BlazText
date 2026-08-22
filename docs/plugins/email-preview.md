# EmailPreviewPlugin

Package: `BlazText.Html` · Namespace: `BlazText.Html`

Previews the document as the e-mail your backend would send: content renderers run first (so Liquid renders if that plugin is attached), then the `BlazText.Rendering` e-mail pipeline — embedded image resolution and **CSS inlining** — and the result renders in a sandboxed iframe with desktop/mobile width presets. PreMailer warnings surface as a badge.

Because preview and backend share one pipeline, "looks right in the preview" means "looks right in the inbox" (per what CSS inlining can guarantee).

```razor
<BlazTextEditor @bind-Document="doc">
    <LiquidPlugin Drops="sampleDrops" LiquidTemplateOptions="templateOptions" />
    <EmailPreviewPlugin LayoutContent="@layoutHtml" Drops="sampleDrops" LiquidTemplateOptions="templateOptions" />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Position` | `PanelPosition` | `Right` | Where the preview panel renders |
| `LayoutContent` | `string?` | `null` | Optional `{{ body }}` Liquid layout wrapping the document |
| `Drops` | `Dictionary<string, object?>` | empty | Values for Liquid in `LayoutContent` |
| `LiquidTemplateOptions` | `TemplateOptions?` | `null` | Fluid options for the layout render; pass your backend's instance so the same drops resolve |
| `MobileWidth` | `int` | `375` | Mobile preset width (px) |
| `Order` | `int` | `70` | Toolbar position |

Set `LayoutContent` to the same layout template your backend passes to `RenderOptions.LayoutContent` so authors preview the fully wrapped e-mail. See [save-load-and-rendering.md](../save-load-and-rendering.md) for the pipeline details.

## Drops in the layout

The layout is a Liquid template of its own: `{{ body }}` is the rendered document, and any other drop it uses (`{{ company }}` in a footer, say) is resolved against `Drops`. Pass the same dictionary you give `LiquidPlugin` and your backend's `RenderOptions.LiquidValues` — otherwise the document's drops resolve (the Liquid plugin's content renderer handles those) while the layout's render empty, and the preview no longer matches the sent e-mail.

`Drops` is only needed when `LayoutContent` is set; drops inside the document itself never go through it.

Values alone are not the whole story. What the layout may *read* from them is decided by the
member access strategy, which defaults to dictionaries and registered types only. A layout using
`{{ user.name }}` renders it empty unless the type is registered — so pass the same
`TemplateOptions` instance your backend renders with, via `LiquidTemplateOptions`, exactly as you
do for `LiquidPlugin`. Scalars and dictionaries need no registration, so `Drops` on its own is
enough only while the layout sticks to those.
