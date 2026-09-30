using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Every ZFLUX diagnostic is reported at the type, attribute, method or parameter it is about, as
/// a location bound to the syntax tree, so the IDE can point at it and <c>#pragma warning
/// disable</c> can suppress it. A source marked with [| and |] gives the expected spans; the
/// markers are removed before it runs.
/// </summary>
public sealed class DiagnosticLocationTests
{
    private const string Prelude = """
        using System;
        using ZeroAlloc.Flux;
        namespace Sample;

        public sealed record IncrementAction(int By);

        """;

    [Theory]
    // ZFLUX001: the state parameter whose type is not a [Feature].
    [InlineData("ZFLUX001", Prelude + """
        public readonly record struct NotAFeature(int Count);
        public static class Reducers
        {
            [Reducer]
            public static NotAFeature On(NotAFeature [|s|], IncrementAction a) => s;
        }
        """)]
    // ZFLUX003: the reducer method's identifier.
    [InlineData("ZFLUX003", Prelude + """
        [Feature]
        public readonly partial record struct CounterState(int Count);
        public class Reducers
        {
            [Reducer]
            public CounterState [|On|](CounterState s, IncrementAction a) => s;
        }
        """)]
    // ZFLUX003: a reducer with one parameter is a signature error too, not a silent skip.
    [InlineData("ZFLUX003", Prelude + """
        [Feature]
        public readonly partial record struct CounterState(int Count);
        public static class Reducers
        {
            [Reducer]
            public static CounterState [|On|](CounterState s) => s;
        }
        """)]
    // ZFLUX004: the InitialState argument that names a factory that does not exist.
    [InlineData("ZFLUX004", Prelude + """
        [Feature([|InitialState = "DoesNotExist"|])]
        public readonly partial record struct CounterState(int Count);
        """)]
    // ZFLUX004: the factory method whose signature is wrong.
    [InlineData("ZFLUX004", Prelude + """
        [Feature(InitialState = "Create")]
        public readonly partial record struct CounterState(int Count)
        {
            public static CounterState [|Create|]() => new(0);
        }
        """)]
    // ZFLUX005: the feature type's identifier.
    [InlineData("ZFLUX005", Prelude + """
        [Feature]
        public readonly record struct [|CounterState|](int Count);
        """)]
    // ZFLUX006: the generic feature type's identifier.
    [InlineData("ZFLUX006", Prelude + """
        [Feature]
        public sealed partial record [|GenericState|]<T>(T Value);
        """)]
    // ZFLUX007: the identifier of the feature type that is not accessible.
    [InlineData("ZFLUX007", Prelude + """
        public partial class Outer
        {
            [Feature]
            private readonly partial record struct [|HiddenState|](int Count);
        }
        """)]
    public void Diagnostic_IsReportedAtItsSourceLocation(string id, string markedSource)
    {
        var (source, spans) = Unmark(markedSource);

        var diagnostics = TestHarness.RunOnFile(source);

        AssertAt(One(diagnostics, id).Location, source, Assert.Single(spans));
    }

    [Fact]
    public void ZFLUX002_IsReportedAtTheLaterReducer_WithTheEarlierAsAdditionalLocation()
    {
        var (source, spans) = Unmark(Prelude + """
            [Feature]
            public readonly partial record struct CounterState(int Count);
            public static class Reducers
            {
                [Reducer]
                public static CounterState [|OnA|](CounterState s, IncrementAction a) => s;
                [Reducer]
                public static CounterState [|OnB|](CounterState s, IncrementAction a) => s;
            }
            """);

        var diagnostics = TestHarness.RunOnFile(source);

        var diagnostic = One(diagnostics, "ZFLUX002");
        AssertAt(diagnostic.Location, source, spans[1]);
        AssertAt(Assert.Single(diagnostic.AdditionalLocations), source, spans[0]);
    }

    [Fact]
    public void PragmaAroundOneFeature_SuppressesThatDiagnosticOnly()
    {
        // Both features are not partial and report ZFLUX005; the pragma covers Quiet only.
        // Quiet's InitialState names a missing factory, so ZFLUX004 fires inside the same
        // region, and the pragma does not name it.
        var source = Prelude + """
            #pragma warning disable ZFLUX005
            [Feature(InitialState = "Missing")]
            public readonly record struct QuietState(int Count);
            #pragma warning restore ZFLUX005

            [Feature]
            public readonly record struct LoudState(int Count);
            """;

        var diagnostics = TestHarness.RunOnFile(source);

        var zflux005 = diagnostics.Where(d => string.Equals(d.Id, "ZFLUX005", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, zflux005.Count);
        Assert.True(ForType(zflux005, "QuietState").IsSuppressed);
        Assert.False(ForType(zflux005, "LoudState").IsSuppressed);
        Assert.False(One(diagnostics, "ZFLUX004").IsSuppressed);

        static Diagnostic ForType(List<Diagnostic> list, string typeName) =>
            Assert.Single(list, d => d.GetMessage(CultureInfo.InvariantCulture)
                .Contains("Sample." + typeName + "'", StringComparison.Ordinal));
    }

    [Fact]
    public void EditAboveAFeature_MovesItsDiagnostic()
    {
        const string source = Prelude + """
            [Feature]
            public readonly record struct CounterState(int Count);
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: TestHarness.TestFilePath);
        var compilation = TestHarness.CreateCompilation(new[] { tree });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FluxGenerator().AsSourceGenerator());
        driver = driver.RunGenerators(compilation);
        var before = One(driver.GetRunResult().Diagnostics, "ZFLUX005");

        var moved = tree.WithChangedText(SourceText.From(
            source.Replace("namespace Sample;", "namespace Sample;\n\n// two\n// more lines", StringComparison.Ordinal)));
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(tree, moved));
        var after = One(driver.GetRunResult().Diagnostics, "ZFLUX005");

        Assert.Same(moved, after.Location.SourceTree);
        Assert.Equal(
            before.Location.GetLineSpan().StartLinePosition.Line + 3,
            after.Location.GetLineSpan().StartLinePosition.Line);
    }

    private static Diagnostic One(ImmutableArray<Diagnostic> diagnostics, string id) =>
        Assert.Single(diagnostics, d => string.Equals(d.Id, id, StringComparison.Ordinal));

    private static void AssertAt(Location location, string source, TextSpan expected)
    {
        // A source location, bound to the tree, is what #pragma and the IDE need.
        Assert.Equal(LocationKind.SourceFile, location.Kind);
        Assert.Equal(TestHarness.TestFilePath, location.SourceTree!.FilePath);
        Assert.Equal(expected, location.SourceSpan);
        Assert.Equal(SourceText.From(source).Lines.GetLinePositionSpan(expected), location.GetLineSpan().Span);
    }

    private static (string source, List<TextSpan> spans) Unmark(string marked)
    {
        var sb = new StringBuilder(marked.Length);
        var spans = new List<TextSpan>();
        var start = -1;
        for (var i = 0; i < marked.Length; i++)
        {
            if (string.CompareOrdinal(marked, i, "[|", 0, 2) == 0)
            {
                start = sb.Length;
                i++;
            }
            else if (string.CompareOrdinal(marked, i, "|]", 0, 2) == 0)
            {
                spans.Add(TextSpan.FromBounds(start, sb.Length));
                i++;
            }
            else
            {
                sb.Append(marked[i]);
            }
        }

        return (sb.ToString(), spans);
    }
}
