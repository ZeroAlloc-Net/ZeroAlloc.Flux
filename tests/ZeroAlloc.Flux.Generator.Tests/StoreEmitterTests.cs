using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Snapshot tests for <see cref="StoreEmitter"/>. Each test compiles a fixture source,
/// runs <see cref="FeatureDiscovery.DiscoverFromCompilation"/>, then verifies the emitted
/// store source against a <c>.verified.txt</c> snapshot under <c>Snapshots/</c>.
/// </summary>
public sealed class StoreEmitterTests
{
    [Fact]
    public void StructFeature_DefaultInit_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);
            """;

        VerifyEmit(source);
    }

    [Fact]
    public void ClassFeature_DefaultInit_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public sealed partial record CounterState(int Count);
            """;

        VerifyEmit(source);
    }

    [Fact]
    public void StructFeature_InitialStateFactory_SnapshotMatches()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature(InitialState = nameof(Init))]
            public partial record struct ConfiguredState(int Count)
            {
                public static ConfiguredState Init(IServiceProvider sp) => new(0);
            }
            """;

        VerifyEmit(source);
    }

    private static void VerifyEmit(string source)
    {
        var references = TestHarness.GetStandardReferences();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        var (features, _) = FeatureDiscovery.DiscoverFromCompilation(compilation);
        var feature = features[0];
        var emitted = StoreEmitter.Emit(feature);
        GeneratorSnapshot.VerifyText(emitted, "txt");
    }
}
