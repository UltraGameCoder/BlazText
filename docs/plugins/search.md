# SearchPlugin

Package: `BlazText` · Namespace: `BlazText.Plugins`

Adds a search box to the toolbar: type a character sequence and all matches highlight in the surface (CSS Custom Highlight API — the document content is never mutated). Enter / Shift+Enter or the arrow buttons cycle matches; a counter shows `current/total`.

```razor
<BlazTextEditor @bind-Value="html">
    <SearchPlugin Comparison="StringComparison.InvariantCultureIgnoreCase" />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Comparison` | `StringComparison` | `OrdinalIgnoreCase` | How matches are compared |
| `SearchPlaceholder` | `string` | `"Search…"` | Input placeholder |
| `Order` | `int` | `50` | Toolbar position |

Case sensitivity uses the full `StringComparison` enum, not a boolean: the configured value picks the comparison family (ordinal / current culture / invariant), and the toolbar's **Aa** toggle switches between that family's case-sensitive and case-insensitive variants at runtime. `EffectiveComparison` exposes what's currently in effect.

Highlight colors are themeable via `--blaztext-highlight-bg` and `--blaztext-highlight-active-bg`. On browsers without the CSS Custom Highlight API, search still counts and navigates matches; only the visual highlight is skipped.
