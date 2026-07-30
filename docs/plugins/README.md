# Built-in plugins

| Plugin | Package | Page |
| --- | --- | --- |
| `BasicFormattingPlugin` | BlazText | [basic-formatting.md](basic-formatting.md) |
| `SearchPlugin` | BlazText | [search.md](search.md) |
| `AutoCompletePlugin` | BlazText | [autocomplete.md](autocomplete.md) |
| `ImagePlugin` | BlazText | [image.md](image.md) |
| `HtmlPlugin` | BlazText.Html | [html.md](html.md) |
| `EmailPreviewPlugin` | BlazText.Html | [email-preview.md](email-preview.md) |
| `LiquidPlugin` | BlazText.Liquid | [liquid.md](liquid.md) |

All plugins share the same usage pattern — place them inside the editor:

```razor
<BlazTextEditor @bind-Document="doc">
    <SearchPlugin Comparison="StringComparison.Ordinal" />
</BlazTextEditor>
```
