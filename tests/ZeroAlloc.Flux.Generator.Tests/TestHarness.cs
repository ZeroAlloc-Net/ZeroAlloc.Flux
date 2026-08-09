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
    /// Builds the standard reference set: the framework reference assemblies for this target,
    /// plus the <c>ZeroAlloc.Flux</c> runtime so the <c>[Feature]</c> / <c>[Reducer]</c>
    /// attribute symbols are bindable from fixture sources.
    /// </summary>
    /// <remarks>
    /// This deliberately does NOT enumerate <c>AppDomain.CurrentDomain.GetAssemblies()</c>. Doing
    /// so made the fixture compilations depend on whatever the test host happened to have loaded,
    /// which is not a property of the code under test: moving from Microsoft.NET.Test.Sdk 17 to 18
    /// changed that set and made <c>ZFLUX004</c> fire on a factory that is valid, failing
    /// <c>NoDiagnostic_OnValidFactory</c> and <c>CapturesInitialStateNamedArgument</c> without a
    /// line of generator or fixture code changing.
    /// <para>
    /// <c>Basic.Reference.Assemblies</c> ships the reference assemblies in the package, so the
    /// reference set is now fixed by the target framework rather than discovered at run time —
    /// the same approach the other generator suites in this org already use.
    /// </para>
    /// </remarks>
    public static List<MetadataReference> GetStandardReferences()
    {
#if NET10_0_OR_GREATER
        var references = new List<MetadataReference>(Basic.Reference.Assemblies.Net100.References.All);
#else
        var references = new List<MetadataReference>(Basic.Reference.Assemblies.Net80.References.All);
#endif

        // The fixtures bind [Feature] / [Reducer], so the runtime assembly has to be present.
        references.Add(
            MetadataReference.CreateFromFile(typeof(ZeroAlloc.Flux.FeatureAttribute).Assembly.Location));

        return references;
    }
}
