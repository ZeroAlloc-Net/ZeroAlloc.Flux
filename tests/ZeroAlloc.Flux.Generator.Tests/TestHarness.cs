using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Shared harness for snapshot + diagnostic tests against <see cref="FluxGenerator"/>.
/// Ported from <c>ZeroAlloc.Authorization.Generator.Tests</c>.
/// </summary>
internal static class TestHarness
{
    /// <summary>
    /// Compiles <paramref name="source"/> into a transient <c>TestAssembly</c>, runs
    /// <see cref="FluxGenerator"/> over it, and returns only the generator-emitted
    /// diagnostics. Use this from diagnostic-focused tests (ZFLUX001-ZFLUX005).
    /// </summary>
    public static ImmutableArray<Diagnostic> RunDiagnostics(string source)
    {
        var driver = Run(source, out _);
        return driver.GetRunResult().Diagnostics;
    }

    /// <summary>
    /// Compiles <paramref name="source"/>, runs the generator, and snapshots the resulting
    /// driver with VerifyXunit (per-test <c>.verified.cs</c> files under <c>Snapshots/</c>).
    /// </summary>
    public static Task Verify(string source)
    {
        var driver = Run(source, out _);
        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    /// <summary>
    /// Low-level access: returns the <see cref="GeneratorDriver"/> after running and the
    /// underlying <see cref="CSharpCompilation"/>. Callers can inspect compilation diagnostics
    /// (e.g. binding errors in the fixture source) separately from generator diagnostics.
    /// </summary>
    public static GeneratorDriver Run(string source, out CSharpCompilation compilation)
    {
        var references = GetStandardReferences();
        compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new FluxGenerator().AsSourceGenerator();
        return CSharpGeneratorDriver.Create(generator).RunGenerators(compilation);
    }

    /// <summary>
    /// Builds the standard reference set: every loaded assembly in the current AppDomain
    /// with a non-empty <c>Location</c>. Force-loads <c>ZeroAlloc.Flux</c> first so the
    /// <c>[Feature]</c> / <c>[Reducer]</c> attribute symbols are bindable from fixture sources.
    /// </summary>
    public static List<MetadataReference> GetStandardReferences()
    {
        // Touch a known type from the Flux runtime assembly to force CLR load.
        _ = typeof(ZeroAlloc.Flux.FeatureAttribute).FullName;

        var references = new List<MetadataReference>();
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            if (string.IsNullOrEmpty(asm.Location)) continue;
            references.Add(MetadataReference.CreateFromFile(asm.Location));
        }
        return references;
    }
}
