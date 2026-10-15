using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// A reducer or feature that breaks the contract the generated code relies on reports a ZFLUX
/// error at the user's code, and the generator emits nothing that would fail to compile in its
/// place. Before #163 a third reducer parameter, or a record class feature with only a primary
/// constructor, passed silently and the build failed with CS7036 inside a generated file.
/// </summary>
public sealed class ContractDiagnosticTests
{
    private const string Prelude = """
        using System;
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

    [Theory]
    [InlineData("CounterState s, PingAction a, int extra")]
    [InlineData("CounterState s, PingAction a, int extra = 0")]
    [InlineData("CounterState s, PingAction a, params int[] extra")]
    [InlineData("ref CounterState s, PingAction a")]
    [InlineData("CounterState s, ref PingAction a")]
    [InlineData("CounterState s, out PingAction a")]
    public void ReducerTheDispatcherCannotCall_ReportsZFLUX003_AndIsNotDispatched(string parameters)
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + $$"""
            public readonly record struct ResetAction;

            public static class ResetReducers
            {
                [Reducer]
                public static CounterState Reset({{parameters}}) { {{(parameters.Contains("out ", StringComparison.Ordinal) ? "a = default;" : "")}} return default; }
            }
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.StartsWith(
            "Reducer 'App.ResetReducers.Reset(",
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "must be public static, take exactly two parameters, the state and the action, passed by value or with in, and return the same type as its first parameter",
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        Assert.DoesNotContain("ResetReducers", GeneratedText(outcome), StringComparison.Ordinal);
        AssertNoCS7036(outcome);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    [Fact]
    public void ReducerWithInParameters_IsDispatched()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;
            namespace App;

            public readonly record struct PingAction;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public static class CounterReducers
            {
                [Reducer]
                public static CounterState On(in CounterState s, in PingAction a) => s with { Count = s.Count + 1 };
            }
            """);
    }

    [Fact]
    public void RecordClassWithOnlyAPrimaryConstructor_ReportsZFLUX009_AndGeneratesNothingForIt()
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + """
            [Feature]
            public sealed partial record SettingsState(string Theme);

            public static class SettingsReducers
            {
                [Reducer]
                public static SettingsState On(SettingsState s, PingAction a) => s;
            }
            """));

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("ZFLUX009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "Feature 'global::App.SettingsState' has no public or internal parameterless constructor and no InitialState factory, so no store is generated for it; add a parameterless constructor or set [Feature(InitialState = ...)]",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertOnlyCounterStateGenerated(outcome, "SettingsState");
    }

    [Theory]
    [InlineData("""
        [Feature]
        public sealed partial record SettingsState(string Theme)
        {
            private SettingsState() : this("light") { }
        }
        """)]
    [InlineData("""
        [Feature]
        public partial class SettingsState
        {
            protected SettingsState() { }
        }
        """)]
    [InlineData("""
        [Feature]
        public partial class SettingsState
        {
            public SettingsState(string theme) { }
        }
        """)]
    [InlineData("""
        [Feature]
        public abstract partial record SettingsState;
        """)]
    public void ClassFeatureTheStoreCannotCreate_ReportsZFLUX009(string feature)
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + feature));

        Assert.Equal("ZFLUX009", Assert.Single(outcome.GeneratorDiagnostics).Id);
        AssertOnlyCounterStateGenerated(outcome, "SettingsState");
    }

    [Theory]
    [InlineData("""
        [Feature]
        public sealed partial record SettingsState(string Theme)
        {
            public SettingsState() : this("light") { }
        }
        """)]
    [InlineData("""
        [Feature]
        public sealed partial record SettingsState(string Theme)
        {
            internal SettingsState() : this("light") { }
        }
        """)]
    [InlineData("""
        [Feature]
        public sealed partial class SettingsState
        {
        }
        """)]
    [InlineData("""
        [Feature(InitialState = nameof(Create))]
        public sealed partial record SettingsState(string Theme)
        {
            public static SettingsState Create(IServiceProvider services) => new("light");
        }
        """)]
    public void ClassFeatureTheStoreCanCreate_IsGenerated(string feature)
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + feature));

        Assert.Empty(outcome.GeneratorDiagnostics);
        Assert.Contains("App.SettingsState.Store.g.cs", outcome.HintNames, StringComparer.Ordinal);
        GeneratorAssert.NoCompilerErrors(outcome);
    }

    /// <summary>
    /// The store used to be generated with a call to the invalid factory, which added CS1501 or
    /// CS7036 in the generated file next to ZFLUX004.
    /// </summary>
    [Theory]
    [InlineData("""
        [Feature(InitialState = "Missing")]
        public readonly partial record struct LimitState(int Max);
        """)]
    [InlineData("""
        [Feature(InitialState = nameof(Create))]
        public readonly partial record struct LimitState(int Max)
        {
            public static LimitState Create() => new(100);
        }
        """)]
    public void InvalidInitialStateFactory_ReportsOnlyZFLUX004_AndGeneratesNothingForIt(string feature)
    {
        var outcome = GeneratorAssert.Run(("/src/App.cs", Prelude + feature));

        Assert.Equal("ZFLUX004", Assert.Single(outcome.GeneratorDiagnostics).Id);
        AssertOnlyCounterStateGenerated(outcome, "LimitState");
    }

    private static string GeneratedText(GeneratorAssert.Outcome outcome)
        => string.Join("\n", outcome.Result.GeneratedSources.Select(s => s.SourceText.ToString()));

    private static void AssertNoCS7036(GeneratorAssert.Outcome outcome)
        => Assert.DoesNotContain(
            outcome.Output.GetDiagnostics(),
            d => string.Equals(d.Id, "CS7036", StringComparison.Ordinal));

    private static void AssertOnlyCounterStateGenerated(GeneratorAssert.Outcome outcome, string skipped)
    {
        Assert.Equal(
            new[] { "App.CounterState.Store.g.cs", "FluxDispatcher.g.cs", "FluxServiceCollectionExtensions.g.cs" },
            outcome.HintNames);
        Assert.DoesNotContain(skipped, GeneratedText(outcome), StringComparison.Ordinal);
        AssertNoCS7036(outcome);
        GeneratorAssert.NoCompilerErrors(outcome);
    }
}
