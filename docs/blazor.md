---
id: blazor
title: Blazor
sidebar_label: Blazor
sidebar_position: 4
---

# Blazor

`ZeroAlloc.Flux.Blazor` adds `FluxComponent`, a base component that re-renders when a store it
subscribes to changes. It works with Blazor Server and Blazor WebAssembly.

## Install and register

```bash
dotnet add package ZeroAlloc.Flux
dotnet add package ZeroAlloc.Flux.Blazor
```

```csharp
using ZeroAlloc.Flux.Blazor;
using ZeroAlloc.Flux.Generated;

builder.Services.AddZeroAllocFlux();
builder.Services.AddZeroAllocFluxBlazor();
```

`AddZeroAllocFlux` registers the dispatcher and the stores. `AddZeroAllocFluxBlazor` registers
nothing yet. Calling it now means Blazor-specific services that a later version adds reach your
app without a code change.

## Subscribe a component

Inherit from `FluxComponent`, inject the stores you read, and subscribe to each one in
`SubscribeStores`:

```razor
@page "/counter"
@inherits FluxComponent
@inject IStore<CounterState> CounterStore
@inject IDispatcher Dispatcher

<p>Count: @CounterStore.Value.Count</p>
<button @onclick="Increment">+1</button>

@code {
    protected override void SubscribeStores() => Subscribe(CounterStore);

    private async Task Increment() => await Dispatcher.DispatchAsync(new IncrementAction(1));
}
```

Add the namespaces to `_Imports.razor` so every component can use them:

```razor
@using ZeroAlloc.Flux
@using ZeroAlloc.Flux.Blazor
```

The same component, written as a C# class:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Blazor;

public sealed class CounterView : FluxComponent
{
    [Inject] public IStore<CounterState> CounterStore { get; set; } = default!;

    protected override void SubscribeStores() => Subscribe(CounterStore);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddContent(1, $"Count: {CounterStore.Value.Count}");
        builder.CloseElement();
    }
}
```

### How it works

- `FluxComponent.OnInitialized` calls `SubscribeStores`. If your component overrides
  `OnInitialized`, call `base.OnInitialized()`, or the component never subscribes.
- `Subscribe` adds a `StateChanged` handler that calls `StateHasChanged` through `InvokeAsync`.
  The re-render is therefore scheduled on the component's renderer, even when the dispatch ran on
  another thread.
- `FluxComponent` implements `IDisposable`. When Blazor disposes the component, it removes every
  handler it added, so a removed component receives no further changes.
- To clean up resources of your own, such as a `CancellationTokenSource` or a timer, override
  `protected virtual void Dispose(bool disposing)` and call `base.Dispose(disposing)`. Do not
  re-implement `IDisposable` in the component: the base call is what removes the subscriptions.

```csharp
private readonly CancellationTokenSource _cts = new();

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _cts.Cancel();
        _cts.Dispose();
    }
    base.Dispose(disposing);
}
```

### React to a change

`Subscribe` takes an optional callback. It runs with the new state, before the component is asked
to re-render:

```csharp
protected override void SubscribeStores()
{
    Subscribe(CounterStore, state => ShowLimitWarning = state.Count >= 10);
}

private bool ShowLimitWarning { get; set; }
```

The callback runs on the dispatching thread, inside the store's `StateChanged` event, so keep it
short and do not dispatch from it.

### Several stores

Call `Subscribe` once per store. The component re-renders when any of them changes:

```csharp
protected override void SubscribeStores()
{
    Subscribe(CartStore);
    Subscribe(BadgeStore);
}
```

## Lifetimes in Blazor

`AddZeroAllocFlux` registers the dispatcher and the stores as scoped by default:

| Hosting model | Scoped means |
|---|---|
| Blazor WebAssembly | one instance for the app in that browser tab |
| Blazor Server, or the interactive server render mode | one instance per circuit, so each user connection has its own state |

On the server, use `AddZeroAllocFlux(ServiceLifetime.Singleton)` only if every user should share
one state.

## Effects in components

Put async work in an effect service rather than in the component, and call it from an event
handler. See [Effects](core-concepts.md#effects).

```razor
@inherits FluxComponent
@inject IStore<WeatherState> WeatherStore
@inject WeatherEffects Effects

@if (WeatherStore.Value.Loading)
{
    <p>Loading…</p>
}
else
{
    <p>@WeatherStore.Value.Forecast</p>
}
<button @onclick="Refresh">Refresh</button>

@code {
    protected override void SubscribeStores() => Subscribe(WeatherStore);

    private Task Refresh() => Effects.LoadAsync("Amsterdam", CancellationToken.None);
}
```

## Allocation

Dispatching an action to a store that a `FluxComponent` subscribes to allocates 0 B. That covers
the dispatch, the reducer, the `StateChanged` event and the component's handler, and a test in
`tests/ZeroAlloc.Flux.Blazor.Tests` asserts it. The render that follows is Blazor's own work and
is not part of that budget. See [Performance](performance.md).
