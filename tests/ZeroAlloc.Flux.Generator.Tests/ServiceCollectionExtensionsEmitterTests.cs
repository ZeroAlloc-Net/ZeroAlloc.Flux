using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Snapshot tests for <see cref="ServiceCollectionExtensionsEmitter"/>. Validates the
/// <c>AddZeroAllocFlux</c> shape for both default-init and factory-init features.
/// </summary>
public sealed class ServiceCollectionExtensionsEmitterTests
{
    [Fact]
    public Task OneFeature_DefaultInit_SnapshotMatches()
    {
        const string source = """
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);
            """;

        return VerifyEmit(source);
    }

    [Fact]
    public Task MultipleFeatures_MixedInit_SnapshotMatches()
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
        var emitted = ServiceCollectionExtensionsEmitter.Emit(features);
        return Verifier.Verify(emitted, extension: "txt").UseDirectory("Snapshots");
    }
}
