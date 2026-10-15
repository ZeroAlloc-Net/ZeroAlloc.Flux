---
id: getting-started
title: Getting Started
sidebar_label: Getting Started
sidebar_position: 2
---

# Getting Started

## Install

```bash
dotnet add package ZeroAlloc.Flux
```

The source generator ships inside `ZeroAlloc.Flux`, so this one package reference gives you the
runtime types and the generator. Both packages target `net8.0` and `net10.0`.

For a Blazor app, also add the Blazor adapter; see [Blazor](blazor.md):

```bash
dotnet add package ZeroAlloc.Flux.Blazor
```

## 1. Define the state

A state slice is called a *feature*. Mark it with `[Feature]` and declare it `partial`. A
`readonly record struct` is the recommended shape:

```csharp
using ZeroAlloc.Flux;

[Feature]
public readonly partial record struct CounterState(int Count);
```

The store starts from `new CounterState()`, which for a record struct is `Count == 0`. To start
from something else, see [Initial state](core-concepts.md#initial-state).

## 2. Define the actions

An action is any type. It needs no base class and no marker interface:

```csharp
public readonly record struct IncrementAction(int Amount);
public readonly record struct ResetAction;
```

## 3. Write the reducers

A reducer is a `public static` method marked `[Reducer]`. It takes the current state and an action,
and returns the new state:

```csharp
public static class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };

    [Reducer]
    public static CounterState On(CounterState state, ResetAction _) => new(0);
}
```

The generator finds the action types from the reducer signatures, so there is nothing else to
register.

## 4. Register and dispatch

The generator emits an `AddZeroAllocFlux` extension method in the `ZeroAlloc.Flux.Generated`
namespace. It registers the dispatcher and one store per feature:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;

var services = new ServiceCollection();
services.AddZeroAllocFlux();
using var provider = services.BuildServiceProvider();

var dispatcher = provider.GetRequiredService<IDispatcher>();
var counter = provider.GetRequiredService<IStore<CounterState>>();

counter.StateChanged += state => Console.WriteLine($"Counter changed to {state.Count}");

await dispatcher.DispatchAsync(new IncrementAction(5));
await dispatcher.DispatchAsync(new IncrementAction(3));
await dispatcher.DispatchAsync(new ResetAction());

Console.WriteLine($"Final: {counter.Value.Count}");
```

Output:

```text
Counter changed to 5
Counter changed to 8
Counter changed to 0
Final: 0
```

`IDispatcher` and `IStore<TState>` are ordinary services, so in an ASP.NET Core, worker or Blazor
app you call `builder.Services.AddZeroAllocFlux()` and inject them where you need them.

## Lifetime

`AddZeroAllocFlux` registers the dispatcher and the stores as **scoped** by default. Pass a lifetime
to change that:

```csharp
services.AddZeroAllocFlux(ServiceLifetime.Singleton);
```

The dispatcher and every store always share one lifetime. Pick singleton for one state shared by
the whole process, and scoped for one state per scope, such as one per Blazor Server circuit. In
an app whose container validates scopes, such as ASP.NET Core in Development, resolve scoped
stores from a scope rather than from the root provider.

## Next steps

- [Core Concepts](core-concepts.md) explains features, reducers, stores, effects and what the
  generator emits.
- [Blazor](blazor.md) shows how a component re-renders when a store changes.
- The [`samples/`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/tree/main/samples) folder has
  this console app, a Blazor WebAssembly counter and the NativeAOT smoke test.
