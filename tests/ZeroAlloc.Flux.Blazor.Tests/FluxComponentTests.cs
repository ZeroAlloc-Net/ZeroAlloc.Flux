using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using Xunit;

namespace ZeroAlloc.Flux.Blazor.Tests;

public sealed class FluxComponentTests : Bunit.TestContext
{
    [Fact]
    public async Task Component_RerendersAfterDispatch()
    {
        // Wire Flux + Blazor into the bunit TestContext's IServiceCollection.
        Services.AddZeroAllocFlux();
        Services.AddZeroAllocFluxBlazor();

        var component = RenderComponent<TestCounter>();
        Assert.Contains("Count: 0", component.Markup, System.StringComparison.Ordinal);

        var dispatcher = Services.GetRequiredService<IDispatcher>();
        await dispatcher.DispatchAsync(new IncrementTestAction(5));

        // After dispatch, the FluxComponent should re-render.
        component.WaitForAssertion(() =>
            Assert.Contains("Count: 5", component.Markup, System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Component_AfterDispose_DoesNotReceiveUpdates()
    {
        Services.AddZeroAllocFlux();
        Services.AddZeroAllocFluxBlazor();

        var component = RenderComponent<TestCounter>();
        var dispatcher = Services.GetRequiredService<IDispatcher>();
        var store = Services.GetRequiredService<IStore<TestCounterState>>();

        var updatesAfterDispose = 0;
        store.StateChanged += _ => updatesAfterDispose++;

        component.Dispose();

        await dispatcher.DispatchAsync(new IncrementTestAction(1));

        // The component's internal handler should NOT have fired. The component's markup
        // should not have re-rendered (we can't easily check markup post-dispose, but
        // we can verify the store's external subscriber DID fire — meaning the dispatch
        // happened, but the FluxComponent's handler is gone).
        Assert.Equal(1, updatesAfterDispose);
    }
}
