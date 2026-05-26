# ZeroAlloc.Flux v1.0.0 — Design

**Date:** 2026-05-26
**Scope:** New ZeroAlloc family package — source-generated, zero-allocation Flux/Redux state management. Ships as a mono-repo containing `ZeroAlloc.Flux` (UI-agnostic core + generator bundled) and `ZeroAlloc.Flux.Blazor` (host adapter). v1.0.0 covers the minimal end-to-end shape; effects, failable reducers, and the mutation analyzer are explicit out-of-scope follow-ups.

## Background

The org-wide backlog at `docs/BACKLOG.md:305` already sketches a `ZeroAlloc.Fluxor` package — a ZeroAlloc-idiomatic replacement for Fluxor's reflection-based dispatch. This design refines that sketch into something shippable as **v1**, and splits the speculative single-package proposal into a **core + host adapter** mono-repo (matching the `ZeroAlloc.Mediator` / `ZeroAlloc.Mediator.Authorization` pattern). Both gating dependencies named in the backlog (`ZeroAlloc.Notify`, `ZeroAlloc.Mediator`) are stable on NuGet.

The package name `ZeroAlloc.Flux` (rather than `ZeroAlloc.Fluxor`) signals the pattern (Flux/Redux state management) rather than directly evoking the Fluxor product. The mental model is identical; the implementation is novel.

## Goal

Ship the smallest end-to-end Flux/Redux contract that:

- Discovers `[Feature]` state slices + `[Reducer]` methods at compile time.
- Routes `IDispatcher.DispatchAsync<TAction>(action)` to all features whose reducer signatures match the action type, with zero reflection at dispatch time.
- Notifies subscribers (Blazor components via `FluxComponent`) of state changes.
- Preserves the zero-allocation hot path: sync-completing dispatch allocates 0 B; ValueTask state machine only allocates on genuine handler suspension.
- Compiles under PublishAot=true with no IL2026 / IL3050 warnings.

## Decisions

### D-1: scope — minimal core + Blazor, v1.0.0

Both packages ship simultaneously at v1.0.0. The v1 surface is:

- **In:** `[Feature]`, `[Reducer]`, `IStore<TState>`, `IDispatcher`, generator with compile-time switch dispatch, `Notify`-free plain `event Action<TState> StateChanged` on each store, `FluxComponent` base class for Blazor, `AddZeroAllocFlux()` DI extension.
- **Out (v1.1+):** Effects via `Mediator.INotificationHandler<TAction>`. Failable reducers returning `Result<TState, ReducerError>`. Mutation-detection analyzer. Generator-emitted auto-subscription on `FluxComponent`. `ZeroAlloc.Notify` integration on the store side. Fluxor ecosystem compatibility (DevTools, middleware).

**Considered and rejected:**

- **Core-only v1.** Ship `ZeroAlloc.Flux` alone first; `Flux.Blazor` follows in a second roll. Cleaner validation but loses the "real end-to-end demo" that motivates the package.
- **Core + Blazor "useful" v1** including Effects + Notify integration. Larger roll; Effects are the most complex piece (async + re-dispatch + cancellation) and benefit from settling reducer/dispatch shape first.
- **Full proposal in v1** (everything in `docs/BACKLOG.md:305`). 3–4× the v1 work; defers shipping by weeks.

### D-2: repo layout — mono-repo

Single repo `ZeroAlloc-Net/ZeroAlloc.Flux` containing both packages:

```
ZeroAlloc.Flux/
├── src/
│   ├── ZeroAlloc.Flux/                       # core package
│   ├── ZeroAlloc.Flux.Generator/             # Roslyn generator (bundled into core nupkg)
│   └── ZeroAlloc.Flux.Blazor/                # host adapter package
├── tests/
│   ├── ZeroAlloc.Flux.Tests/                 # runtime tests + allocation budgets
│   ├── ZeroAlloc.Flux.Generator.Tests/       # generator snapshot + diagnostic tests
│   └── ZeroAlloc.Flux.Blazor.Tests/          # bunit tests for FluxComponent
└── samples/
    ├── ZeroAlloc.Flux.Sample/                # console / worker fixture
    └── ZeroAlloc.Flux.AotSmoke/              # PublishAot=true CI gate
```

Two NuGet packages: `ZeroAlloc.Flux` (core + generator bundled) and `ZeroAlloc.Flux.Blazor`. release-please cuts both via the standard multi-package config used in `ZeroAlloc.Mediator`.

**Considered and rejected:**

- **Two repos** (`ZeroAlloc.Flux` and `ZeroAlloc.Flux.Blazor`). Matches the older `ZeroAlloc.Authorization` / `ZeroAlloc.Mediator.Authorization` split. Rejected — that split was a historical accident; `ZeroAlloc.Mediator`'s mono-repo pattern is the lesson-learned shape.

### D-3: action dispatch — fan-out across features, one reducer per (state, action) within a feature

When `dispatcher.DispatchAsync(new IncrementAction(1))` is called, the generator-emitted dispatcher updates *every* feature whose reducer signature matches `IncrementAction`. Inside a single feature, having two reducers for the same action type is a `ZFLUX002` compile error.

Matches Fluxor's basic semantics. Cross-feature fan-out covers the natural "one action, many listeners" idiom (e.g., a `UserLoggedOutAction` resets multiple feature slices). Chained reducers within a feature (Fluxor's actual behaviour) are explicit YAGNI — adds ordering footguns; defer to v1.1 if a real consumer wants them.

**Considered and rejected:**

- **Chained reducers within a feature.** Fluxor-equivalent. Rejected for v1 — source-order isn't deterministic across partial classes; introduces analyzer surface to warn about non-deterministic order.
- **Single-feature dispatch** (action belongs to exactly one feature). Zero ambiguity, but breaks the Fluxor "one action, many listeners" idiom.

### D-4: state storage — both `record struct` (promoted) and `record class` supported

The generator emits different update strategies per `[Feature]`:

- **`record struct` (recommended default)** — per-store `lock(_lock)` around the state field. Zero-allocation per update. Slightly higher per-dispatch overhead than CAS on the uncontested path, but contention is negligible for typical Blazor state slices. Docs steer users here.
- **`record class` (opt-in for concurrent backends)** — lock-free CAS via `Interlocked.CompareExchange<TState>(ref _state, newState, oldState)`. Allocates one record per update (user-controlled cost). Lock-free dispatch makes it suitable for high-concurrency backend stores.

Both compile under PublishAot=true. The generator chooses the strategy automatically based on `INamedTypeSymbol.IsValueType`.

**Considered and rejected:**

- **`record struct` only.** Honors the original backlog example literally. Rejected — leaves concurrent backend users with no escape hatch.
- **`record class` only.** Flux/Redux-orthodox (immutable reference state). Rejected — undercuts the zero-alloc promise for the most common UI case.

### D-5: dispatch shape — async-only with sync-completion fast path

`IDispatcher.DispatchAsync<TAction>(TAction action)` returns `ValueTask`. The reducer runs synchronously inside the dispatcher; only the change-notification fan-out is awaited. When all subscribers complete synchronously, the `ValueTask` returns synchronously with no allocation.

Flux/Redux purists may grumble — Redux/Fluxor are sync-dispatch — but async-first composes naturally with:

- The future Effects-via-Mediator integration (v1.1) — Mediator handlers are already async.
- Future Notify integration — Notify is async-first.
- Backpressure for async subscribers (logging, persistence, telemetry).

Blazor `@onclick` becomes `async () => await Dispatcher.DispatchAsync(action)` instead of `() => Dispatcher.Dispatch(action)`. Blazor event handlers natively support async lambdas; the verbosity is real but trivial.

**Considered and rejected:**

- **Sync `Dispatch`-only.** Most flux-coherent. Rejected — closes the door on Effects/Notify integration without a contract break in v1.1.
- **Both `Dispatch` (sync) and `DispatchAsync`.** Considered as a convenience-overload story. Rejected — `Dispatch(action)` would have to be fire-and-forget over `DispatchAsync` (known anti-pattern, swallows errors), or a sync-over-async block (`.GetAwaiter().GetResult()`, also an anti-pattern). Single `DispatchAsync` keeps the contract honest.

### D-6: initial state — default ctor + opt-in factory

By default, each `[Feature]` store initializes via `new TState()`. For features that need configuration / DI / runtime data, opt-in via `[Feature(InitialState = nameof(GetInitialState))]` naming a `public static TState GetInitialState(IServiceProvider sp)` factory.

```csharp
[Feature]
public readonly partial record struct CounterState(int Count);  // default-init: new CounterState() => Count = 0

[Feature(InitialState = nameof(Init))]
public readonly partial record struct ConfiguredState(string Name)
{
    public static ConfiguredState Init(IServiceProvider sp)
        => new(sp.GetRequiredService<IConfig>().DefaultName);
}
```

**Considered and rejected:**

- **Default ctor only.** Forces users with config-bound state to dispatch a `LoadAction` after construction. More friction; common pattern (DI-driven init).
- **Factory only, no default-ctor path.** Adds boilerplate even for trivial `record struct CounterState(int Count)`.

### D-7: DI registration — `Scoped` by default, configurable

```csharp
services.AddZeroAllocFlux();                            // Scoped — Blazor Server / per-circuit
services.AddZeroAllocFlux(ServiceLifetime.Singleton);   // Blazor WASM / worker / backend
```

`AddZeroAllocFlux()` is generator-emitted as an extension method. It registers `IDispatcher` + every discovered `Store<TState>` at the requested lifetime. Default is `Scoped` to match Fluxor's per-circuit isolation in Blazor Server (each user gets their own state).

**Considered and rejected:**

- **Per-feature configurable via `[Feature(Lifetime = ...)]`.** Most flexible. Rejected — adds attribute surface and forces every user to think about lifetime per slice. Defer to v1.1 if a real consumer needs mixed lifetimes.
- **`Singleton` only.** Breaks Blazor Server multi-user state isolation.

## Design

### User-facing API

```csharp
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Blazor;

// 1. Define state — record struct (promoted) or record class.
[Feature]
public readonly partial record struct CounterState(int Count);

// 2. Define actions — plain record struct / record class. No marker, no attribute.
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

// 4. Register in Program.cs / Startup.cs.
services.AddZeroAllocFlux();              // Scoped (Blazor Server)
services.AddZeroAllocFluxBlazor();        // FluxComponent infra (Blazor only)

// 5. Consume in a Blazor component.
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

### Generator-emitted code

**Per `[Feature] TState`:**

- A `Store<TState>` implementation registered as `IStore<TState>`.
- Holds `private TState _state` field; for value-type state, paired with `private readonly object _lock = new()`.
- Exposes `TState Value { get; }` + `event Action<TState> StateChanged`.
- `internal ValueTask UpdateAsync(TState newState)`:
  - struct path: `lock(_lock) { _state = newState; } StateChanged?.Invoke(newState); return ValueTask.CompletedTask;`
  - class path: CAS loop on `_state` reference; fires `StateChanged` after successful swap.
- Initial state: `new TState()` or `TState.GetInitialState(sp)` per D-6.

**For the global `IDispatcher`:**

- Single emitted `DispatcherImpl` class implementing `IDispatcher`.
- One `DispatchAsync<TAction>(TAction action)` overload per discovered action type, with the body being a fan-out:

```csharp
public ValueTask DispatchAsync(IncrementAction action)
{
    var counterStore = _sp.GetRequiredService<Store<CounterState>>();
    var newCounterState = CounterReducers.On(counterStore.Value, action);
    return counterStore.UpdateAsync(newCounterState);
    // If multiple features have reducers for IncrementAction, the generator emits
    // sequential awaits or Task.WhenAll depending on D-5 fast-path semantics.
}
```

When multiple features match the same action, the generator emits:

```csharp
public async ValueTask DispatchAsync(IncrementAction action)
{
    var counterStore = _sp.GetRequiredService<Store<CounterState>>();
    var otherStore = _sp.GetRequiredService<Store<OtherState>>();
    await counterStore.UpdateAsync(CounterReducers.On(counterStore.Value, action));
    await otherStore.UpdateAsync(OtherReducers.On(otherStore.Value, action));
}
```

**For DI:**

- Generator emits `public static IServiceCollection AddZeroAllocFlux(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)` registering `IDispatcher` + every `Store<TState>` discovered in the compilation.

### Diagnostics (`ZFLUX###` prefix)

- **ZFLUX001 (Error)** — `[Reducer]` method's first parameter type isn't decorated with `[Feature]`.
- **ZFLUX002 (Error)** — Two `[Reducer]` methods within the same feature target the same action type.
- **ZFLUX003 (Error)** — `[Reducer]` method isn't `public static`, returns a different type from its first parameter, or has fewer than 2 parameters.
- **ZFLUX004 (Error)** — `[Feature(InitialState = "...")]` target factory method doesn't exist or has wrong signature (must be `public static TState Method(IServiceProvider)`).
- **ZFLUX005 (Error)** — `[Feature]` type isn't declared `partial`.

`Category = "ZeroAlloc.Flux"`. RS1032-compliant messageFormat (single sentence, no trailing period).

### Blazor adapter (`FluxComponent`)

```csharp
public abstract class FluxComponent : ComponentBase, IDisposable
{
    private readonly List<Action> _unsubscribers = new();

    /// <summary>
    /// v1 hook: override and call <see cref="Subscribe"/> for each injected
    /// <c>IStore&lt;T&gt;</c> property. v1.1 will introduce a generator-emitted
    /// partial method that auto-subscribes every <c>[Inject] IStore&lt;T&gt;</c> property.
    /// </summary>
    protected virtual void SubscribeStores() { }

    protected void Subscribe<TState>(IStore<TState> store, Action<TState>? onChange = null)
    {
        Action<TState> handler = state =>
        {
            onChange?.Invoke(state);
            InvokeAsync(StateHasChanged);
        };
        store.StateChanged += handler;
        _unsubscribers.Add(() => store.StateChanged -= handler);
    }

    protected override void OnInitialized() => SubscribeStores();

    void IDisposable.Dispose()
    {
        foreach (var unsub in _unsubscribers) unsub();
        _unsubscribers.Clear();
    }
}
```

v1.1 considers a generator-emitted partial `SubscribeStores` override that reads every `[Inject] IStore<TState>` property and wires it automatically.

### Allocation budget

- **`Dispatcher.DispatchAsync` happy path** (sync-completing reducer + sync change-notification handler): **0 B / call** under `AllocationGate.AssertBudgetValueTask`.
- **`Store<TState>.Value` getter:** **0 B / call**.
- ValueTask state-machine allocation only when a handler genuinely suspends.

## Tests

- **`ZeroAlloc.Flux.Generator.Tests`** — snapshot tests for each emit shape (one feature, multiple features, factory init, action without reducer); diagnostic tests for ZFLUX001–ZFLUX005 (positive + negative each).
- **`ZeroAlloc.Flux.Tests`** — runtime tests covering:
  - Fan-out across features (one action, multiple feature reducers).
  - Sync completion of `DispatchAsync` with sync handlers.
  - Async handler suspension and ValueTask state-machine allocation.
  - State update atomicity (struct lock + class CAS).
  - `StateChanged` event firing order (after state swap, exactly once per dispatch).
  - Allocation budgets for the happy paths.
- **`ZeroAlloc.Flux.Blazor.Tests`** — `bunit` tests on `FluxComponent` re-rendering after dispatch.
- **`ZeroAlloc.Flux.AotSmoke`** — `PublishAot=true` sample exercising one feature end-to-end; CI gate.

## Out of scope (deferred to v1.1+)

- **Effects via `Mediator.INotificationHandler<TAction>`.** The most complex piece — async, re-dispatch, cancellation. Defer until reducer/dispatch shape settles.
- **Failable reducers** returning `Result<TState, ReducerError>` via `ZeroAlloc.Results`.
- **Mutation-detection analyzer** — warn when a `[Reducer]` method mutates a property instead of returning a new value.
- **Generator-emitted `FluxComponent` auto-subscription** — read `[Inject] IStore<T>` properties and wire `StateChanged` automatically.
- **`ZeroAlloc.Notify` integration on the store side** — wrap stores in `[NotifyPropertyChangedAsync]` for fine-grained property change events.
- **Fluxor ecosystem compatibility** — DevTools, middleware, Redux DevTools wire protocol.
- **Sync `Dispatch` convenience overload.** If real Blazor friction surfaces around `async () => await Dispatcher.DispatchAsync(...)`, revisit.
- **Per-feature DI lifetime** via `[Feature(Lifetime = ...)]`.
- **Chained reducers** within a feature (multiple reducers for the same action).

## Backward compatibility

New package — no backwards-compatibility surface. v1.0.0 is the first public release.

SemVer: any future breaking change to the v1 contract requires a major bump; additive changes (new attributes, new DI extensions, Effects, failable reducers) are minor bumps.

## Files (initial scaffold)

The initial scaffold for the repo lands in a single PR alongside the design/plan docs. Standard ZeroAlloc family conventions:

- `Directory.Build.props` — `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<IsAotCompatible>true</IsAotCompatible>`, `<Nullable>enable</Nullable>`, common analyzer package refs.
- `GitVersion.yml`, `release-please-config.json`, `.release-please-manifest.json` — multi-package release config.
- `.github/workflows/ci.yml` — build + test + aot-smoke + api-compat + lint-commits.
- `.github/workflows/release-please.yml` — release-please + publish to NuGet on tag.
- `.github/workflows/publish-from-manifest.yml` — rescue workflow (manual trigger).
- `LICENSE` — MIT.
- `README.md` — quick-start + AOT/zero-alloc badges + benchmark placeholder.

These are mechanically derived from the closest sibling repo (`ZeroAlloc.Mediator`) and tracked in the implementation plan, not invented in this design doc.
