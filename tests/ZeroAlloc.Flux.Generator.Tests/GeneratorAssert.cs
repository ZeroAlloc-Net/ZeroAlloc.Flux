using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Runs the generator over consumer code and checks that the result compiles.
/// </summary>
internal static class GeneratorAssert
{
    /// <summary>The generator's run, and the compilation with the generated sources added.</summary>
    internal sealed record Outcome(
        GeneratorRunResult Result,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        Compilation Output)
    {
        /// <summary>The hint names of the generated sources, in ordinal order.</summary>
        public string[] HintNames =>
            Result.GeneratedSources.Select(s => s.HintName).OrderBy(h => h, StringComparer.Ordinal).ToArray();

        /// <summary>The generator's diagnostics with <paramref name="id"/>.</summary>
        public Diagnostic[] WithId(string id) =>
            GeneratorDiagnostics.Where(d => string.Equals(d.Id, id, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>
    /// Runs the generator over <paramref name="source"/>, then asserts that it reported nothing and
    /// that the consumer code compiles together with the generated sources.
    /// </summary>
    public static void Compiles(string source)
    {
        var outcome = Run(("/src/App.cs", source));
        Assert.Empty(outcome.GeneratorDiagnostics);
        NoCompilerErrors(outcome);
    }

    /// <summary>
    /// Runs the generator over one source tree per file, in the order given, with the
    /// dependency-injection assembly referenced so the generated registrations bind.
    /// </summary>
    public static Outcome Run(params (string Path, string Source)[] files)
    {
        var compilation = TestHarness.CreateCompilation(
                files.Select(f => CSharpSyntaxTree.ParseText(f.Source, path: f.Path)))
            .AddReferences(MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

        var driver = CSharpGeneratorDriver.Create(new FluxGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        return new Outcome(driver.GetRunResult().Results.Single(), generatorDiagnostics, output);
    }

    /// <summary>
    /// Asserts that the compiler reports no error of its own. The generator's diagnostics are
    /// not part of the compilation, so a test that expects one can still use this.
    /// </summary>
    public static void NoCompilerErrors(Outcome outcome)
    {
        var errors = outcome.Output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
