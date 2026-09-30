using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// Every generated store class has a name that is unique in the compilation: it is qualified by
/// the feature's namespace and containing types the same way the store's hint name is, and the
/// encoding into one identifier is injective. See #142.
/// </summary>
public sealed class StoreClassNameTests
{
    /// <summary>
    /// Both stores were <c>Store_App_X_CounterState</c>, and the build failed with CS0101.
    /// </summary>
    [Fact]
    public void Features_WhoseNamesDifferOnlyInUnderscoreAndDot_Compile()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;

            namespace Shared
            {
                public readonly record struct PingAction;
            }

            namespace App_X
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);

                public static class Reducers
                {
                    [Reducer]
                    public static CounterState On(CounterState s, Shared.PingAction a) => s with { Count = s.Count + 1 };
                }
            }

            namespace App
            {
                [Feature]
                public readonly partial record struct X_CounterState(int Count);

                public static class Reducers
                {
                    [Reducer]
                    public static X_CounterState On(X_CounterState s, Shared.PingAction a) => s with { Count = s.Count + 1 };
                }
            }
            """);
    }

    /// <summary>
    /// A feature nested in <c>App.Outer</c> and a feature in namespace <c>App_Outer</c> were both
    /// <c>Store_App_Outer_CounterState</c>.
    /// </summary>
    [Fact]
    public void NestedFeature_AndFeatureInUnderscoredNamespace_Compile()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;

            namespace App
            {
                public static class Outer
                {
                    [Feature]
                    public readonly partial record struct CounterState(int Count);
                }
            }

            namespace App_Outer
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);
            }

            namespace App
            {
                [Feature]
                public readonly partial record struct Outer_CounterState(int Count);
            }
            """);
    }

    [Fact]
    public void NestedFeature_InTypesOfEveryAccessibleKind_Compiles()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;
            namespace App;

            public readonly record struct PingAction;

            internal struct S { [Feature] internal readonly partial record struct A(int Count); }
            public interface I { [Feature] public readonly partial record struct B(int Count); }
            public record R { [Feature] public readonly partial record struct C(int Count); }
            public class Outer { public class Middle { [Feature] public readonly partial record struct D(int Count); } }

            internal static class Reducers
            {
                [Reducer] public static S.A On(S.A s, PingAction a) => s;
                [Reducer] public static I.B On(I.B s, PingAction a) => s;
                [Reducer] public static R.C On(R.C s, PingAction a) => s;
                [Reducer] public static Outer.Middle.D On(Outer.Middle.D s, PingAction a) => s;
            }
            """);
    }

    [Theory]
    // The common case keeps the name it had before #142.
    [InlineData("namespace App; [Feature] public readonly partial record struct CounterState(int Count);", "Store_App_CounterState")]
    [InlineData("namespace A.B.C; [Feature] public readonly partial record struct S(int Count);", "Store_A_B_C_S")]
    [InlineData("[Feature] public readonly partial record struct GlobalState(int Count);", "Store_GlobalState")]
    // A '_' in a name is written as "_1", so it cannot be read as a namespace dot.
    [InlineData("namespace App_X; [Feature] public readonly partial record struct CounterState(int Count);", "Store_App_1X_CounterState")]
    [InlineData("namespace App; [Feature] public readonly partial record struct X_CounterState(int Count);", "Store_App_X_1CounterState")]
    [InlineData("namespace App; [Feature] public readonly partial record struct _S(int Count);", "Store_App__1S")]
    [InlineData("namespace App_; [Feature] public readonly partial record struct S(int Count);", "Store_App_1_S")]
    // Nesting is written as "_2", where the hint name has '+'.
    [InlineData("namespace App; public class Outer { [Feature] public readonly partial record struct S(int Count); }", "Store_App_Outer_2S")]
    [InlineData("namespace App; public class O_1 { [Feature] public readonly partial record struct S(int Count); }", "Store_App_O_11_2S")]
    // Verbatim identifiers are named without their '@'; letters outside ASCII are kept.
    [InlineData("namespace Café.@event; [Feature] public sealed partial record Ωmega(int Count);", "Store_Café_event_Ωmega")]
    public void StoreClassName_IsQualifiedAndInjective(string source, string expected)
    {
        var feature = FeatureSymbol("using ZeroAlloc.Flux;\n" + source);

        Assert.Equal(expected, StoreEmitter.GetStoreClassName(feature));
    }

    [Fact]
    public void StoreClassNames_OfNamesThatDifferOnlyInUnderscoreDotAndNesting_AreDistinct()
    {
        var names = new[]
        {
            "namespace App_X; [Feature] public readonly partial record struct CounterState(int Count);",
            "namespace App; [Feature] public readonly partial record struct X_CounterState(int Count);",
            "namespace App.X; [Feature] public readonly partial record struct CounterState(int Count);",
            "namespace App; public class X { [Feature] public readonly partial record struct CounterState(int Count); }",
            "namespace App; public class X_ { [Feature] public readonly partial record struct CounterState(int Count); }",
            "namespace App; public class X { [Feature] public readonly partial record struct _CounterState(int Count); }",
            "namespace App._X; [Feature] public readonly partial record struct CounterState(int Count);",
            "namespace App_._X; [Feature] public readonly partial record struct CounterState(int Count);",
            "namespace App__X; [Feature] public readonly partial record struct CounterState(int Count);",
        }.Select(s => StoreEmitter.GetStoreClassName(FeatureSymbol("using ZeroAlloc.Flux;\n" + s))).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static INamedTypeSymbol FeatureSymbol(string source)
    {
        var compilation = TestHarness.CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source) });
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var declaration = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Single(d => d.AttributeLists.Count > 0);
        return model.GetDeclaredSymbol(declaration)!;
    }
}
