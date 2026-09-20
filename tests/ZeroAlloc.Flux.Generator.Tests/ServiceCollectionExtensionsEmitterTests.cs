using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Snapshot tests for <see cref="ServiceCollectionExtensionsEmitter"/>. Validates the
/// <c>AddZeroAllocFlux</c> shape for both default-init and factory-init features.
/// </summary>
public sealed class ServiceCollectionExtensionsEmitterTests
{
    [Fact]
    public void OneFeature_DefaultInit_SnapshotMatches()
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
    public void MultipleFeatures_MixedInit_SnapshotMatches()
    {
        const string source = """
            using System;
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var (features, _) = FeatureDiscovery.DiscoverFromCompilation(compilation);
        var emitted = ServiceCollectionExtensionsEmitter.Emit(features);
        GeneratorSnapshot.VerifyText(emitted, "txt");
    }
}
