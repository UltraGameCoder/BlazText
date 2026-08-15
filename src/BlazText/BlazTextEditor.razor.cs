using BlazText.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazText;

/// <summary>
/// The BlazText rich text editor. On its own it renders a bare contenteditable surface with
/// two-way content binding; features are added by placing plugin components inside it:
/// <code>
/// &lt;BlazTextEditor @bind-Document="doc"&gt;
///     &lt;SearchPlugin /&gt;
///     &lt;ImagePlugin /&gt;
/// &lt;/BlazTextEditor&gt;
/// </code>
/// </summary>
public partial class BlazTextEditor : ComponentBase, IAsyncDisposable
{
    private readonly EditorContext _context;
    private ElementReference _surface;
    private BlazTextDocument _document = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<BlazTextEditor>? _selfRef;
    private string _lastKnownContent = string.Empty;
    private string[] _appliedInterceptKeys = [];
    private bool _pendingContentPush;
    private bool _disposed;

    public BlazTextEditor()
    {
        _context = new EditorContext(new ApiImplementation(this));
        _context.Changed += OnContextChanged;
    }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Resolved lazily so the editor works in a host with no logging registered.</summary>
    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    private ILogger? _logger;

    /// <summary>The full document (content + images + plugin data). Use for save/load scenarios.</summary>
    [Parameter]
    public BlazTextDocument? Document { get; set; }

    [Parameter]
    public EventCallback<BlazTextDocument> DocumentChanged { get; set; }

    /// <summary>Lightweight alternative to <see cref="Document"/>: just the HTML content.</summary>
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Plugin components (and any other content) to attach to this editor.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The editor's extension surface, also cascaded to child plugins.</summary>
    public EditorContext Context => _context;

    /// <summary>Programmatic access to the editor for app code holding a <c>@ref</c>.</summary>
    public EditorApi Api => _context.Api;

    protected override void OnParametersSet()
    {
        if (Document is not null)
        {
            if (!ReferenceEquals(Document, _document))
            {
                // A new document was supplied (e.g. loading a saved one): adopt it.
                _document = Document;
            }

            if (_document.Content != _lastKnownContent)
            {
                _pendingContentPush = true;
            }
        }
        else if (Value is not null && Value != _lastKnownContent)
        {
            _document.Content = Value;
            _pendingContentPush = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await RenderInteropAsync(firstRender);
        }
        catch (Exception e) when (IsDeadBrowserContext(e))
        {
            // The circuit dropped between the render and this callback — navigating away during
            // page load is a more common event than the teardown path. Nothing to do here.
            Log(e, "Editor interop after render was abandoned");
        }
    }

    private async Task RenderInteropAsync(bool firstRender)
    {
        if (firstRender)
        {
            _selfRef = DotNetObjectReference.Create(this);
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/BlazText/BlazTextEditor.razor.js");

            if (_disposed)
            {
                // Disposed while the import was in flight. DisposeAsync already ran and saw no
                // module, so nothing will ever call dispose() for this one — release it here
                // rather than leaking the handle and the selectionchange listener init adds.
                await module.DisposeAsync();
                return;
            }

            _module = module;
            await _module.InvokeVoidAsync("init", _surface, _selfRef);
            _pendingContentPush = true;
        }

        if (_module is null)
        {
            return;
        }

        if (_pendingContentPush)
        {
            _pendingContentPush = false;
            _lastKnownContent = _document.Content;
            await _module.InvokeVoidAsync("setContent", _surface, _document.Content, BuildImageMap());
        }

        var interceptKeys = _context.InterceptedKeys.ToArray();
        if (!interceptKeys.SequenceEqual(_appliedInterceptKeys))
        {
            _appliedInterceptKeys = interceptKeys;
            await _module.InvokeVoidAsync("setInterceptKeys", _surface, interceptKeys);
        }
    }

    [JSInvokable]
    public async Task NotifyContentChangedAsync(string html, string textBeforeCaret, CaretRect? caret)
    {
        if (html == _lastKnownContent)
        {
            return;
        }

        _lastKnownContent = html;
        _document.Content = html;
        await RaiseBindingsAsync();
        await _context.RaiseContentChangedAsync(new ContentChangedEventArgs
        {
            Html = html,
            Document = _document,
            TextBeforeCaret = textBeforeCaret,
            Caret = caret,
        });
    }

    [JSInvokable]
    public Task NotifySelectionChangedAsync(string textBeforeCaret, CaretRect? caret, bool isCollapsed) =>
        _context.RaiseSelectionChangedAsync(new SelectionChangedEventArgs
        {
            TextBeforeCaret = textBeforeCaret,
            Caret = caret,
            IsCollapsed = isCollapsed,
        });

    [JSInvokable]
    public Task NotifyKeyInterceptedAsync(string key) => _context.DispatchInterceptedKeyAsync(key);

    private async Task RaiseBindingsAsync()
    {
        await ValueChanged.InvokeAsync(_lastKnownContent);
        await DocumentChanged.InvokeAsync(_document);
    }

    private Dictionary<string, string> BuildImageMap() =>
        _document.Images.ToDictionary(i => i.Id, i => i.ToDataUri());

    private void OnContextChanged() => _ = InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context.Changed -= OnContextChanged;

        var module = _module;
        _module = null;

        try
        {
            if (module is not null)
            {
                // Two independent steps. A failure telling JS to tear down must not also skip
                // releasing the module handle, or the JS object table keeps an entry per editor
                // mount — the same leak class this method exists to fix.
                await TryTeardownAsync(() => module.InvokeVoidAsync("dispose", _surface).AsTask(), "JS dispose failed");
                await TryTeardownAsync(() => module.DisposeAsync().AsTask(), "Releasing the JS module failed");
            }
        }
        finally
        {
            _selfRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task TryTeardownAsync(Func<Task> step, string message)
    {
        try
        {
            await step();
        }
        catch (Exception e) when (IsDeadBrowserContext(e))
        {
            // Expected during teardown; nothing on the other side to clean up.
        }
        catch (JSException e)
        {
            // A genuine error inside the module, not a dead browser. Swallowing it keeps
            // teardown going, but silently doing so would hide a real bug forever — so unlike
            // the dead-context case this one is always logged.
            Log(e, message);
        }
    }

    /// <summary>
    /// True for the exceptions a browser context that is going away produces. Deliberately
    /// excludes <see cref="JSException"/>: that means the JS function itself threw, which is a
    /// bug to surface rather than a disconnect to absorb.
    /// </summary>
    private static bool IsDeadBrowserContext(Exception e) =>
        e is JSDisconnectedException or ObjectDisposedException or OperationCanceledException;

    private void Log(Exception e, string message) =>
        (_logger ??= (Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger<BlazTextEditor>())
            ?.LogDebug(e, "{Message}", message);

    private sealed class ApiImplementation(BlazTextEditor editor) : EditorApi
    {
        public override BlazTextDocument Document => editor._document;

        public override async Task<string> GetContentAsync() =>
            editor._module is null ? editor._document.Content : await editor._module.InvokeAsync<string>("getContent", editor._surface);

        public override async Task SetContentAsync(string html)
        {
            editor._document.Content = html;
            editor._lastKnownContent = html;

            if (editor._module is not null)
            {
                await editor._module.InvokeVoidAsync("setContent", editor._surface, html, editor.BuildImageMap());
            }

            await editor.RaiseBindingsAsync();
            await editor._context.RaiseContentChangedAsync(new ContentChangedEventArgs
            {
                Html = html,
                Document = editor._document,
            });
        }

        public override Task<string> GetPlainTextAsync() =>
            editor._module?.InvokeAsync<string>("getPlainText", editor._surface).AsTask() ?? Task.FromResult(string.Empty);

        public override Task InsertHtmlAtSelectionAsync(string html) =>
            InvokeVoidAsync("insertHtml", html);

        public override Task ReplaceTextBeforeCaretAsync(int charCount, string text) =>
            InvokeVoidAsync("replaceTextBeforeCaret", charCount, text);

        public override Task ApplyFormatAsync(string command, string? value = null) =>
            InvokeVoidAsync("applyFormat", command, value);

        public override Task HighlightRangesAsync(IReadOnlyList<TextRange> ranges, int activeIndex = -1) =>
            InvokeVoidAsync("highlightRanges", ranges, activeIndex);

        public override Task ClearHighlightsAsync() => InvokeVoidAsync("clearHighlights");

        public override Task ScrollToHighlightAsync(int index) => InvokeVoidAsync("scrollToHighlight", index);

        public override Task FocusAsync() => InvokeVoidAsync("focusEditor");

        public override async Task UpdateDocumentAsync(Action<BlazTextDocument> mutate)
        {
            mutate(editor._document);
            await editor.DocumentChanged.InvokeAsync(editor._document);
        }

        private async Task InvokeVoidAsync(string method, params object?[] args)
        {
            if (editor._module is null || editor._disposed)
            {
                return;
            }

            try
            {
                await editor._module.InvokeVoidAsync(method, [editor._surface, .. args]);
            }
            catch (Exception e) when (e is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
                // Plugins clean up through this API in OnDisposingAsync — SearchPlugin clears
                // its highlights there — which on a torn-down circuit means talking to a
                // browser that is already gone. Nothing a plugin can act on, and letting it
                // escape would abort the rest of the component tree's disposal.
                // A JSException (a genuine error inside the module) is deliberately not caught.
            }
        }
    }
}
