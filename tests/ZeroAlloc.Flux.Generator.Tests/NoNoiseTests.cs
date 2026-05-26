using System.Linq;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Negative-control test asserting a valid <c>[Feature]</c> + <c>[Reducer]</c> fixture
/// produces zero <c>ZFLUX*</c> diagnostics from the generator. Equivalent to
/// <c>ZeroAlloc.Authorization</c>'s no-noise test for the ZAUTH001-ZAUTH005 set.
/// </summary>
public sealed class NoNoiseTests
{
    [Fact]
    public void CleanSource_EmitsNoZFLUXDiagnostics()
    {
        var source = """
            using ZeroAlloc.Flux;

            namespace MyApp;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public readonly record struct IncrementAction(int Amount);

            public static partial class CounterReducers
            {
                [Reducer]
                public static CounterState On(CounterState state, IncrementAction action)
                    => state with { Count = state.Count + action.Amount };
            }
            """;

        var diags = TestHarness.RunDiagnostics(source);

        var zfluxDiags = diags.Where(d => d.Id.StartsWith("ZFLUX")).ToList();
        Assert.Empty(zfluxDiags);
    }
}
