using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazText;

/// <summary>
/// Base class for editor plugins. Place a derived component inside a
/// <see cref="BlazTextEditor"/>; it receives the editor's <see cref="EditorContext"/>,
/// registers itself for discovery, and cleans up on disposal. Override
/// <see cref="OnEditorInitializedAsync"/> to register toolbar items, panels, providers, etc.
/// </summary>
public abstract class BlazTextPluginBase : ComponentBase, IAsyncDisposable
{
    [CascadingParameter]
    public EditorContext? Context { get; set; }

    /// <summary>The editor's context. Only valid after initialization.</summary>
    protected EditorContext Editor => Context
        ?? throw new InvalidOperationException($"{GetType().Name} must be placed inside a <BlazTextEditor>.");

    /// <summary>Shorthand for <c>Editor.Api</c>.</summary>
    protected EditorApi Api => Editor.Api;

    protected override async Task OnInitializedAsync()
    {
        Editor.RegisterPlugin(this);
        await OnEditorInitializedAsync();
    }

    /// <summary>Register the plugin's contributions with <see cref="Editor"/> here.</summary>
    protected virtual Task OnEditorInitializedAsync() => Task.CompletedTask;

    /// <summary>Unregister anything registered in <see cref="OnEditorInitializedAsync"/> here.</summary>
    protected virtual ValueTask OnDisposingAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        Context?.UnregisterPlugin(this);

        try
        {
            await OnDisposingAsync();
        }
        catch (Exception e) when (e is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // A third-party plugin doing its own interop in OnDisposingAsync should not take the
            // circuit down when the browser is already gone. This cannot resume the rest of that
            // plugin's cleanup — only the plugin can order its own teardown so the local work
            // happens before any interop — but it does contain the blast radius.
        }

        GC.SuppressFinalize(this);
    }
}
