# Architecture

Contributor-focused notes. Read [index.md](index.md) first for the consumer view.

## Package graph

```
BlazText.Models      (POCOs, zero deps — the frontend/backend contract)
   ↑            ↑
BlazText.Rendering   BlazText (core editor RCL)
(Fluid, PreMailer)      ↑            ↑
   ↑          ┌─────────┘            │
BlazText.Html ┘              BlazText.Liquid
(AngleSharp)                 (via Rendering → Fluid)
```

Rules the graph enforces:

- `Models` and `Rendering` never reference Blazor — backends consume them.
- Core (`BlazText`) references no third-party packages — the lean default.
- Feature packages (`Html`, `Liquid`) never reference *each other*. Cross-feature behavior goes through core interfaces (see `IContentRenderer` below).

## How the editor works

`BlazTextEditor` renders a `contenteditable` div (`.blaztext-surface`) and **never renders children into it** — Blazor's diffing would fight the user's typing. All DOM interaction goes through the collocated JS module `BlazTextEditor.razor.js`:

- `input` events → `getContent` (normalized HTML) → `NotifyContentChangedAsync` → updates `Document.Content`, fires `@bind` callbacks, then `EditorContext.ContentChanged`.
- Content pushed *into* the editor (bind param changed, `SetContentAsync`, document restore) goes through `setContent`, guarded by `_lastKnownContent` comparisons to avoid caret-destroying redundant writes.
- Images: the DOM shows data URIs tagged `data-blaztext-id`; `getContent`/`setContent` translate to/from `blaztext:{id}` so the stored document stays blob-free (blobs live in `Document.Images`).
- Search highlighting uses the CSS Custom Highlight API — no DOM mutation, so highlights never leak into saved content. `.NET` computes match offsets (honoring `StringComparison`) over the same plain-text view the JS walker produces.
- Key interception: plugins register keys via `EditorContext.InterceptKeys`; the editor syncs the key set to JS after render; intercepted keys are `preventDefault`ed and dispatched back as events (this is how the autocomplete popup owns Enter/arrows while open).
- Paste is sanitized in JS (scripts, event handlers, `javascript:` URLs). `setContent` deliberately does **not** sanitize — documents may legitimately contain `<style>` blocks for e-mail; sanitization happens at preview/render boundaries (`HtmlTooling.Sanitize`).

## The plugin contract

`BlazTextEditor` cascades an `EditorContext` (fixed value). `BlazTextPluginBase` captures it, self-registers for `GetPlugin<T>()` discovery, and unregisters on dispose. Plugins contribute:

- **ToolbarItem / EditorPanel** — `RenderFragment`s executed *inside the editor's render tree* (that's why `RequestRefresh()` exists: a plugin's own `StateHasChanged` can't re-render fragments it handed to the editor).
- **ISuggestionProvider** — pull-based: the `AutoCompletePlugin` queries providers with the text before the caret on every content/selection change; a provider returns null when its trigger isn't present.
- **IContentRenderer** — an ordered transform chain over document HTML. `EditorContext.ApplyContentRenderersAsync()` runs it; this is how `EmailPreviewPlugin` shows Liquid-rendered output without referencing `BlazText.Liquid`.

## Testing

`tests/BlazText.Tests` uses xunit; component tests use bUnit with the JS module mocked in loose mode. The JS-heavy paths (caret handling, highlight ranges, autocomplete keyboard flow) are covered by driving the demo app in a real browser — see the verification list in [CONTRIBUTING.md](../CONTRIBUTING.md).
