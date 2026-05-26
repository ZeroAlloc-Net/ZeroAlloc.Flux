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

    /// <summary>
    /// Override to subscribe each injected <see cref="IStore{TState}"/> via
    /// <see cref="Subscribe{TState}"/>. Called from <see cref="ComponentBase.OnInitialized"/>.
    /// </summary>
    protected virtual void SubscribeStores() { }

    /// <summary>
    /// Subscribes <paramref name="onChange"/> (default: <see cref="ComponentBase.StateHasChanged"/>)
    /// to <paramref name="store"/>'s <see cref="IStore{TState}.StateChanged"/> event. Registers
    /// the unsubscribe so it runs on <see cref="Dispose"/>.
    /// </summary>
    protected void Subscribe<TState>(IStore<TState> store, Action<TState>? onChange = null)
    {
        Action<TState> handler = state =>
        {
            onChange?.Invoke(state);
            _ = InvokeAsync(StateHasChanged);
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

    /// <inheritdoc />
    public void Dispose()
    {
#pragma warning disable HLQ012 // Use CollectionsMarshal.AsSpan — clarity wins for a tiny disposal list.
        foreach (var unsub in _unsubscribers) unsub();
#pragma warning restore HLQ012
        _unsubscribers.Clear();
    }
}
