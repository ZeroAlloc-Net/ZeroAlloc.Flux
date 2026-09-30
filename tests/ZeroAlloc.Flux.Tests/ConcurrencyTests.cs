using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using Xunit;

namespace ZeroAlloc.Flux.Tests;

// Concurrent dispatches to the same store must not lose updates: each reducer has to run on the
// state it replaces, atomically with the write. See ZeroAlloc.Flux#131.
public sealed class ConcurrencyTests
{
    private const int Threads = 8;
    private const int DispatchesPerThread = 20_000;
    private const int Total = Threads * DispatchesPerThread;

    [Fact]
    public async Task ConcurrentDispatch_StructFeatures_LosesNoUpdates()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var counter = sp.GetRequiredService<IStore<CounterState>>();
        var badge = sp.GetRequiredService<IStore<BadgeCountState>>();

        // IncrementAction fans out to two struct features, so this covers the fan-out path.
        await RunConcurrently(() => dispatcher.DispatchAsync(new IncrementAction(1)));

        Assert.Equal(Total, counter.Value.Count);
        Assert.Equal(Total, badge.Value.Count);
    }

    [Fact]
    public async Task ConcurrentDispatch_RecordClassFeature_LosesNoUpdates()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var tally = sp.GetRequiredService<IStore<TallyState>>();

        // TallyAction has a single record-class feature, so this covers the single-feature path.
        await RunConcurrently(() => dispatcher.DispatchAsync(new TallyAction()));

        Assert.Equal(Total, tally.Value.Count);
    }

    [Fact]
    public async Task ConcurrentDispatch_StateChangedValues_AreDistinctAndStored()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var tally = sp.GetRequiredService<IStore<TallyState>>();

        // Every update publishes the state it stored, so each count 1..Total is published once.
        var seen = new int[Total + 1];
        tally.StateChanged += state => Interlocked.Increment(ref seen[state.Count]);

        await RunConcurrently(() => dispatcher.DispatchAsync(new TallyAction()));

        Assert.Equal(0, seen[0]);
        for (var i = 1; i <= Total; i++)
        {
            Assert.True(seen[i] == 1, $"count {i} was published {seen[i]} times");
        }
    }

    private static async Task RunConcurrently(Func<ValueTask> dispatch)
    {
        using var start = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    for (var i = 0; i < DispatchesPerThread; i++)
                    {
                        dispatch().GetAwaiter().GetResult();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
    }
}
