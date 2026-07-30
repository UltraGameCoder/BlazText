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

Documentation: https://github.com/mikedegroot/BlazText
