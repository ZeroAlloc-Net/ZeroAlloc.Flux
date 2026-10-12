using ZeroAlloc.Flux;

namespace ZeroAlloc.Flux.WasmSmoke;

/// <summary>The smoke feature: proves a feature, a reducer and a FluxComponent compile for browser-wasm.</summary>
[Feature]
public readonly partial record struct CounterState(int Count);

/// <summary>Adds one.</summary>
public readonly record struct Increment;

/// <summary>The smoke reducer.</summary>
public static class CounterReducers
{
    /// <summary>Adds one to the count.</summary>
    [Reducer]
    public static CounterState On(CounterState state, Increment _) => state with { Count = state.Count + 1 };
}