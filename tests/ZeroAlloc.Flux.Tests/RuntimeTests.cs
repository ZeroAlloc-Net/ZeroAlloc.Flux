using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using Xunit;

namespace ZeroAlloc.Flux.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public async Task DispatchAsync_UpdatesStore()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var counter = sp.GetRequiredService<IStore<CounterState>>();

        await dispatcher.DispatchAsync(new IncrementAction(5));

        Assert.Equal(5, counter.Value.Count);
    }

    [Fact]
    public async Task DispatchAsync_FansOut_AcrossMatchingFeatures()
    {
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var counter = sp.GetRequiredService<IStore<CounterState>>();
        var badge = sp.GetRequiredService<IStore<BadgeCountState>>();

        await dispatcher.DispatchAsync(new IncrementAction(3));

        Assert.Equal(3, counter.Value.Count);
        Assert.Equal(3, badge.Value.Count);
    }
}
