namespace ZeroAlloc.Flux.Benchmarks;

public static partial class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };

    [Reducer]
    public static CounterState On(CounterState state, ResetAction _) => new(0);
}
