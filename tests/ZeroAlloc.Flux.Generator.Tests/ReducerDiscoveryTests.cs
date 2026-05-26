using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

public sealed class ReducerDiscoveryTests
{
    [Fact]
    public void FindsReducerWithValidSignature()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public sealed record IncrementAction(int By);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState On(CounterState s, IncrementAction a) =>
                    s with { Count = s.Count + a.By };
            }
            """;

        var (reducers, diagnostics) = Discover(source);

        var reducer = Assert.Single(reducers);
        Assert.Equal("On", reducer.MethodName);
        Assert.Equal("global::Sample.CounterReducers", reducer.OwningTypeFqn);
        Assert.Equal("Sample.IncrementAction", reducer.ActionType.ToDisplayString());
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void FiresZFLUX003_OnNonStaticMethod()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public sealed record IncrementAction(int By);

            public class CounterReducers
            {
                [Reducer]
                public CounterState On(CounterState s, IncrementAction a) => s;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX003", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX003_OnReturnTypeMismatch()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public sealed record IncrementAction(int By);

            public static class CounterReducers
            {
                [Reducer]
                public static int On(CounterState s, IncrementAction a) => 0;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX003", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX001_OnNonFeatureStateType()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            public readonly partial record struct NotAFeature(int X);
            public sealed record SomeAction();

            public static class Reducers
            {
                [Reducer]
                public static NotAFeature On(NotAFeature s, SomeAction a) => s;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX001", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX002_OnDuplicateReducerInFeature()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public sealed record IncrementAction(int By);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState OnA(CounterState s, IncrementAction a) => s;
                [Reducer]
                public static CounterState OnB(CounterState s, IncrementAction a) => s;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX002", StringComparison.Ordinal));
    }

    internal static (ImmutableArray<ReducerInfo>, ImmutableArray<Diagnostic>) Discover(string source)
    {
        var references = TestHarness.GetStandardReferences();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var (features, _) = FeatureDiscovery.DiscoverFromCompilation(compilation);
        return ReducerDiscovery.DiscoverFromCompilation(compilation, features);
    }
}
