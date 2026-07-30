using BlazText.Models;

namespace BlazText;

/// <summary>
/// The registration surface a <see cref="BlazTextEditor"/> cascades to its child plugins.
/// Plugins contribute toolbar items, panels, suggestion providers, content renderers, and
/// key interceptors here, and talk back to the editor through <see cref="Api"/>.
/// </summary>
public class EditorContext
{
    private readonly List<ToolbarItem> _toolbarItems = [];
    private readonly List<EditorPanel> _panels = [];
    private readonly List<ISuggestionProvider> _suggestionProviders = [];
    private readonly List<IContentRenderer> _contentRenderers = [];
    private readonly List<object> _plugins = [];
    private readonly Dictionary<string, List<Func<string, Task>>> _keyInterceptors = [];

    internal EditorContext(EditorApi api)
    {
        Api = api;
    }

    /// <summary>The editor's programmatic API: read/write content, insert HTML, apply formatting.</summary>
    public EditorApi Api { get; }

    /// <summary>The document currently being edited.</summary>
    public BlazTextDocument Document => Api.Document;

    public IReadOnlyList<ToolbarItem> ToolbarItems => _toolbarItems;

    public IReadOnlyList<EditorPanel> Panels => _panels;

    public IReadOnlyList<ISuggestionProvider> SuggestionProviders => _suggestionProviders;

    /// <summary>Preview transformers, ordered. See <see cref="IContentRenderer"/>.</summary>
    public IReadOnlyList<IContentRenderer> ContentRenderers =>
        _contentRenderers.OrderBy(r => r.Order).ToList();

    /// <summary>Raised after the user (or a plugin) changed the editor content.</summary>
    public event Func<ContentChangedEventArgs, Task>? ContentChanged;

    /// <summary>Raised when the caret or selection moved without a content change.</summary>
    public event Func<SelectionChangedEventArgs, Task>? SelectionChanged;

    /// <summary>Raised when registrations change, so the editor re-renders its chrome.</summary>
    internal event Action? Changed;

    internal IReadOnlyCollection<string> InterceptedKeys => _keyInterceptors.Keys;

    public void RegisterToolbarItem(ToolbarItem item)
    {
        _toolbarItems.Add(item);
        NotifyChanged();
    }

    public void UnregisterToolbarItem(ToolbarItem item)
    {
        _toolbarItems.Remove(item);
        NotifyChanged();
    }

    public void RegisterPanel(EditorPanel panel)
    {
        _panels.Add(panel);
        NotifyChanged();
    }

    public void UnregisterPanel(EditorPanel panel)
    {
        _panels.Remove(panel);
        NotifyChanged();
    }

    public void RegisterSuggestionProvider(ISuggestionProvider provider)
    {
        _suggestionProviders.Add(provider);
        NotifyChanged();
    }

    public void UnregisterSuggestionProvider(ISuggestionProvider provider)
    {
        _suggestionProviders.Remove(provider);
        NotifyChanged();
    }

    public void RegisterContentRenderer(IContentRenderer renderer)
    {
        _contentRenderers.Add(renderer);
        NotifyChanged();
    }

    public void UnregisterContentRenderer(IContentRenderer renderer)
    {
        _contentRenderers.Remove(renderer);
        NotifyChanged();
    }

    /// <summary>
    /// Intercepts a keyboard key inside the editing surface: its default action is prevented and
    /// <paramref name="handler"/> runs instead. Used e.g. by autocomplete while its popup is open.
    /// Returns a disposable that removes the interception.
    /// </summary>
    public IDisposable InterceptKeys(IEnumerable<string> keys, Func<string, Task> handler)
    {
        var keyList = keys.ToList();

        foreach (var key in keyList)
        {
            if (!_keyInterceptors.TryGetValue(key, out var handlers))
            {
                _keyInterceptors[key] = handlers = [];
            }

            handlers.Add(handler);
        }

        NotifyChanged();
        return new KeyInterception(this, keyList, handler);
    }

    /// <summary>
    /// Runs the document content (or <paramref name="html"/>) through all registered
    /// <see cref="IContentRenderer"/>s in order — how preview plugins obtain e.g. Liquid-rendered output.
    /// </summary>
    public async Task<string> ApplyContentRenderersAsync(string? html = null)
    {
        html ??= Document.Content;

        foreach (var renderer in ContentRenderers)
        {
            html = await renderer.RenderAsync(html, Document);
        }

        return html;
    }

    /// <summary>Makes a plugin instance discoverable by other plugins via <see cref="GetPlugin{T}"/>.</summary>
    public void RegisterPlugin(object plugin) => _plugins.Add(plugin);

    public void UnregisterPlugin(object plugin) => _plugins.Remove(plugin);

    public T? GetPlugin<T>() where T : class => _plugins.OfType<T>().FirstOrDefault();

    internal async Task RaiseContentChangedAsync(ContentChangedEventArgs args)
    {
        if (ContentChanged is { } handlers)
        {
            foreach (var handler in handlers.GetInvocationList().Cast<Func<ContentChangedEventArgs, Task>>())
            {
                await handler(args);
            }
        }
    }

    internal async Task RaiseSelectionChangedAsync(SelectionChangedEventArgs args)
    {
        if (SelectionChanged is { } handlers)
        {
            foreach (var handler in handlers.GetInvocationList().Cast<Func<SelectionChangedEventArgs, Task>>())
            {
                await handler(args);
            }
        }
    }

    internal async Task DispatchInterceptedKeyAsync(string key)
    {
        if (_keyInterceptors.TryGetValue(key, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                await handler(key);
            }
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    private sealed class KeyInterception(EditorContext context, List<string> keys, Func<string, Task> handler) : IDisposable
    {
        public void Dispose()
        {
            foreach (var key in keys)
            {
                if (context._keyInterceptors.TryGetValue(key, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        context._keyInterceptors.Remove(key);
                    }
                }
            }

            context.NotifyChanged();
        }
    }
}
