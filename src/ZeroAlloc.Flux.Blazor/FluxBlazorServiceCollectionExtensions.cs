using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Flux.Blazor;

/// <summary>
/// DI extensions for <c>ZeroAlloc.Flux.Blazor</c>. v1.0 is a stub — Blazor-specific
/// service registration lands in v1.1 alongside the generator-emitted auto-subscription
/// hook for <see cref="FluxComponent"/>. Today it's a no-op that exists so consumer code
/// can call <c>services.AddZeroAllocFluxBlazor()</c> as a future-proof API.
/// </summary>
public static class FluxBlazorServiceCollectionExtensions
{
    /// <summary>
    /// Registers Blazor-specific Flux services. v1.0: no-op stub. Reserve for v1.1
    /// auto-subscription registrations.
    /// </summary>
    public static IServiceCollection AddZeroAllocFluxBlazor(this IServiceCollection services)
    {
        // v1.0 no-op. Reserve for v1.1 auto-subscription registrations.
        return services;
    }
}
