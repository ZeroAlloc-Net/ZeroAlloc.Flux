# ZeroAlloc.Flux

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Flux.svg)](https://www.nuget.org/packages/ZeroAlloc.Flux)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.Flux/graph/badge.svg)](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.Flux)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Source-generated Flux/Redux state management for .NET and Blazor, an alternative to Fluxor: no reflection, no allocations when you dispatch, NativeAOT-ready.

## Why ZeroAlloc.Flux

- **0 B per dispatch.** Dispatching an action, fanning it out to several features, notifying subscribers and reading the state allocate nothing; tests assert each path on net8.0 and net10.0, including dispatch to a store that a Blazor `FluxComponent` subscribes to.
- **Dispatch is generated code.** The dispatcher is built at compile time from your `[Reducer]` methods and resolves to a direct call per action type: no handler dictionary, no reflection.
- **Works under NativeAOT and trimming.** CI publishes and runs a NativeAOT app, and builds a Blazor WebAssembly app, on every build.
- **Compile-time diagnostics.** A wrong reducer signature, a duplicate reducer or a feature that is not `partial` stops the build with a `ZFLUX` error instead of failing at runtime.

## Install

```bash
dotnet add package ZeroAlloc.Flux
```

The source generator ships inside the package. A console app also needs a DI container, for example `dotnet add package Microsoft.Extensions.DependencyInjection`; ASP.NET Core and Blazor already have one.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;

using var sp = new ServiceCollection().AddZeroAllocFlux().BuildServiceProvider();
var dispatcher = sp.GetRequiredService<IDispatcher>();
var counter = sp.GetRequiredService<IStore<CounterState>>();

counter.StateChanged += state => Console.WriteLine($"Count is now {state.Count}");

await dispatcher.DispatchAsync(new Increment(5));  // Count is now 5
await dispatcher.DispatchAsync(new Increment(3));  // Count is now 8

[Feature]
public readonly partial record struct CounterState(int Count);

public readonly record struct Increment(int Amount);

public static class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, Increment action)
        => state with { Count = state.Count + action.Amount };
}
```

State is a record, actions are plain types with no marker interface, and reducers are static methods. The generator writes the store, the dispatcher and `AddZeroAllocFlux`.

## What you get

- **Stores per state slice**, safe for concurrent dispatch and reads, with a `StateChanged` event. [Core concepts](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/core-concepts.md)
- **One action, many features**: every reducer for an action runs, as in Redux and Fluxor.
- **Custom initial state** through a factory that can use your services. [Initial state](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/core-concepts.md#initial-state)
- **Effects as plain services** that inject `IDispatcher`, do async work and dispatch the result. [Effects](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/core-concepts.md#effects)
- **Blazor support**: `ZeroAlloc.Flux.Blazor` adds `FluxComponent`, which re-renders when a store it subscribes to changes. [Blazor guide](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/blazor.md)
- **Build-time checks**, ZFLUX001 to ZFLUX009. [Diagnostics](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/diagnostics.md)
- **Targets** `net8.0` and `net10.0`.

## Performance

Exact allocation budgets, each asserted by an `AllocationGate` test over 1,000 calls in CI:

| Hot path | Allocated |
|---|---|
| `DispatchAsync` to one record struct feature | 0 B |
| `DispatchAsync` fanned out to two features, each raising `StateChanged` to a subscriber | 0 B |
| `IStore<TState>.Value` read | 0 B |

The budgets hold for the library's own work; a record class feature allocates the new state your reducer returns. What still allocates, why dispatch does not, and how to run the benchmarks: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/performance.md).

## Learn more

- Documentation site: [flux.zeroalloc.net](https://flux.zeroalloc.net)
- Documentation source: [docs/](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/tree/main/docs), starting with [getting started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/docs/getting-started.md)
- Runnable examples: [`samples/`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/tree/main/samples) has a console app, a Blazor WebAssembly counter and the NativeAOT smoke test
- Related packages: the [ZeroAlloc family](https://github.com/ZeroAlloc-Net), including [ZeroAlloc.Mediator](https://github.com/ZeroAlloc-Net/ZeroAlloc.Mediator) for request and notification dispatch
- Contributing: issues and pull requests are welcome on [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/issues)
- License: [MIT](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/LICENSE)
