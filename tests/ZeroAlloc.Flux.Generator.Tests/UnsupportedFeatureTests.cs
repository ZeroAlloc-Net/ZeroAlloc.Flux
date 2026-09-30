using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// A feature the generator cannot make a store for gets an error on the feature, and nothing is
/// generated for it: no store, no registration and no dispatch. Every other feature is generated
/// and the consumer code compiles. See #142.
/// </summary>
public sealed class UnsupportedFeatureTests
{
    private const string Prelude = """
        using ZeroAlloc.Flux;
        namespace App;

        public readonly record struct PingAction;

        [Feature]
        public readonly partial record struct CounterState(int Count);

        public static class CounterReducers
        {
            [Reducer]
            public static CounterState On(CounterState s, PingAction a) => s with { Count = s.Count + 1 };
        }

        """;

    /// <summary>
    /// The store class was <c>Store_App_GenericState&lt;T&gt;</c>, whose constructor is not valid C#,
    /// and <c>AddZeroAllocFlux</c> named an unbound <c>T</c>.
    /// </summary>
    [Fact]
    public void GenericFeature_ReportsZFLUX006_AndGeneratesNothingForIt()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            [Feature]
            public sealed partial record GenericState<T>(T Value);
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Feature 'global::App.GenericState<T>' is generic or nested in a generic type, so no store is generated for it; a store needs a closed state type",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertIdentifier(diagnostic, "GenericState");
        AssertOnlyCounterStateGenerated(outcome);
    }

    [Fact]
    public void FeatureNestedInAGenericType_ReportsZFLUX006_AndGeneratesNothingForIt()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            public class Outer<T>
            {
                [Feature]
                public readonly partial record struct InnerState(int Count);
            }
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX006", diagnostic.Id);
        AssertIdentifier(diagnostic, "InnerState");
        AssertOnlyCounterStateGenerated(outcome);
    }

    /// <summary>
    /// A reducer on a closed form of a generic feature names a type whose definition is a
    /// <c>[Feature]</c>. ZFLUX006 already covers it, so ZFLUX001, which says the type is not a
    /// feature, is not reported, and the reducer is not dispatched to.
    /// </summary>
    [Fact]
    public void ReducersOnAGenericFeature_AreNotReportedAsZFLUX001_AndNotDispatched()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            [Feature]
            public sealed partial record GenericState<T>(T Value);

            public static class GenericReducers
            {
                [Reducer]
                public static GenericState<int> On(GenericState<int> s, PingAction a) => s;

                [Reducer]
                public static GenericState<T> Open<T>(GenericState<T> s, PingAction a) => s;
            }
            """));

        Assert.Equal("ZFLUX006", Assert.Single(outcome.GeneratorDiagnostics).Id);
        AssertOnlyCounterStateGenerated(outcome);
    }

    [Theory]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("private protected")]
    public void FeatureNotAccessibleFromItsAssembly_ReportsZFLUX007_AndGeneratesNothingForIt(string accessibility)
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + $$"""
            public partial class Outer
            {
                [Feature]
                {{accessibility}} readonly partial record struct HiddenState(int Count);

                private static class Reducers
                {
                    [Reducer]
                    public static HiddenState On(HiddenState s, PingAction a) => s;
                }
            }
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Feature 'global::App.Outer.HiddenState' is not accessible to the rest of its assembly, so no store is generated for it; make it and each type that contains it public or internal",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertIdentifier(diagnostic, "HiddenState");
        AssertOnlyCounterStateGenerated(outcome);
    }

    [Fact]
    public void FeatureInAPrivateContainingType_ReportsZFLUX007()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            public class Outer
            {
                private class Middle
                {
                    [Feature]
                    public readonly partial record struct HiddenState(int Count);
                }
            }
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX007", diagnostic.Id);
        AssertIdentifier(diagnostic, "HiddenState");
        AssertOnlyCounterStateGenerated(outcome);
    }

    [Fact]
    public void FileLocalFeature_ReportsZFLUX007()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            [Feature]
            file readonly partial record struct HiddenState(int Count);
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX007", diagnostic.Id);
        AssertIdentifier(diagnostic, "HiddenState");
        AssertOnlyCounterStateGenerated(outcome);
    }

    [Fact]
    public void ProtectedInternalFeature_IsGenerated()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;
            namespace App;

            public readonly record struct PingAction;

            public class Outer
            {
                [Feature]
                protected internal readonly partial record struct State(int Count);

                protected internal static class Reducers
                {
                    [Reducer]
                    public static State On(State s, PingAction a) => s;
                }
            }
            """);
    }

    [Fact]
    public void OnlyUnsupportedFeatures_GenerateNothing()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", """
            using ZeroAlloc.Flux;
            namespace App;

            [Feature]
            public sealed partial record GenericState<T>(T Value);
            """));

        Assert.Equal("ZFLUX006", Assert.Single(outcome.GeneratorDiagnostics).Id);
        Assert.Empty(outcome.HintNames);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    private static void AssertIdentifier(Diagnostic diagnostic, string identifier)
    {
        Assert.Equal(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.Equal(identifier, diagnostic.Location.SourceTree!.ToString().Substring(
            diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    private static void AssertOnlyCounterStateGenerated(GeneratorAssert.Outcome outcome)
    {
        Assert.Equal(
            new[] { "App.CounterState.Store.g.cs", "FluxDispatcher.g.cs", "FluxServiceCollectionExtensions.g.cs" },
            outcome.HintNames);

        var generated = string.Join("\n", outcome.Result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.DoesNotContain("GenericState", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("InnerState", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("HiddenState", generated, StringComparison.Ordinal);
        GeneratorAssert.NoCompilerErrors(outcome);
    }
}
