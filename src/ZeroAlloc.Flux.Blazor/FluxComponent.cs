using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using ZeroAlloc.Flux;

namespace ZeroAlloc.Flux.Blazor;

/// <summary>
/// Base class for Blazor components that consume <see cref="IStore{TState}"/>. Wraps
/// <see cref="ComponentBase"/> with a subscription helper that triggers <see cref="ComponentBase.StateHasChanged"/>
/// on store updates and unsubscribes on disposal.
/// </summary>
/// <remarks>
/// Override <see cref="SubscribeStores"/> and call <see cref="Subscribe{TState}"/> for each
/// injected store. v1.1 considers a generator-emitted partial override that reads
/// every <c>[Inject] IStore&lt;TState&gt;</c> property and wires automatically.
/// </remarks>
public abstract class FluxComponent : ComponentBase, IDisposable
{
    private readonly List<Action> _unsubscribers = new();

    // StateHasChanged as a delegate, created once per component. Passing the method group to
    // InvokeAsync directly would allocate a new Action on every store change.
    private readonly Action _stateHasChanged;

    /// <summary>
    /// Initializes the component.
    /// </summary>
    protected FluxComponent()
    {
        _stateHasChanged = StateHasChanged;
    }

    /// <summary>
    /// Override to subscribe each injected <see cref="IStore{TState}"/> via
    /// <see cref="Subscribe{TState}"/>. Called from <see cref="ComponentBase.OnInitialized"/>.
    /// </summary>
    protected virtual void SubscribeStores() { }

    /// <summary>
    /// Subscribes <paramref name="onChange"/> (default: <see cref="ComponentBase.StateHasChanged"/>)
    /// to <paramref name="store"/>'s <see cref="IStore{TState}.StateChanged"/> event. Registers
    /// the unsubscribe so it runs on <see cref="Dispose()"/>.
    /// </summary>
    protected void Subscribe<TState>(IStore<TState> store, Action<TState>? onChange = null)
    {
        Action<TState> handler = state =>
        {
            onChange?.Invoke(state);
            _ = InvokeAsync(_stateHasChanged);
        };
        store.StateChanged += handler;
        _unsubscribers.Add(() => store.StateChanged -= handler);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeStores();
    }

    /// <summary>
    /// Releases the component's store subscriptions. Blazor calls this when it disposes the
    /// component. Override <see cref="Dispose(bool)"/> to add cleanup of your own.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources the component holds. The base implementation removes every store
    /// subscription made through <see cref="Subscribe{TState}"/>. An override disposes its own
    /// resources and then calls <c>base.Dispose(disposing)</c>.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when called from <see cref="Dispose()"/>; <see langword="false"/>
    /// when called from a finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        foreach (var unsub in _unsubscribers) unsub();
        _unsubscribers.Clear();
    }
}
