# BasicFormattingPlugin

Package: `BlazText` · Namespace: `BlazText.Plugins`

Adds toolbar buttons for bold, italic, underline, strikethrough, bulleted/numbered lists, and clear formatting. The bare editor already honors browser shortcuts (Ctrl+B, …); this plugin makes them clickable.

```razor
<BlazTextEditor @bind-Value="html">
    <BasicFormattingPlugin />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Order` | `int` | `0` | Toolbar position relative to other items |

Formatting is applied through `EditorApi.ApplyFormatAsync(command, value)`, which any plugin can call with other browser editing commands (e.g. `"foreColor"` — see the demo app's color picker).
