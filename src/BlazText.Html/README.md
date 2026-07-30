# BlazText.Html

HTML tooling plugins for the BlazText editor (AngleSharp-backed):

- `HtmlPlugin` — raw HTML source view, pretty-printing, live validation with line/column issues, sanitized preview panel
- `EmailPreviewPlugin` — previews the document as the e-mail your backend would send: Liquid-rendered (when that plugin is attached), images resolved, **CSS inlined**, in a sandboxed iframe with desktop/mobile presets

```razor
<BlazTextEditor @bind-Document="doc">
    <HtmlPlugin />
    <EmailPreviewPlugin />
</BlazTextEditor>
```

Documentation: https://github.com/mikedegroot/BlazText
