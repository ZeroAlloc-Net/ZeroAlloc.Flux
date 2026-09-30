using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Roslyn compares hint names ignoring case, so two features whose qualified names differ only in
/// case need the same store file. The feature declared first, by file path and then position, is
/// generated; each later one gets ZFLUX008 and nothing is generated for it, as ZeroAlloc.Mapping
/// does with ZAMP024. See #142.
/// </summary>
public sealed class CaseCollisionTests
{
    [Fact]
    public void FeaturesThatDifferOnlyInCase_ReportZFLUX008_OnTheLaterOne()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            using ZeroAlloc.Flux;
            namespace App;

            public readonly record struct PingAction;

            [Feature]
            public readonly partial record struct State(int Count);

            [Feature]
            public readonly partial record struct state(int Count);

            [Feature]
            public readonly partial record struct OtherState(int Count);

            public static class Reducers
            {
                [Reducer]
                public static State On(State s, PingAction a) => s;

                [Reducer]
                public static state On(state s, PingAction a) => s;
            }
            """));

        Assert.Null(outcome.Result.Exception);
        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Feature 'global::App.state' has the same qualified name as 'global::App.State' apart from case, so no store is generated for it; rename one of them",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("state", Text(diagnostic.Location));
        Assert.Equal("State", Text(Assert.Single(diagnostic.AdditionalLocations)));

        Assert.Equal(
            new[]
            {
                "App.OtherState.Store.g.cs",
                "App.State.Store.g.cs",
                "FluxDispatcher.g.cs",
                "FluxServiceCollectionExtensions.g.cs",
            },
            outcome.HintNames);
        var generated = string.Join("\n", outcome.Result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.DoesNotContain("App.state", generated, StringComparison.Ordinal);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    [Fact]
    public void ThreeFeaturesThatDifferOnlyInCase_ReportTwoZFLUX008()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            using ZeroAlloc.Flux;
            namespace App { [Feature] public readonly partial record struct S(int Count); }
            namespace APP { [Feature] public readonly partial record struct S(int Count); }
            namespace app { [Feature] public readonly partial record struct s(int Count); }
            """));

        Assert.Null(outcome.Result.Exception);
        Assert.Equal(2, outcome.WithId("ZFLUX008").Length);
        Assert.Equal(
            new[] { "App.S.Store.g.cs", "FluxDispatcher.g.cs", "FluxServiceCollectionExtensions.g.cs" },
            outcome.HintNames);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcrossFiles_TheFeatureInTheEarlierFilePathIsGenerated(bool reverseCompilationOrder)
    {
        (string, string)[] files =
        {
            ("/src/A.cs", "using ZeroAlloc.Flux; namespace App; [Feature] public readonly partial record struct State(int Count);"),
            ("/src/B.cs", "using ZeroAlloc.Flux; namespace App; [Feature] public readonly partial record struct STATE(int Count);"),
        };
        if (reverseCompilationOrder) Array.Reverse(files);

        var outcome = GeneratorAssert.Run(files);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX008", diagnostic.Id);
        Assert.Equal("/src/B.cs", diagnostic.Location.SourceTree!.FilePath);
        var generated = string.Join("\n", outcome.Result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("global::App.State", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::App.STATE", generated, StringComparison.Ordinal);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    /// <summary>
    /// A type whose two partial declarations both carry <c>[Feature]</c> is found twice with the
    /// same name. The compiler reports the repeated attribute; the generator makes one store and
    /// reports neither ZFLUX008 nor any diagnostic twice.
    /// </summary>
    [Fact]
    public void FeatureAttributeOnTwoPartialDeclarations_GeneratesOneStore()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            using ZeroAlloc.Flux;
            namespace App;

            [Feature]
            public readonly partial record struct State(int Count);

            [Feature]
            public readonly partial record struct State;
            """));

        Assert.Null(outcome.Result.Exception);
        Assert.Empty(outcome.GeneratorDiagnostics);
        Assert.Equal(
            new[] { "App.State.Store.g.cs", "FluxDispatcher.g.cs", "FluxServiceCollectionExtensions.g.cs" },
            outcome.HintNames);
    }

    private static string Text(Location location) =>
        location.SourceTree!.ToString().Substring(location.SourceSpan.Start, location.SourceSpan.Length);
}
