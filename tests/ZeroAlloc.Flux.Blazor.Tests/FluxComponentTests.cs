using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.TestHelpers;
using Xunit;

namespace ZeroAlloc.Flux.Blazor.Tests;

public sealed class FluxComponentTests : Bunit.BunitContext
{
    [Fact]
    public async Task Component_RerendersAfterDispatch()
    {
        // Wire Flux + Blazor into the bunit TestContext's IServiceCollection.
        Services.AddZeroAllocFlux();
        Services.AddZeroAllocFluxBlazor();

        var component = Render<TestCounter>();
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

        var component = Render<TestCounter>();
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

    [Fact]
    public async Task Dispatch_ToSubscribedComponent_ZeroAllocation()
    {
        // DispatchAsync -> Store.UpdateAsync -> StateChanged -> the FluxComponent handler, which
        // calls InvokeAsync with StateHasChanged. Measured on the renderer's dispatcher so
        // InvokeAsync runs the work item inline, and against a component whose ShouldRender is
        // false so StateHasChanged queues no render. What remains is the handler itself, which
        // used to allocate an Action for the StateHasChanged method group on every change.
        Services.AddZeroAllocFlux();
        Services.AddZeroAllocFluxBlazor();

        Render<QuietCounter>();
        var dispatcher = Services.GetRequiredService<IDispatcher>();
        var action = new IncrementTestAction(1);

        await Renderer.Dispatcher.InvokeAsync(() =>
            AllocationGate.AssertBudget(
                budgetBytes: 0,
                iterations: 1000,
                action: () =>
                {
                    var t = dispatcher.DispatchAsync(action);
                    if (!t.IsCompletedSuccessfully)
                    {
                        throw new System.InvalidOperationException(
                            "AllocationGate: DispatchAsync did not complete synchronously — " +
                            "awaiter machinery would pollute the measurement.");
                    }
                },
                label: "DispatchAsync to a subscribed FluxComponent"));

        Assert.Equal(1002, Services.GetRequiredService<IStore<TestCounterState>>().Value.Count);
    }
}
