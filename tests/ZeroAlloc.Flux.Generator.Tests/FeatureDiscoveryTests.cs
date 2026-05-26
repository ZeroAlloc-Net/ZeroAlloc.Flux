using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

public sealed class FeatureDiscoveryTests
{
    [Fact]
    public void FindsFeatureOnRecordStruct()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);
            """;

        var (features, diagnostics) = DiscoverFromCompilation(source);

        var feature = Assert.Single(features);
        Assert.Equal("global::Sample.CounterState", feature.FullyQualifiedName);
        Assert.True(feature.IsStruct);
        Assert.True(feature.IsPartial);
        Assert.Null(feature.InitialStateFactoryName);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void FindsFeatureOnRecordClass()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public sealed partial record CounterState(int Count);
            """;

        var (features, diagnostics) = DiscoverFromCompilation(source);

        var feature = Assert.Single(features);
        Assert.Equal("global::Sample.CounterState", feature.FullyQualifiedName);
        Assert.False(feature.IsStruct);
        Assert.True(feature.IsPartial);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void FiresZFLUX005OnNonPartialFeature()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public sealed record CounterState(int Count);
            """;

        var (features, diagnostics) = DiscoverFromCompilation(source);

        var feature = Assert.Single(features);
        Assert.False(feature.IsPartial);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("ZFLUX005", diag.Id);
    }

    [Fact]
    public void CapturesInitialStateNamedArgument()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = nameof(Init))]
            public partial record struct CounterState(int Count)
            {
                public static CounterState Init(IServiceProvider sp) => new(0);
            }
            """;

        var (features, diagnostics) = DiscoverFromCompilation(source);

        var feature = Assert.Single(features);
        Assert.Equal("Init", feature.InitialStateFactoryName);
        Assert.Empty(diagnostics);
    }

    internal static (System.Collections.Immutable.ImmutableArray<FeatureInfo>, System.Collections.Immutable.ImmutableArray<Diagnostic>)
        DiscoverFromCompilation(string source)
    {
        var references = TestHarness.GetStandardReferences();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return FeatureDiscovery.DiscoverFromCompilation(compilation);
    }
}
