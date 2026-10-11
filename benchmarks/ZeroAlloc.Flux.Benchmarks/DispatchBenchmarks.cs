using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux.Generated;

namespace ZeroAlloc.Flux.Benchmarks;

/// <summary>
/// Measures the generated dispatch hot path: <see cref="IDispatcher.DispatchAsync{TAction}"/>
/// into the emitted <c>Store_*.UpdateAsync</c>, which runs the reducer and raises
/// <see cref="IStore{TState}.StateChanged"/>. The dispatcher and stores are resolved from the
/// container that <c>AddZeroAllocFlux</c> builds, the way a consumer calls them. Every benchmark
/// here targets 0 B allocated; the allocation budgets in ZeroAlloc.Flux.Tests enforce that.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class DispatchBenchmarks
{
    // Two containers, so the stores without subscribers stay without them: each container
    // holds its own singleton dispatcher and stores.
    private ServiceProvider _plain = null!;
    private ServiceProvider _subscribed = null!;
    private IDispatcher _plainDispatcher = null!;
    private IDispatcher _subscribedDispatcher = null!;
    private IStore<CounterState> _counter = null!;

    /// <summary>Incremented by the StateChanged subscribers, so their work is not dead code.</summary>
    public int Notifications { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        _plain = Build();
        _plainDispatcher = _plain.GetRequiredService<IDispatcher>();
        _counter = _plain.GetRequiredService<IStore<CounterState>>();

        _subscribed = Build();
        _subscribedDispatcher = _subscribed.GetRequiredService<IDispatcher>();
        _subscribed.GetRequiredService<IStore<CounterState>>().StateChanged += _ => Notifications++;
        _subscribed.GetRequiredService<IStore<BadgeState>>().StateChanged += _ => Notifications++;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _subscribed.Dispose();
    }

    /// <summary>One feature handles the action and nobody listens to StateChanged.</summary>
    [Benchmark(Description = "DispatchAsync – one feature, no subscribers")]
    public ValueTask Dispatch_SingleFeature_NoSubscribers()
        => _plainDispatcher.DispatchAsync(default(ResetAction));

    /// <summary>
    /// The action fans out to two features, and each store raises StateChanged to a subscriber.
    /// </summary>
    [Benchmark(Description = "DispatchAsync – two features, StateChanged subscribers")]
    public ValueTask Dispatch_FanOut_WithSubscribers()
        => _subscribedDispatcher.DispatchAsync(new IncrementAction(1));

    /// <summary>The lock-free read of the current state.</summary>
    [Benchmark(Description = "IStore.Value – read")]
    public CounterState StoreValue_Read() => _counter.Value;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        return services.BuildServiceProvider();
    }
}
