using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

/// <summary>
/// The generated sources compile together with the consumer's code. Each fixture is a valid
/// Flux program, so any compiler error comes from what the generator emitted.
/// </summary>
public sealed class GeneratedCodeCompilesTests
{
    [Fact]
    public void FanOut_ToFeaturesWithTheSameName_InDifferentNamespaces_Compiles()
    {
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;

            namespace Shared
            {
                public readonly record struct PingAction;
            }

            namespace Left
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);

                public static class Reducers
                {
                    [Reducer]
                    public static CounterState On(CounterState s, Shared.PingAction a) => s with { Count = s.Count + 1 };
                }
            }

            namespace Right
            {
                [Feature]
                public readonly partial record struct CounterState(int Count);

                public static class Reducers
                {
                    [Reducer]
                    public static CounterState On(CounterState s, Shared.PingAction a) => s with { Count = s.Count + 1 };
                }
            }
            """);
    }

    [Fact]
    public void FanOut_ToOneFeature_FromTwoReducerClasses_Compiles()
    {
        // ZFLUX002 only forbids two reducers for one (state, action) within one owning type.
        GeneratorAssert.Compiles("""
            using ZeroAlloc.Flux;
            namespace Sample;

            [Feature]
            public readonly partial record struct CounterState(int Count);

            public readonly record struct PingAction;

            public static class CountReducers
            {
                [Reducer]
                public static CounterState On(CounterState s, PingAction a) => s with { Count = s.Count + 1 };
            }

            public static class DoubleReducers
            {
                [Reducer]
                public static CounterState On(CounterState s, PingAction a) => s with { Count = s.Count * 2 };
            }
            """);
    }
}
