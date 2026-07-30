# BlazText

BlazText is a rich text editor for Blazor built around one idea: **the editor ships bare, and every feature is a plugin**.

```razor
<BlazTextEditor @bind-Document="doc">          @* bare editable surface *@
    <BasicFormattingPlugin />                  @* + bold/italic/lists *@
    <SearchPlugin />                           @* + find with StringComparison *@
    <LiquidPlugin Drops="drops" />             @* + Shopify Liquid templating *@
    <EmailPreviewPlugin />                     @* + inlined-CSS e-mail preview *@
</BlazTextEditor>
```

Placing a `<BlazTextEditor>` renders its bare minimum: a `contenteditable` surface with two-way binding. Plugins are ordinary Razor components placed inside it; each one registers toolbar items, panels, suggestion providers, or content renderers with the editor. Third-party (or your own) plugins use exactly the same API the built-in ones do — see [extending.md](extending.md).

## Packages

| Package | What it adds | Depends on |
| --- | --- | --- |
| `BlazText` | The editor + dependency-free plugins (formatting, search, autocomplete, images) | Blazor only |
| `BlazText.Models` | Save/load data contracts, **no Blazor** — reference from any backend | nothing |
| `BlazText.Rendering` | Document → final HTML (Liquid, images, e-mail CSS inlining), **no Blazor** | Fluid, PreMailer.Net |
| `BlazText.Html` | HTML source view, validation, formatting, HTML & e-mail previews | AngleSharp |
| `BlazText.Liquid` | Liquid rendering, drop detection, drop autocomplete | Fluid |

The split keeps consumers lean: a plain rich text box costs you only `BlazText`; heavy dependencies arrive with the feature that needs them. Packages never reference each other's features directly — cross-plugin integration (e.g. the e-mail preview showing Liquid-rendered output) flows through interfaces in the core package.

## Documentation map

- [Getting started](getting-started.md) — install, first editor, adding plugins
- [Save, load & rendering](save-load-and-rendering.md) — the document model, backend contract, e-mail pipeline
- [Built-in plugins](plugins/) — one page per plugin
- [Writing your own plugin](extending.md)
- [Theming](theming.md) — CSS custom properties and class hooks
- [Architecture](architecture.md) — contributor-focused internals
