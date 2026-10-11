namespace ZeroAlloc.Flux.Benchmarks;

/// <summary>Handled by both <see cref="CounterState"/> and <see cref="BadgeState"/>.</summary>
public readonly record struct IncrementAction(int Amount);
