using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// An edit to a file with no <c>[Feature]</c> or <c>[Reducer]</c> must not make the generator redo
/// its work. The pipeline carries value-equal models, so the second run finds every tracked step
/// and every output cached or unchanged. See ZeroAlloc.Flux#132.
/// </summary>
public sealed class IncrementalityTests
{
    // Features of both kinds, an InitialState factory, a fan-out action, and a diagnostic of each
    // kind the models carry, so every model field and the diagnostic locations take part.
    private const string Flux = """
        using System;
        using ZeroAlloc.Flux;
        namespace Sample;

        [Feature]
        public readonly partial record struct CounterState(int Count);

        [Feature(InitialState = nameof(Create))]
        public sealed partial record SettingsState(string Theme)
        {
            public static SettingsState Create(IServiceProvider sp) => new("default");
        }

        [Feature]
        public readonly record struct NotPartialState(int Count);

        [Feature(InitialState = "Missing")]
        public readonly partial record struct NoFactoryState(int Count);

        public readonly record struct IncrementAction(int By);
        public sealed record ThemeAction(string Theme);
        public readonly record struct NotAFeature(int X);

        public static class Reducers
        {
            [Reducer]
            public static CounterState On(CounterState s, IncrementAction a) => s with { Count = s.Count + a.By };

            [Reducer]
            public static NotPartialState On(NotPartialState s, IncrementAction a) => s;

            [Reducer]
            public static SettingsState On(SettingsState s, ThemeAction a) => s with { Theme = a.Theme };

            [Reducer]
            public static SettingsState Again(SettingsState s, ThemeAction a) => s;

            [Reducer]
            public static NotAFeature On(NotAFeature s, IncrementAction a) => s;

            [Reducer]
            public static CounterState Lonely(CounterState s) => s;
        }
        """;

    private const string Unrelated = "namespace Sample; public static class Other { public static int V => 1; }";

    private static readonly string[] ExpectedDiagnostics = { "ZFLUX001", "ZFLUX002", "ZFLUX003", "ZFLUX004", "ZFLUX005" };

    [Fact]
    public void UnrelatedEdit_LeavesEveryTrackedStepAndOutputCachedOrUnchanged()
    {
        var unrelated = CSharpSyntaxTree.ParseText(Unrelated, path: "/src/Other.cs");
        var compilation = TestHarness.CreateCompilation(new[]
        {
            CSharpSyntaxTree.ParseText(Flux, path: "/src/Flux.cs"),
            unrelated,
        });

        var driver = CreateDriver().RunGenerators(compilation);
        var first = driver.GetRunResult().Results.Single();
        Assert.Equal(
            ExpectedDiagnostics,
            first.Diagnostics.Select(d => d.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);

        var edited = compilation.ReplaceSyntaxTree(
            unrelated,
            CSharpSyntaxTree.ParseText(Unrelated.Replace("=> 1", "=> 2", StringComparison.Ordinal), path: "/src/Other.cs"));
        driver = driver.RunGenerators(edited);
        var second = driver.GetRunResult().Results.Single();

        foreach (var name in FluxGenerator.TrackingNames.All)
        {
            Assert.True(second.TrackedSteps.ContainsKey(name), $"step {name} is not tracked");
            AssertAllCachedOrUnchanged(name, second.TrackedSteps[name].AsEnumerable());
        }

        Assert.NotEmpty(second.TrackedOutputSteps);
        foreach (var pair in second.TrackedOutputSteps)
        {
            AssertAllCachedOrUnchanged(pair.Key, pair.Value.AsEnumerable());
        }

        Assert.Equal(Sources(first), Sources(second));
        Assert.Equal(
            first.Diagnostics.Select(d => d.ToString()).ToList(),
            second.Diagnostics.Select(d => d.ToString()).ToList());
    }

    [Fact]
    public void EditToAReducer_RegeneratesTheOutput()
    {
        var flux = CSharpSyntaxTree.ParseText(Flux, path: "/src/Flux.cs");
        var compilation = TestHarness.CreateCompilation(new[] { flux });

        var driver = CreateDriver().RunGenerators(compilation);
        var first = driver.GetRunResult().Results.Single();

        var edited = compilation.ReplaceSyntaxTree(
            flux,
            CSharpSyntaxTree.ParseText(
                Flux.Replace("CounterState On(CounterState s, IncrementAction a)", "CounterState OnIncrement(CounterState s, IncrementAction a)", StringComparison.Ordinal),
                path: "/src/Flux.cs"));
        driver = driver.RunGenerators(edited);
        var second = driver.GetRunResult().Results.Single();

        var reasons = second.TrackedOutputSteps
            .SelectMany(pair => pair.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason);
        Assert.Contains(IncrementalStepRunReason.Modified, reasons);

        var dispatcher = Dispatcher(second);
        Assert.Contains("global::Sample.Reducers.OnIncrement(", dispatcher, StringComparison.Ordinal);
        Assert.NotEqual(Dispatcher(first), dispatcher, StringComparer.Ordinal);

        static string Dispatcher(GeneratorRunResult result) =>
            result.GeneratedSources
                .Single(s => string.Equals(s.HintName, "FluxDispatcher.g.cs", StringComparison.Ordinal))
                .SourceText.ToString();
    }

    private static List<string> Sources(GeneratorRunResult result) =>
        result.GeneratedSources.Select(s => s.HintName + ": " + s.SourceText.ToString()).ToList();

    private static GeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            new[] { new FluxGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static void AssertAllCachedOrUnchanged(string name, IEnumerable<IncrementalGeneratorRunStep> steps)
    {
        foreach (var step in steps)
        {
            foreach (var (_, reason) in step.Outputs)
            {
                Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"step {name} output was {reason}");
            }
        }
    }
}
