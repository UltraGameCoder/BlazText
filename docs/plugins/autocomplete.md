# AutoCompletePlugin

Package: `BlazText` · Namespace: `BlazText.Plugins`

The generic suggestion machinery: a caret-anchored popup with keyboard navigation (arrows, Enter/Tab to accept, Escape to dismiss). It has **no completions of its own** — it queries every registered `ISuggestionProvider` on each content or caret change and shows the first non-empty result.

```razor
<BlazTextEditor @bind-Document="doc">
    <AutoCompletePlugin />
    <LiquidPlugin Drops="drops" />   @* registers a provider: type {{ to complete drops *@
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `MaxItems` | `int` | `8` | Maximum suggestions shown |

## Supplying your own completions

```csharp
public class MentionProvider : ISuggestionProvider
{
    public Task<SuggestionResult?> GetSuggestionsAsync(SuggestionRequest request)
    {
        var match = Regex.Match(request.TextBeforeCaret, @"@(\w*)$");
        if (!match.Success) return Task.FromResult<SuggestionResult?>(null);

        var prefix = match.Groups[1].Value;
        var items = users.Where(u => u.StartsWith(prefix))
                         .Select(u => new Suggestion(u, $"@{u} "))
                         .ToList();
        return Task.FromResult<SuggestionResult?>(new(items, prefix.Length + 1));
    }
}
```

Register it from a plugin (`Editor.RegisterSuggestionProvider(...)`) — the popup, positioning, and keyboard handling are handled for you. `ReplaceLength` tells the popup how many characters before the caret the accepted suggestion replaces.
