using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

public sealed class InitialStateValidatorTests
{
    [Fact]
    public void NoDiagnostic_WhenInitialStateNotSet()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);
            """;

        var (_, diagnostics) = Discover(source);
        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    [Fact]
    public void NoDiagnostic_OnValidFactory()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = "Init")]
            public readonly partial record struct CounterState(int Count)
            {
                public static CounterState Init(IServiceProvider sp) => new(0);
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX004_WhenFactoryMissing()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = "DoesNotExist")]
            public readonly partial record struct CounterState(int Count);
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX004_OnSignatureMismatch_WrongReturnType()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = "Init")]
            public readonly partial record struct CounterState(int Count)
            {
                public static int Init(IServiceProvider sp) => 0;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX004_OnSignatureMismatch_WrongParam()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = "Init")]
            public readonly partial record struct CounterState(int Count)
            {
                public static CounterState Init(int x) => new(x);
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    [Fact]
    public void FiresZFLUX004_OnSignatureMismatch_NotStatic()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = "Init")]
            public partial record CounterState(int Count)
            {
                public CounterState Init(IServiceProvider sp) => this;
            }
            """;

        var (_, diagnostics) = Discover(source);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZFLUX004", StringComparison.Ordinal));
    }

    internal static (ImmutableArray<FeatureInfo>, ImmutableArray<Diagnostic>) Discover(string source)
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
