using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Metadata captured for each <c>[Reducer]</c>-decorated method discovered by
/// <see cref="ReducerDiscovery"/>. Validated signature: <c>public static TState
/// Method(TState, TAction)</c> where <c>TState</c> is a <c>[Feature]</c> type.
/// </summary>
/// <param name="MethodSymbol">The discovered reducer method symbol.</param>
/// <param name="OwningType">The containing class — its full namespace path is needed at emit.</param>
/// <param name="StateType">First parameter type; must match a known <see cref="FeatureInfo"/>.</param>
/// <param name="ActionType">Second parameter type; the action payload.</param>
/// <param name="MethodName">Bare method name (no qualification) — emit-time identifier.</param>
/// <param name="OwningTypeFqn">Fully-qualified <c>OwningType</c> name with <c>global::</c> prefix.</param>
internal sealed record ReducerInfo(
    IMethodSymbol MethodSymbol,
    INamedTypeSymbol OwningType,
    INamedTypeSymbol StateType,
    INamedTypeSymbol ActionType,
    string MethodName,
    string OwningTypeFqn);
