---
id: intro
title: Introduction
sidebar_label: Introduction
sidebar_position: 1
slug: /
---

# ZeroAlloc.Flux

ZeroAlloc.Flux is a source-generated Flux/Redux state management library for .NET 8 and .NET 10.
You declare state, actions and reducers as plain C# types and static methods. A Roslyn source
generator turns them into a store per state slice, one dispatcher for the whole assembly, and a DI
registration method, all at compile time.

```csharp
[Feature]
public readonly partial record struct CounterState(int Count);

public readonly record struct IncrementAction(int Amount);

public static class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };
}
```

```csharp
await dispatcher.DispatchAsync(new IncrementAction(5));
Console.WriteLine(counter.Value.Count); // 5
```

## Packages

| Package | What it contains |
|---|---|
| `ZeroAlloc.Flux` | `[Feature]`, `[Reducer]`, `IStore<TState>`, `IDispatcher`, and the source generator, bundled in the same package |
| `ZeroAlloc.Flux.Blazor` | `FluxComponent`, a Blazor base component that re-renders when a store changes |

## What you get

- **Compile-time dispatch.** The generated dispatcher has one method per action type. There is no
  handler dictionary and no reflection.
- **0 B per dispatch.** Dispatching to a record struct feature, raising `StateChanged` and reading
  `IStore<TState>.Value` allocate nothing. Tests in the repository assert each of these budgets; see
  [Performance](performance.md).
- **Thread-safe stores.** Concurrent dispatches never lose an update, and a read never sees half of
  a state.
- **Native AOT.** Both packages are marked `IsAotCompatible`, and CI publishes and runs a NativeAOT
  sample on every build.
- **Errors at build time.** Mistakes in a feature or a reducer are compiler errors with a `ZFLUX`
  ID, not runtime exceptions; see [Diagnostics](diagnostics.md).

## Where to go next

- [Getting Started](getting-started.md): install the package and dispatch your first action.
- [Core Concepts](core-concepts.md): state, actions, reducers, stores, effects and the generated
  dispatcher.
- [Blazor](blazor.md): re-render components when a store changes.
- [Performance](performance.md): what allocates, what does not, and how that is tested.
- [Diagnostics](diagnostics.md): every `ZFLUX` error and how to fix it.
