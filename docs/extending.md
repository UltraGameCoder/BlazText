# Writing your own plugin

A plugin is an ordinary Razor component derived from `BlazTextPluginBase`, placed inside a `<BlazTextEditor>`. On initialization it receives the editor's `EditorContext` and registers whatever it contributes. The built-in plugins use exactly this API — read their source for full examples.

## What you can register

| Registration | Gives you |
| --- | --- |
| `Editor.RegisterToolbarItem(item)` | A fragment in the toolbar (ordered) |
| `Editor.RegisterPanel(panel)` | A panel right of / below the surface |
| `Editor.RegisterSuggestionProvider(p)` | Completions in the `AutoCompletePlugin` popup |
| `Editor.RegisterContentRenderer(r)` | A step in the preview-render chain |
| `Editor.InterceptKeys(keys, handler)` | Keyboard keys handled by you instead of the browser |
| `Editor.ContentChanged` / `SelectionChanged` | Events for reacting to the author |

And through `Api` (the `EditorApi`): `GetContentAsync`, `SetContentAsync`, `InsertHtmlAtSelectionAsync`, `ApplyFormatAsync`, `HighlightRangesAsync`, `UpdateDocumentAsync`, …

> **Rendering rule:** toolbar items and panels execute inside the *editor's* render tree. When your plugin's state changes what those fragments display, call `Editor.RequestRefresh()` — your component's own `StateHasChanged()` won't reach them.

## Example: a color picker toolbar item

The demo app ships this as [`ColorPickerPlugin.razor`](../samples/BlazText.DemoApp/Plugins/ColorPickerPlugin.razor):

```razor
@inherits BlazTextPluginBase

@code {
    private ToolbarItem? _item;
    private string _color = "#d61f1f";

    protected override Task OnEditorInitializedAsync()
    {
        _item = new ToolbarItem { Id = "color-picker", Order = 20, Content = Picker };
        Editor.RegisterToolbarItem(_item);
        return Task.CompletedTask;
    }

    protected override ValueTask OnDisposingAsync()
    {
        if (_item is not null) Editor.UnregisterToolbarItem(_item);
        return ValueTask.CompletedTask;
    }

    private async Task OnColorPicked(ChangeEventArgs e)
    {
        _color = e.Value?.ToString() ?? _color;
        await Api.ApplyFormatAsync("foreColor", _color);
    }

    private RenderFragment Picker => @<span style="display: contents">
        <input type="color" class="blaztext-btn" value="@_color" @onchange="OnColorPicked" />
    </span>;
}
```

Use it like any built-in: `<BlazTextEditor><ColorPickerPlugin /></BlazTextEditor>`.

## Example ideas from the same building blocks

- **AI menu item** — a toolbar button that reads `await Api.GetContentAsync()`, calls your AI backend, then `await Api.SetContentAsync(rewritten)`.
- **A `{{ body }}` layout drop** — with `BlazText.Liquid`, add `new LiquidDropDefinition("body", "Inner template content")` to `DropDefinitions` so layout authors get it in autocomplete; render with `RenderOptions.LayoutContent` (see [save-load-and-rendering.md](save-load-and-rendering.md)).
- **Custom autocomplete source** — implement `ISuggestionProvider` (inspect `request.TextBeforeCaret`, return a `SuggestionResult` or null) and register it; the `AutoCompletePlugin` renders the popup for you.
- **Cross-plugin features** — find other plugins with `Editor.GetPlugin<T>()`, or stay decoupled by consuming `Editor.ApplyContentRenderersAsync()` like the e-mail preview does.

## Distributing plugins

A plugin package is a plain Razor class library referencing `BlazText`. No registration ceremony, no DI setup — consumers just place your component inside their editor.
