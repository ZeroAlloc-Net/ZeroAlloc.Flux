using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    [Fact]
    public void NoFeature_StandaloneProject_EmitsInternalExtensionsThatCompile()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            using Microsoft.Extensions.DependencyInjection;
            using ZeroAlloc.Flux.Generated;

            namespace App;

            public static class Root
            {
                public static IServiceCollection Compose(IServiceCollection s) => s.AddZeroAllocFlux();
            }
            """));

        var extensions = outcome.Result.GeneratedSources.Single(s => string.Equals(s.HintName, "FluxServiceCollectionExtensions.g.cs", System.StringComparison.Ordinal));
        Assert.Contains("internal static class FluxServiceCollectionExtensions", extensions.SourceText.ToString(), System.StringComparison.Ordinal);
        Assert.DoesNotContain("public static class", extensions.SourceText.ToString(), System.StringComparison.Ordinal);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    [Fact]
    public void NoFeature_HostReferencingFeaturesLibrary_BindsToTheLibrarysMethod()
    {
        var features = BuildLibrary("Features", """
            using ZeroAlloc.Flux;
            namespace Feat;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public readonly record struct Inc(int By);

            public static partial class Reducers
            {
                [Reducer]
                public static CounterState On(CounterState s, Inc a) => s with { Count = s.Count + a.By };
            }
            """);

        var host = Compile("Host", """
            using Microsoft.Extensions.DependencyInjection;
            using ZeroAlloc.Flux.Generated;

            namespace Host;

            public static class Root
            {
                public static IServiceCollection Compose(IServiceCollection s) => s.AddZeroAllocFlux();
            }
            """, features);

        // The host emits nothing: its own copy would shadow the library's, and the features'
        // stores would never be registered. Only a CS0436 warning would have said so.
        Assert.Empty(host.Result.GeneratedSources);
        AssertNoTypeConflicts(host);
        GeneratorAssert.NoCompilerErrors(host);
    }

    [Fact]
    public void NoFeature_TwoEmptyLibrariesAndAnApp_Compile()
    {
        var libA = BuildLibrary("LibA", "namespace LibA; public sealed class A;");
        var libB = BuildLibrary("LibB", "namespace LibB; public sealed class B;");

        var app = Compile("App", """
            using Microsoft.Extensions.DependencyInjection;
            using ZeroAlloc.Flux.Generated;

            namespace App;

            public static class Root
            {
                public static IServiceCollection Compose(IServiceCollection s) => s.AddZeroAllocFlux();
            }
            """, libA, libB);

        AssertNoTypeConflicts(app);
        GeneratorAssert.NoCompilerErrors(app);
    }

    private static GeneratorAssert.Outcome Compile(string assemblyName, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source, path: "/src/" + assemblyName + ".cs")],
                TestHarness.GetStandardReferences().Append(MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)).Concat(references),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new FluxGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new GeneratorAssert.Outcome(driver.GetRunResult().Results.Single(), diagnostics, output);
    }

    private static MetadataReference BuildLibrary(string assemblyName, string source, params MetadataReference[] references)
    {
        var outcome = Compile(assemblyName, source, references);
        GeneratorAssert.NoCompilerErrors(outcome);

        using var stream = new MemoryStream();
        var emitted = outcome.Output.Emit(stream);
        Assert.True(emitted.Success, string.Join(System.Environment.NewLine, emitted.Diagnostics.Select(d => d.ToString())));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static void AssertNoTypeConflicts(GeneratorAssert.Outcome outcome)
    {
        var conflicts = outcome.Output.GetDiagnostics()
            .Where(d => d.Id is "CS0436" or "CS0433" or "CS0121")
            .Select(d => d.ToString())
            .ToList();
        Assert.True(conflicts.Count == 0, string.Join(System.Environment.NewLine, conflicts));
    }
}
