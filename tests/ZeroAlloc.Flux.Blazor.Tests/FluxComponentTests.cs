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
}
