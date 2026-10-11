namespace ZeroAlloc.Flux.Benchmarks;

public static partial class BadgeReducers
{
    [Reducer]
    public static BadgeState On(BadgeState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };
}
