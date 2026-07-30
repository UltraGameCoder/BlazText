# BlazText

An extensible rich text editor component for Blazor. On its own, `<BlazTextEditor>` renders a bare contenteditable surface with two-way binding; features arrive as plugin components you place inside it.

```razor
<BlazTextEditor @bind-Document="doc">
    <BasicFormattingPlugin />
    <SearchPlugin Comparison="StringComparison.OrdinalIgnoreCase" />
    <AutoCompletePlugin />
    <ImagePlugin />
</BlazTextEditor>
```

This core package carries no third-party dependencies and includes the formatting, search, autocomplete, and image plugins. Add `BlazText.Html` (HTML tooling, e-mail preview) and `BlazText.Liquid` (Shopify Liquid templating) for more, or write your own plugin against the same API the built-ins use.

Documentation: https://github.com/UltraGameCoder/BlazText
