using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Discovers <c>[Reducer]</c>-decorated methods and validates each signature against the
/// Flux contract: <c>public static TState Method(TState, TAction, ...)</c> where <c>TState</c>
/// is a <c>[Feature]</c> type. Surfaces ZFLUX001 / ZFLUX002 / ZFLUX003.
/// </summary>
/// <remarks>
/// Mirrors <see cref="FeatureDiscovery"/>: a <see cref="Transform"/> hook for the
/// <c>IIncrementalGenerator</c> pipeline (Task 2.9) and a direct
/// <see cref="DiscoverFromCompilation"/> helper used by unit tests.
/// </remarks>
internal static class ReducerDiscovery
{
    /// <summary>Metadata name used by <c>ForAttributeWithMetadataName</c>.</summary>
    public const string ReducerAttributeFullName = "ZeroAlloc.Flux.ReducerAttribute";

    /// <summary>
    /// Lightweight pre-validation transform used by the pipeline path. Returns a raw
    /// <see cref="ReducerInfo"/> built straight from the symbol; downstream stages perform
    /// the full set of validation checks (ZFLUX001 needs the known-feature set, so it
    /// cannot run here).
    /// </summary>
    /// <remarks>
    /// The candidate has no <see cref="ReducerInfo"/> when the method has fewer than 2
    /// parameters or its state or action parameter type isn't a named type. Those fail the
    /// structural prerequisites for any downstream check, and are reported as ZFLUX003.
    /// </remarks>
    public static ReducerCandidate? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;
        if (method.ContainingType is null) return null;
        if (method.Parameters.Length < 2
            || method.Parameters[0].Type is not INamedTypeSymbol stateType
            || method.Parameters[1].Type is not INamedTypeSymbol actionType)
        {
            return new ReducerCandidate(method, null);
        }

        return new ReducerCandidate(method, new ReducerInfo(
            method,
            method.ContainingType,
            stateType,
            actionType,
            method.Name,
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    /// <summary>
    /// Walks the supplied <paramref name="compilation"/> for <c>[Reducer]</c>-decorated methods,
    /// validates each, and cross-checks for ZFLUX002 duplicates. The <paramref name="features"/>
    /// list is needed so ZFLUX001 can be raised when a state parameter isn't a known feature.
    /// </summary>
    public static (ImmutableArray<ReducerInfo> Reducers, ImmutableArray<Diagnostic> Diagnostics)
        DiscoverFromCompilation(Compilation compilation, ImmutableArray<FeatureInfo> features)
    {
        var reducerAttr = compilation.GetTypeByMetadataName(ReducerAttributeFullName);
        if (reducerAttr is null)
        {
            return (ImmutableArray<ReducerInfo>.Empty, ImmutableArray<Diagnostic>.Empty);
        }

        var knownFeatureTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var f in features) knownFeatureTypes.Add(f.TypeSymbol);

        var allCandidates = new List<(IMethodSymbol Method, AttributeData Attr)>();
        WalkNamespace(compilation.SourceModule.GlobalNamespace, reducerAttr, allCandidates);

        var reducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var (method, _) in allCandidates)
        {
            var methodDisplay = method.ToDisplayString();

            // ZFLUX003: must be public static with >= 2 params and return-type == first-param type.
            var signatureOk =
                method.DeclaredAccessibility == Accessibility.Public &&
                method.IsStatic &&
                method.Parameters.Length >= 2 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, method.Parameters[0].Type);

            if (!signatureOk)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                    SourceLocations.Of(method),
                    methodDisplay));
                continue;
            }

            if (method.Parameters[0].Type is not INamedTypeSymbol stateType)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                    SourceLocations.Of(method),
                    methodDisplay));
                continue;
            }

            if (method.Parameters[1].Type is not INamedTypeSymbol actionType)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                    SourceLocations.Of(method),
                    methodDisplay));
                continue;
            }

            // ZFLUX001: state parameter type must be a known [Feature]. At the state parameter.
            if (!knownFeatureTypes.Contains(stateType))
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.ZFLUX001_ReducerOnNonFeatureState,
                    SourceLocations.Of(method.Parameters[0]),
                    methodDisplay,
                    stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                continue;
            }

            if (method.ContainingType is null) continue;

            reducers.Add(new ReducerInfo(
                method,
                method.ContainingType,
                stateType,
                actionType,
                method.Name,
                method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        diagnostics.AddRange(FindDuplicates(reducers));

        return (reducers.ToImmutable(), diagnostics.ToImmutable());
    }

    /// <summary>
    /// ZFLUX002: within the same OwningType, no two reducers may share (StateType, ActionType).
    /// Reported once per clash, at the later reducer, with the others as additional locations.
    /// </summary>
    public static IEnumerable<Diagnostic> FindDuplicates(IEnumerable<ReducerInfo> reducers)
    {
        var groups = new Dictionary<(INamedTypeSymbol Owning, INamedTypeSymbol State, INamedTypeSymbol Action), List<ReducerInfo>>(
            new TripleComparer());
        var order = new List<List<ReducerInfo>>();
        foreach (var r in reducers)
        {
            var key = (r.OwningType, r.StateType, r.ActionType);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new List<ReducerInfo>();
                groups.Add(key, group);
                order.Add(group);
            }
            group.Add(r);
        }

        foreach (var group in order)
        {
            if (group.Count < 2) continue;

            var located = new List<Location>(group.Count);
            foreach (var r in group) located.Add(SourceLocations.Of(r.MethodSymbol));
            located.Sort(SourceLocations.Compare);

            var later = located[located.Count - 1];
            located.RemoveAt(located.Count - 1);
            yield return Diagnostic.Create(
                Diagnostics.ZFLUX002_DuplicateReducerInFeature,
                later,
                located,
                group[0].StateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                group[0].ActionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
    }

    private static void WalkNamespace(
        INamespaceOrTypeSymbol root,
        INamedTypeSymbol reducerAttr,
        List<(IMethodSymbol, AttributeData)> sink)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current is INamedTypeSymbol currentType)
            {
                ProcessType(currentType, reducerAttr, sink);
            }

            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    stack.Push(ns);
                }
                else if (member is INamedTypeSymbol type)
                {
                    foreach (var nested in type.GetTypeMembers()) stack.Push(nested);
                    ProcessType(type, reducerAttr, sink);
                }
            }
        }
    }

    private static void ProcessType(
        INamedTypeSymbol type,
        INamedTypeSymbol reducerAttr,
        List<(IMethodSymbol, AttributeData)> sink)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            foreach (var attr in method.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, reducerAttr))
                {
                    sink.Add((method, attr));
                    break;
                }
            }
        }
    }

    private sealed class TripleComparer
        : IEqualityComparer<(INamedTypeSymbol, INamedTypeSymbol, INamedTypeSymbol)>
    {
        public bool Equals(
            (INamedTypeSymbol, INamedTypeSymbol, INamedTypeSymbol) x,
            (INamedTypeSymbol, INamedTypeSymbol, INamedTypeSymbol) y) =>
            SymbolEqualityComparer.Default.Equals(x.Item1, y.Item1) &&
            SymbolEqualityComparer.Default.Equals(x.Item2, y.Item2) &&
            SymbolEqualityComparer.Default.Equals(x.Item3, y.Item3);

        public int GetHashCode((INamedTypeSymbol, INamedTypeSymbol, INamedTypeSymbol) obj)
        {
            unchecked
            {
                var h = SymbolEqualityComparer.Default.GetHashCode(obj.Item1);
                h = (h * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.Item2);
                h = (h * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.Item3);
                return h;
            }
        }
    }
}
