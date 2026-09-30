namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// A <c>[Reducer]</c> method with a valid signature, <c>public static TState Method(TState,
/// TAction)</c>. Value data only, no symbols, so it compares equal across compilations and the
/// pipeline can cache it. Whether <c>TState</c> is a <c>[Feature]</c> is checked later, against
/// the full feature set.
/// </summary>
/// <param name="OwningTypeFqn">Fully-qualified containing type name with <c>global::</c> prefix.</param>
/// <param name="MethodName">Bare method name (no qualification) — emit-time identifier.</param>
/// <param name="StateTypeFqn">Fully-qualified first parameter type; must name a known feature.</param>
/// <param name="ActionTypeFqn">Fully-qualified second parameter type; the action payload.</param>
/// <param name="MethodDisplay">The method as the diagnostics name it.</param>
/// <param name="MethodLocation">The method's identifier, where ZFLUX002 is reported.</param>
/// <param name="StateParameterLocation">The state parameter, where ZFLUX001 is reported.</param>
internal sealed record ReducerInfo(
    string OwningTypeFqn,
    string MethodName,
    string StateTypeFqn,
    string ActionTypeFqn,
    string MethodDisplay,
    LocationInfo? MethodLocation,
    LocationInfo? StateParameterLocation);
