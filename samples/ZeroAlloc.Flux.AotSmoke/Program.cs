using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.Flux.AotSmoke;

var services = new ServiceCollection();
services.AddZeroAllocFlux();
using var sp = services.BuildServiceProvider();

var dispatcher = sp.GetRequiredService<IDispatcher>();
var counter = sp.GetRequiredService<IStore<CounterState>>();
var totals = sp.GetRequiredService<IStore<TotalsState>>();

var counterChanges = 0;
var totalsChanges = 0;
counter.StateChanged += state =>
{
    counterChanges++;
    Console.WriteLine($"Counter changed to {state.Count}");
};
totals.StateChanged += state =>
{
    totalsChanges++;
    Console.WriteLine($"Totals changed to Sum={state.Sum} Hits={state.Hits}");
};

// Struct action handled by two features: the struct CounterState and the record-class
// TotalsState. Runs the generated fan-out overload and the class-feature CAS update path.
await dispatcher.DispatchAsync(new IncrementAction(5)).ConfigureAwait(false);
if (counter.Value.Count != 5)
    return Fail($"After IncrementAction 5, Count expected 5, got {counter.Value.Count}");
if (totals.Value.Sum != 5 || totals.Value.Hits != 1)
    return Fail($"After IncrementAction 5, Totals expected 5/1, got {totals.Value.Sum}/{totals.Value.Hits}");

await dispatcher.DispatchAsync(new IncrementAction(3)).ConfigureAwait(false);
if (counter.Value.Count != 8)
    return Fail($"After IncrementAction 3, Count expected 8, got {counter.Value.Count}");
if (totals.Value.Sum != 8 || totals.Value.Hits != 2)
    return Fail($"After IncrementAction 3, Totals expected 8/2, got {totals.Value.Sum}/{totals.Value.Hits}");

// Empty struct action handled by one feature only; TotalsState must not change.
await dispatcher.DispatchAsync(new ResetAction()).ConfigureAwait(false);
if (counter.Value.Count != 0)
    return Fail($"After ResetAction, Count expected 0, got {counter.Value.Count}");
if (totals.Value.Sum != 8 || totals.Value.Hits != 2)
    return Fail($"After ResetAction, Totals expected unchanged 8/2, got {totals.Value.Sum}/{totals.Value.Hits}");

await dispatcher.DispatchAsync(new IncrementAction(2)).ConfigureAwait(false);
if (counter.Value.Count != 2)
    return Fail($"After IncrementAction 2, Count expected 2, got {counter.Value.Count}");
Console.WriteLine("Struct actions: OK");

// Reference-type action: TAction is a record class.
await dispatcher.DispatchAsync(new ScaleAction(10)).ConfigureAwait(false);
if (counter.Value.Count != 20)
    return Fail($"After ScaleAction 10, Count expected 20, got {counter.Value.Count}");
Console.WriteLine("Record-class action: OK");

// Nullable primitive action through the generic IDispatcher entry point, with and without a value.
await dispatcher.DispatchAsync<int?>(7).ConfigureAwait(false);
if (counter.Value.Count != 27)
    return Fail($"After int? 7, Count expected 27, got {counter.Value.Count}");
await dispatcher.DispatchAsync<int?>(null).ConfigureAwait(false);
if (counter.Value.Count != 127)
    return Fail($"After int? null, Count expected 127, got {counter.Value.Count}");
Console.WriteLine("Nullable int action: OK");

// Enum action.
await dispatcher.DispatchAsync(Step.Down).ConfigureAwait(false);
if (counter.Value.Count != 126)
    return Fail($"After Step.Down, Count expected 126, got {counter.Value.Count}");
await dispatcher.DispatchAsync(Step.Up).ConfigureAwait(false);
if (counter.Value.Count != 127)
    return Fail($"After Step.Up, Count expected 127, got {counter.Value.Count}");
Console.WriteLine("Enum action: OK");

// Action type with no reducer: the generic fallback is a no-op and must leave every state alone.
await dispatcher.DispatchAsync(42L).ConfigureAwait(false);
if (counter.Value.Count != 127)
    return Fail($"After unhandled long action, Count expected unchanged 127, got {counter.Value.Count}");
if (totals.Value.Sum != 10 || totals.Value.Hits != 3)
    return Fail($"After unhandled long action, Totals expected 10/3, got {totals.Value.Sum}/{totals.Value.Hits}");
Console.WriteLine("Unhandled action: OK");

// Every handled dispatch fires exactly one StateChanged per matching feature.
if (counterChanges != 9)
    return Fail($"Counter StateChanged expected 9 times, got {counterChanges}");
if (totalsChanges != 3)
    return Fail($"Totals StateChanged expected 3 times, got {totalsChanges}");

Console.WriteLine($"Final: {counter.Value.Count}");
Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}

namespace ZeroAlloc.Flux.AotSmoke
{
    [Feature]
    public readonly partial record struct CounterState(int Count);

    [Feature]
    public sealed partial record class TotalsState(int Sum, int Hits)
    {
        public TotalsState() : this(0, 0) { }
    }

    public readonly record struct IncrementAction(int Amount);
    public readonly record struct ResetAction;
    public sealed record class ScaleAction(int Factor);

    public enum Step
    {
        Down = -1,
        Up = 1,
    }

    public static partial class CounterReducers
    {
        [Reducer]
        public static CounterState On(CounterState state, IncrementAction action)
            => state with { Count = state.Count + action.Amount };

        [Reducer]
        public static CounterState On(CounterState state, ResetAction _) => new(0);

        [Reducer]
        public static CounterState On(CounterState state, ScaleAction action)
            => state with { Count = state.Count * action.Factor };

        [Reducer]
        public static CounterState On(CounterState state, int? amount)
            => state with { Count = state.Count + (amount ?? 100) };

        [Reducer]
        public static CounterState On(CounterState state, Step step)
            => state with { Count = state.Count + (int)step };
    }

    public static partial class TotalsReducers
    {
        [Reducer]
        public static TotalsState On(TotalsState state, IncrementAction action)
            => state with { Sum = state.Sum + action.Amount, Hits = state.Hits + 1 };
    }
}
