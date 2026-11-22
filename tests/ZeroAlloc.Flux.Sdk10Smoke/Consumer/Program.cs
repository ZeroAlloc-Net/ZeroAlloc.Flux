using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.Flux.Sdk10Smoke;

// AddZeroAllocFlux and the IStore registrations exist only if the compiler loaded the generator.
var services = new ServiceCollection();
services.AddZeroAllocFlux();
using var sp = services.BuildServiceProvider();

var dispatcher = sp.GetRequiredService<IDispatcher>();
var counter = sp.GetRequiredService<IStore<CounterState>>();

await dispatcher.DispatchAsync(new IncrementAction(5));
if (counter.Value.Count != 5)
{
    Console.Error.WriteLine($"FAIL: Count expected 5, got {counter.Value.Count}");
    return 1;
}

Console.WriteLine("SDK 10 consumer: generator loaded, action reduced through the generated store.");
return 0;

namespace ZeroAlloc.Flux.Sdk10Smoke
{
    [Feature]
    public readonly partial record struct CounterState(int Count);

    public readonly record struct IncrementAction(int Amount);

    public static partial class CounterReducers
    {
        [Reducer]
        public static CounterState On(CounterState state, IncrementAction action)
            => new(state.Count + action.Amount);
    }
}
