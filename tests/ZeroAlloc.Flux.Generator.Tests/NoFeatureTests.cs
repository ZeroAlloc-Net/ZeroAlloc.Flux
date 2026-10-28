using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// A project with no <c>[Feature]</c> still gets <c>AddZeroAllocFlux</c> and a no-op dispatcher,
/// so a composition root written before the first feature compiles.
/// </summary>
public sealed class NoFeatureTests
{
    [Fact]
    public void NoFeature_EmitsDispatcherAndServiceCollectionExtensions()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            namespace App;

            public sealed class Marker;
            """));

        Assert.Empty(outcome.GeneratorDiagnostics);
        Assert.Equal(["FluxDispatcher.g.cs", "FluxServiceCollectionExtensions.g.cs"], outcome.HintNames);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    [Fact]
    public void NoFeature_CompositionRootCallingAddZeroAllocFlux_Compiles()
    {
        GeneratorAssert.Compiles("""
            using Microsoft.Extensions.DependencyInjection;
            using ZeroAlloc.Flux.Generated;

            namespace App;

            public static class Root
            {
                public static IServiceCollection Compose(IServiceCollection s) => s.AddZeroAllocFlux();
            }
            """);
    }
}
