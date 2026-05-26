# ZeroAlloc.Flux

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Flux.svg)](https://www.nuget.org/packages/ZeroAlloc.Flux)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

ZeroAlloc.Flux is a source-generated, zero-allocation Flux/Redux state management library for .NET 8 and .NET 10. The Roslyn source generator wires all action dispatch at compile time — no reflection, no runtime dictionaries, no virtual dispatch. Dispatch hot path allocates 0 bytes per call when handlers complete synchronously.

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
}
```

```razor
<p>Count: @CounterStore.Value.Count</p>
<button @onclick="async () => await Dispatcher.DispatchAsync(new IncrementAction(1))">+</button>
```

## Why ZeroAlloc.Flux

- **Compile-time dispatch.** Every `DispatchAsync<TAction>` overload is generated from the discovered `[Reducer]` set. The dispatcher is a static `switch` — no dictionary lookups, no reflection.
- **Zero allocation hot path.** Sync-completing dispatch returns a `ValueTask.CompletedTask`-equivalent — no state-machine box, no closure capture.
- **AOT-compatible.** No reflection, no dynamic code. `<IsAotCompatible>true</IsAotCompatible>` enforced on every csproj.
- **Flux-coherent semantics.** One action, many feature listeners — fan-out matches Fluxor/Redux conventions.
- **Family integration.** Effects via `ZeroAlloc.Mediator.INotificationHandler<TAction>` (v1.1). Notify integration via `ZeroAlloc.Notify` (v1.1).

## Documentation

(Placeholder — full docs land in v1.0 release.)

## License

MIT — see [LICENSE](LICENSE).
