using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Every generated store file has a hint name that is unique in the compilation and that Roslyn
/// accepts, whatever the feature's namespace, containing types or generic arity.
/// </summary>
public sealed class HintNameTests
{
    [Fact]
    public void SameNamedFeatures_InDifferentContainingTypes_Compile()
    {
        AssertCompiles("""
            using ZeroAlloc.Flux;
            namespace App;

            public static partial class Left
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);
            }

            public static partial class Right
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);
            }
            """);
    }

    /// <summary>
    /// <c>App_X.CounterState</c> and <c>App.X_CounterState</c> were both written to
    /// <c>Store_App_X_CounterState.g.cs</c>, so the generator threw and emitted nothing.
    /// Their store classes still share a name, which is #142, so this checks the
    /// generator's own output rather than the compilation.
    /// </summary>
    [Fact]
    public void Features_WhoseNamesDifferOnlyInUnderscoreAndDot_GetDistinctFiles()
    {
        var result = RunGenerator("""
            using ZeroAlloc.Flux;

            namespace App_X
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);
            }

            namespace App
            {
                [Feature]
                public readonly partial record struct X_CounterState(int Count);
            }
            """);

        Assert.Null(result.Exception);
        Assert.Equal(
            new[]
            {
                "App.X_CounterState.Store.g.cs",
                "App_X.CounterState.Store.g.cs",
                "FluxDispatcher.g.cs",
                "FluxServiceCollectionExtensions.g.cs",
            },
            HintNamesOf(result));
    }

    /// <summary>
    /// The hint name was built from the store class name, which carries the feature's type
    /// parameters, so Roslyn rejected the <c>&lt;</c> and the generator emitted nothing. The
    /// store class itself does not compile for a generic feature, which is #142.
    /// </summary>
    [Fact]
    public void GenericFeatures_AreNamedWithTheirArity()
    {
        var result = RunGenerator("""
            using ZeroAlloc.Flux;
            namespace App;

            [Feature]
            public partial record GenericState<T>(T Value);

            public partial class Outer<T>
            {
                [Feature]
                public partial record Inner<U>(U Value);
            }
            """);

        Assert.Null(result.Exception);
        Assert.Equal(
            new[]
            {
                "App.GenericState`1.Store.g.cs",
                "App.Outer`1+Inner`1.Store.g.cs",
                "FluxDispatcher.g.cs",
                "FluxServiceCollectionExtensions.g.cs",
            },
            HintNamesOf(result));
    }

    [Fact]
    public void GeneratedStoreFiles_AreNamedAfterNamespaceAndContainingTypes()
    {
        var result = RunGenerator("""
            using ZeroAlloc.Flux;

            [Feature]
            public readonly partial record struct GlobalState(int Count);

            namespace App
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);

                public static partial class Outer
                {
                    [Feature]
                    public readonly partial record struct NestedState(int Count);
                }
            }
            """);

        Assert.Null(result.Exception);
        Assert.Equal(
            new[]
            {
                "App.CounterState.Store.g.cs",
                "App.Outer+NestedState.Store.g.cs",
                "FluxDispatcher.g.cs",
                "FluxServiceCollectionExtensions.g.cs",
                "GlobalState.Store.g.cs",
            },
            HintNamesOf(result));
    }

    /// <summary>
    /// A verbatim identifier is named without its <c>@</c>, and a letter outside ASCII is kept.
    /// </summary>
    [Fact]
    public void VerbatimAndNonAsciiNames_AreNamedByTheirIdentifier()
    {
        var result = RunGenerator("""
            using ZeroAlloc.Flux;
            namespace Café.@event;

            [Feature]
            public readonly partial record struct Ωmega(int Count);
            """);

        Assert.Null(result.Exception);
        Assert.Contains("Café.event.Ωmega.Store.g.cs", HintNamesOf(result), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("M", "M")]
    [InlineData("App.M", "App.M")]
    [InlineData("App.Outer+M`1", "App.Outer+M`1")]
    [InlineData("Café.Ωmega_1", "Café.Ωmega_1")]
    [InlineData("a/b|c:d*e?f<g>h", "a-u002Fb-u007Cc-u003Ad-u002Ae-u003Ff-u003Cg-u003Eh")]
    [InlineData("a-b", "a-u002Db")]
    public void Sanitize_KeepsIdentifierCharactersAndEscapesTheRest(string name, string expected)
    {
        Assert.Equal(expected, HintNames.Sanitize(name));
    }

    [Fact]
    public void Sanitize_EscapesControlAndSeparatorCharacters_AndKeepsAstralLetters()
    {
        var backslash = ((char)92).ToString();
        var quote = ((char)34).ToString();
        var tab = ((char)9).ToString();
        var mathBoldA = char.ConvertFromUtf32(0x1D400);

        Assert.Equal("a-u005Cb", HintNames.Sanitize("a" + backslash + "b"));
        Assert.Equal("a-u0022b", HintNames.Sanitize("a" + quote + "b"));
        Assert.Equal("a-u0009b", HintNames.Sanitize("a" + tab + "b"));
        Assert.Equal(mathBoldA + "x", HintNames.Sanitize(mathBoldA + "x"));
        Assert.Equal("-uD835x", HintNames.Sanitize(mathBoldA.Substring(0, 1) + "x"));
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var compilation = TestHarness.CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source) });
        return CSharpGeneratorDriver.Create(new FluxGenerator().AsSourceGenerator())
            .RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private static string[] HintNamesOf(GeneratorRunResult result) =>
        result.GeneratedSources.Select(s => s.HintName).OrderBy(h => h, StringComparer.Ordinal).ToArray();

    private static void AssertCompiles(string source)
    {
        var compilation = TestHarness.CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source) })
            .AddReferences(MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

        CSharpGeneratorDriver.Create(new FluxGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        Assert.Empty(generatorDiagnostics);
        var errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
