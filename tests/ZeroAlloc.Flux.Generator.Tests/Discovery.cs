using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Runs the generator on a source and returns the models its pipeline built, validated the way
/// the generator validates them. Tests of discovery and of the emitters use the same models the
/// generator emits from, so there is no second discovery path that can drift from the pipeline.
/// </summary>
internal static class Discovery
{
    public static (ImmutableArray<FeatureInfo> Features, ImmutableArray<ReducerInfo> Reducers, ImmutableArray<Diagnostic> Diagnostics)
        Run(string source)
    {
        var compilation = TestHarness.CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source) });
        var driver = CSharpGeneratorDriver.Create(
                new[] { new FluxGenerator().AsSourceGenerator() },
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true))
            .RunGenerators(compilation);
        var result = driver.GetRunResult().Results.Single();

        var model = FluxValidation.Validate(
            Outputs<FeatureInfo>(result, FluxGenerator.TrackingNames.Features),
            Outputs<ReducerCandidate>(result, FluxGenerator.TrackingNames.Reducers));

        return (
            model.Features,
            model.Reducers,
            model.Diagnostics.Select(d => d.ToDiagnostic()).ToImmutableArray());
    }

    private static ImmutableArray<T?> Outputs<T>(GeneratorRunResult result, string trackingName)
        where T : class
    {
        if (!result.TrackedSteps.TryGetValue(trackingName, out var steps))
        {
            return ImmutableArray<T?>.Empty;
        }

        return steps
            .SelectMany(step => step.Outputs)
            .Select(output => (T?)output.Value)
            .ToImmutableArray();
    }
}
