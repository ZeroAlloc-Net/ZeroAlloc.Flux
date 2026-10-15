# ZeroAlloc.Flux

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Flux.svg)](https://www.nuget.org/packages/ZeroAlloc.Flux)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

ZeroAlloc.Flux is a source-generated, zero-allocation Flux/Redux state management library for .NET 8 and .NET 10. The Roslyn source generator wires all action dispatch at compile time — no reflection and no handler dictionaries; the only interface calls are `IDispatcher` and the store lookup. Dispatch hot path allocates 0 bytes per call when handlers complete synchronously.

## Install

```shell
dotnet add package ZeroAlloc.Flux
dotnet add package ZeroAlloc.Flux.Blazor   # for Blazor apps
```

The Roslyn generator is bundled into the `ZeroAlloc.Flux` package — one `PackageReference` gives you both runtime contracts and the generator.

## Quick start

```csharp
using ZeroAlloc.Flux;

// 1. Define state — record struct (recommended) or record class.
[Feature]
public readonly partial record struct CounterState(int Count);

// 2. Define actions — plain record struct, no marker interface.
public readonly record struct IncrementAction(int Amount);
public readonly record struct ResetAction;

// 3. Define reducers — static methods, [Reducer] attribute.
public static partial class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };

    [Reducer]
    public static CounterState On(CounterState state, ResetAction _) => new(0);
}

// 4. Register + dispatch.
services.AddZeroAllocFlux();   // Scoped by default

var dispatcher = sp.GetRequiredService<IDispatcher>();
var counter = sp.GetRequiredService<IStore<CounterState>>();

await dispatcher.DispatchAsync(new IncrementAction(5));
Console.WriteLine(counter.Value.Count);   // 5
```

For Blazor:

```csharp
public partial class Counter : FluxComponent
{
    [Inject] public IStore<CounterState> CounterStore { get; set; } = default!;
    [Inject] public IDispatcher Dispatcher { get; set; } = default!;

    protected override void SubscribeStores() => Subscribe(CounterStore);
}
```

```razor
<p>Count: @CounterStore.Value.Count</p>
<button @onclick="async () => await Dispatcher.DispatchAsync(new IncrementAction(1))">+</button>
```

`FluxComponent` re-renders when a store it subscribes to changes. Subscribe in `SubscribeStores()`:

```csharp
protected override void SubscribeStores() => Subscribe(CounterStore);
```

A component that overrides `OnInitialized` must call `base.OnInitialized()`, or it never subscribes.

## Why ZeroAlloc.Flux

- **Compile-time dispatch.** The dispatcher is generated from the discovered `[Reducer]` set: `DispatchAsync<TAction>` is a chain of `typeof(TAction) ==` checks that the JIT folds to a direct call for each action type, then the matching reducers run against their stores. No handler dictionary, no reflection.
- **Zero allocation hot path.** Sync-completing dispatch returns a `ValueTask.CompletedTask`-equivalent — no state-machine box, no closure capture.
- **AOT-compatible.** No reflection, no dynamic code. `<IsAotCompatible>true</IsAotCompatible>` on both runtime packages, and CI publishes a NativeAOT smoke app.
- **Flux-coherent semantics.** One action, many feature listeners — fan-out matches Fluxor/Redux conventions.
- **Effects are yours.** 1.1.x ships no effects package. Write an effect as a service that injects `IDispatcher`,
  does its async work, and dispatches the result as another action.

## Documentation

Runnable examples live in [`samples/`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/tree/main/samples): a console app, a Blazor WebAssembly counter, and the NativeAOT smoke test. The release history is in the [CHANGELOG](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/CHANGELOG.md).

## License

MIT — see [LICENSE](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/blob/main/LICENSE).
