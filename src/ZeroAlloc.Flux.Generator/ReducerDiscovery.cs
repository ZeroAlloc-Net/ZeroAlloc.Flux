using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Builds a <see cref="ReducerCandidate"/> for each <c>[Reducer]</c>-decorated method and
/// validates its signature against the Flux contract: <c>public static TState Method(TState,
/// TAction, ...)</c>. Surfaces ZFLUX003 here, and ZFLUX002 through <see cref="FindDuplicates"/>.
/// </summary>
internal static class ReducerDiscovery
{
    /// <summary>Metadata name used by <c>ForAttributeWithMetadataName</c>.</summary>
    public const string ReducerAttributeFullName = "ZeroAlloc.Flux.ReducerAttribute";

    /// <summary>
    /// Transform for the <c>ForAttributeWithMetadataName</c> pipeline branch. The symbols are read
    /// here and projected into value data, so the candidate compares equal across compilations.
    /// </summary>
    /// <remarks>
    /// ZFLUX003 is decided here: the method must be public and static, with at least two
    /// parameters whose types are named types, and return its first parameter's type. ZFLUX001
    /// needs the full feature set, so it is checked later, in <see cref="FluxValidation"/>.
    /// </remarks>
    public static ReducerCandidate? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;
        if (method.ContainingType is null) return null;
        ct.ThrowIfCancellationRequested();

        var methodDisplay = method.ToDisplayString();
        var signatureOk =
            method.DeclaredAccessibility == Accessibility.Public &&
            method.IsStatic &&
            method.Parameters.Length >= 2 &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, method.Parameters[0].Type);

        if (!signatureOk
            || method.Parameters[0].Type is not INamedTypeSymbol stateType
            || method.Parameters[1].Type is not INamedTypeSymbol actionType)
        {
            // ZFLUX003 — at the method.
            return new ReducerCandidate(null, DiagnosticInfo.Create(
                Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                SourceLocations.Of(method),
                methodDisplay));
        }

        return new ReducerCandidate(
            new ReducerInfo(
                method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                method.Name,
                stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                stateType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                actionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                methodDisplay,
                LocationInfo.From(SourceLocations.Of(method)),
                LocationInfo.From(SourceLocations.Of(method.Parameters[0]))),
            null);
    }

    /// <summary>
    /// ZFLUX002: within the same owning type, no two reducers may share (state, action).
    /// Reported once per clash, at the later reducer, with the others as additional locations.
    /// </summary>
    public static IEnumerable<DiagnosticInfo> FindDuplicates(IEnumerable<ReducerInfo> reducers)
    {
        var groups = new Dictionary<(string Owning, string State, string Action), List<ReducerInfo>>();
        var order = new List<List<ReducerInfo>>();
        foreach (var r in reducers)
        {
            var key = (r.OwningTypeFqn, r.StateTypeFqn, r.ActionTypeFqn);
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

            var located = new List<LocationInfo?>(group.Count);
            foreach (var r in group) located.Add(r.MethodLocation);
            located.Sort(LocationInfo.Compare);

            var later = located[located.Count - 1];
            located.RemoveAt(located.Count - 1);

            var additional = ImmutableArray.CreateBuilder<LocationInfo>(located.Count);
            foreach (var location in located)
            {
                if (location is not null) additional.Add(location);
            }

            yield return DiagnosticInfo.Create(
                Diagnostics.ZFLUX002_DuplicateReducerInFeature,
                later,
                new EquatableArray<LocationInfo>(additional.ToImmutable()),
                group[0].StateTypeFqn,
                group[0].ActionTypeFqn);
        }
    }
}
