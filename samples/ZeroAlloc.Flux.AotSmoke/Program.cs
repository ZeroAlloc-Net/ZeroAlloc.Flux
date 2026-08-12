using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.Flux.AotSmoke;

var services = new ServiceCollection();
services.AddZeroAllocFlux();
using var sp = services.BuildServiceProvider();

var dispatcher = sp.GetRequiredService<IDispatcher>();
var counter = sp.GetRequiredService<IStore<CounterState>>();

counter.StateChanged += state => Console.WriteLine($"Counter changed to {state.Count}");

await dispatcher.DispatchAsync(new IncrementAction(5)).ConfigureAwait(false);
await dispatcher.DispatchAsync(new IncrementAction(3)).ConfigureAwait(false);
await dispatcher.DispatchAsync(new ResetAction()).ConfigureAwait(false);

Console.WriteLine($"Final: {counter.Value.Count}");

namespace ZeroAlloc.Flux.AotSmoke
{
    [Feature]
    public readonly partial record struct CounterState(int Count);

    public readonly record struct IncrementAction(int Amount);
    public readonly record struct ResetAction;

    public static partial class CounterReducers
    {
        [Reducer]
        public static CounterState On(CounterState state, IncrementAction action)
            => state with { Count = state.Count + action.Amount };

        [Reducer]
        public static CounterState On(CounterState state, ResetAction _) => new(0);
    }
}
