---
id: performance
title: Performance
sidebar_label: Performance
sidebar_position: 5
---

# Performance

ZeroAlloc.Flux aims for a dispatch path that allocates nothing. This page lists what is measured,
what the numbers hold for, and what still allocates.

## Allocation budgets

Each row is an exact budget asserted by a test with `AllocationGate` from ZeroAlloc.TestHelpers.
The test runs the call 1,000 times and fails if it allocates more than the budget. CI runs these
tests on every pull request.

| Hot path | Budget | Test |
|---|---|---|
| `DispatchAsync` to one record struct feature, no `StateChanged` subscribers | 0 B | `AllocationBudgetTests.DispatchAsync_SyncPath_ZeroAllocation` |
| `DispatchAsync` fanned out to two record struct features, each raising `StateChanged` to a subscriber | 0 B | `AllocationBudgetTests.DispatchAsync_WithStateChangedSubscribers_ZeroAllocation` |
| `IStore<TState>.Value` read of a record struct feature | 0 B | `AllocationBudgetTests.StoreValue_Getter_ZeroAllocation` |
| `DispatchAsync` to a store a `FluxComponent` subscribes to, through the component's handler | 0 B | `FluxComponentTests.Dispatch_ToSubscribedComponent_ZeroAllocation` |

The first three are in `tests/ZeroAlloc.Flux.Tests`, the last in `tests/ZeroAlloc.Flux.Blazor.Tests`.
Each test resolves `IDispatcher` and `IStore<TState>` from a container built with
`AddZeroAllocFlux()`, the way an application calls them.

## Why dispatch does not allocate

- **The dispatcher is generated.** `DispatchAsync<TAction>` is a chain of `typeof(TAction) == ...`
  checks that the JIT reduces to one direct call. There is no dictionary lookup, no reflection and
  no boxing of the action; `Unsafe.As` passes it on by reference.
- **Reducers are called through cached delegates.** The generated code passes each reducer as a
  `static` lambda, which the compiler creates once and reuses.
- **The store completes synchronously.** The update returns a completed `ValueTask`, so there is no
  async state machine to allocate. When an action fans out to several features, the generated
  `async ValueTask` helper also completes synchronously and allocates nothing.
- **A record struct feature lives in the store.** The new state is copied into the store's field,
  not boxed or wrapped.

## What still allocates

The budgets above hold for the library's own work. These allocate, and are yours to control:

- **A record class feature** allocates the new state object your reducer returns, on every update.
  Use a `readonly record struct` if updates are frequent.
- **Your reducers and handlers.** A reducer that builds a string or a collection, or a
  `StateChanged` handler that captures variables or does work, allocates as much as that code does.
- **Subscribing.** Adding a `StateChanged` handler, or calling `FluxComponent.Subscribe`, allocates
  once when you subscribe, not per change.
- **Resolving services.** The dispatcher resolves each store from the container on every dispatch.
  The budgets above use the default Microsoft.Extensions.DependencyInjection container and include
  that lookup. With a different container, measure it.
- **Rendering.** In Blazor, the render that follows a change is the framework's own work.

## Thread safety at no allocation

The stores are safe for concurrent dispatch and reads without allocating:

- A record struct feature is written under a lock, and read through a sequence lock: a read that
  races a write retries until it copies a whole state.
- A record class feature is swapped with a compare-and-swap, and read as a single reference.

`ConcurrencyTests` in `tests/ZeroAlloc.Flux.Tests` dispatches from eight threads at once and checks
that no update is lost and no read sees a torn state.

## Native AOT

Both packages set `IsAotCompatible`. The generated code uses no reflection and no dynamic code. CI
publishes `samples/ZeroAlloc.Flux.AotSmoke` with `PublishAot=true` and runs the binary, and builds
the Blazor WebAssembly sample, on every build.

## Benchmarks

`benchmarks/ZeroAlloc.Flux.Benchmarks` measures the three core hot paths above with
BenchmarkDotNet and `[MemoryDiagnoser]`. To run it on your own machine:

```bash
dotnet run -c Release --project benchmarks/ZeroAlloc.Flux.Benchmarks -- --filter '*'
```

Timings depend on the machine, so this page does not quote them. The allocation column is the part
the budgets above guarantee.
