# Getting started

## Install

```bash
dotnet add package BlazText
```

Add the feature packages you want:

```bash
dotnet add package BlazText.Html
dotnet add package BlazText.Liquid
```

On your backend (to load saved documents or render them), reference the Blazor-free packages instead:

```bash
dotnet add package BlazText.Models
dotnet add package BlazText.Rendering
```

## A minimal editor

```razor
@using BlazText

<BlazTextEditor @bind-Value="_html" Placeholder="Start typing…" />

@code {
    private string _html = "";
}
```

`@bind-Value` binds the raw HTML string. That's the whole bare editor: no toolbar, no panels — browser-native editing shortcuts (Ctrl+B, Ctrl+I) still work.

## Binding the full document

For anything beyond throwaway content, bind a `BlazTextDocument` instead — it carries the HTML *plus* embedded images, detected Liquid drops, and per-plugin state, and is the unit you save and restore:

```razor
@using BlazText
@using BlazText.Models

<BlazTextEditor @bind-Document="_doc" />

@code {
    private BlazTextDocument _doc = new();
}
```

Assigning a previously saved document to `_doc` restores the editor to where the user left off. See [save-load-and-rendering.md](save-load-and-rendering.md).

## Adding features

Features are plugin components placed inside the editor:

```razor
@using BlazText.Plugins
@using BlazText.Html
@using BlazText.Liquid

<BlazTextEditor @bind-Document="_doc">
    <BasicFormattingPlugin />
    <ImagePlugin />
    <SearchPlugin Comparison="StringComparison.OrdinalIgnoreCase" />
    <AutoCompletePlugin />
    <HtmlPlugin />
    <EmailPreviewPlugin />
    <LiquidPlugin Drops="_drops" DropDefinitions="_definitions" />
</BlazTextEditor>
```

Each plugin's parameters are documented in [plugins/](plugins/). Anything you can't find as a built-in is a [custom plugin](extending.md) away.

## Programmatic access

Hold a `@ref` to the editor and use its `Api`:

```razor
<BlazTextEditor @ref="_editor" />
<button @onclick="Insert">Insert</button>

@code {
    private BlazTextEditor _editor = default!;
    private Task Insert() => _editor.Api.InsertHtmlAtSelectionAsync("<b>Hi!</b>");
}
```
