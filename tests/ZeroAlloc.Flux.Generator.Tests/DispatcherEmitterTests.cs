using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Snapshot tests for <see cref="DispatcherEmitter"/>. Each fixture exercises a different
/// fan-out shape: single feature (sync fast path), multiple features sharing an action
/// (async fan-out), and multiple distinct actions (multiple overloads).
/// </summary>
public sealed class DispatcherEmitterTests
{
    [Fact]
    public Task SingleFeature_OneAction_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public readonly record struct IncrementAction(int By);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState On(CounterState s, IncrementAction a) => s with { Count = s.Count + a.By };
            }
            """;

        return VerifyEmit(source);
    }

    [Fact]
    public Task MultipleFeatures_FanOut_SameAction_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            [Feature]
            public readonly partial record struct AuditState(int Hits);

            public readonly record struct PingAction(string Tag);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState On(CounterState s, PingAction a) => s with { Count = s.Count + 1 };
            }

            public static class AuditReducers
            {
                [Reducer]
                public static AuditState On(AuditState s, PingAction a) => s with { Hits = s.Hits + 1 };
            }
            """;

        return VerifyEmit(source);
    }

    [Fact]
    public Task MultipleActions_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public readonly record struct IncrementAction(int By);
            public readonly record struct DecrementAction(int By);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState OnInc(CounterState s, IncrementAction a) => s with { Count = s.Count + a.By };

                [Reducer]
                public static CounterState OnDec(CounterState s, DecrementAction a) => s with { Count = s.Count - a.By };
            }
            """;

        return VerifyEmit(source);
    }

    private static Task VerifyEmit(string source)
    {
        var references = TestHarness.GetStandardReferences();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var (features, _) = FeatureDiscovery.DiscoverFromCompilation(compilation);
        var (reducers, _) = ReducerDiscovery.DiscoverFromCompilation(compilation, features);
        var emitted = DispatcherEmitter.Emit(features, reducers);
        return Verifier.Verify(emitted, extension: "txt").UseDirectory("Snapshots");
    }
}
