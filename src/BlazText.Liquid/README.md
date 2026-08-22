# BlazText.Liquid

Shopify Liquid templating plugin for the BlazText editor (powered by Fluid):

- Live preview rendering with developer-supplied drops
- Drop **detection**: `Document.DetectedDrops` tells your code which values a template actually uses
- Drop **autocomplete** after `{{ ` (requires the core `AutoCompletePlugin`)
- Parse-status badge with the current template error

```razor
<BlazTextEditor @bind-Document="doc">
    <AutoCompletePlugin />
    <LiquidPlugin Drops="drops" DropDefinitions="definitions" />
</BlazTextEditor>
```

Previews use the same restricted member access as backend rendering (dictionaries and types you allow,
never the wider object graph). Pass `LiquidTemplateOptions` the same Fluid options your backend renders
with so the preview keeps telling the truth.

Documentation: https://github.com/UltraGameCoder/BlazText
