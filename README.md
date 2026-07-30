# BlazText

An extensible rich text editor for Blazor. The editor ships bare; every feature — HTML tooling, Shopify Liquid templating, e-mail preview, search, autocomplete, images — is a plugin you opt into by placing a component inside it:

```razor
<BlazTextEditor @bind-Document="doc">
    <BasicFormattingPlugin />
    <SearchPlugin Comparison="StringComparison.OrdinalIgnoreCase" />
    <AutoCompletePlugin />
    <ImagePlugin />
    <HtmlPlugin />
    <EmailPreviewPlugin />
    <LiquidPlugin Drops="drops" />
</BlazTextEditor>
```

Third-party plugins use the same API as the built-ins — a color picker, an AI rewrite button, or a custom Liquid drop is [a small Razor component away](docs/extending.md).

## Packages

| Package | Purpose |
| --- | --- |
| `BlazText` | Core editor + dependency-free plugins (formatting, search, autocomplete, images) |
| `BlazText.Models` | Save/load contracts, no Blazor — share between frontend and backend |
| `BlazText.Rendering` | Document → final HTML: Liquid, image resolution, e-mail CSS inlining; no Blazor |
| `BlazText.Html` | HTML source view, validation/formatting (AngleSharp), HTML & e-mail previews |
| `BlazText.Liquid` | Liquid rendering, drop detection & autocomplete (Fluid) |

## Highlights

- **Save/load contract**: bind a `BlazTextDocument`, serialize it anywhere, assign it back to restore the session — images and detected Liquid drops included. Backends reference `BlazText.Models` without touching Blazor.
- **E-mail-correct output**: e-mail clients strip `<style>` blocks, so `BlazText.Rendering` inlines CSS at render time (PreMailer). The in-editor e-mail preview runs the *same* pipeline your backend does.
- **Liquid drops as data**: supply drops for live preview; read `Document.DetectedDrops` to know which values a template actually uses. Layout templates wrap bodies via `{{ body }}`.
- **Themeable with plain CSS**: `--blaztext-*` custom properties and stable class names — composes with AntBlazor or any design system. See [docs/theming.md](docs/theming.md).

## Getting started

```bash
dotnet add package BlazText
```

Read [docs/getting-started.md](docs/getting-started.md), or run the demo app:

```bash
dotnet run --project samples/BlazText.DemoApp
```

## Documentation

Everything lives under [docs/](docs/index.md): getting started, per-plugin pages, save/load & rendering, theming, plugin authoring, and architecture notes for contributors ([CONTRIBUTING.md](CONTRIBUTING.md)).

## License

[MIT](LICENSE)
