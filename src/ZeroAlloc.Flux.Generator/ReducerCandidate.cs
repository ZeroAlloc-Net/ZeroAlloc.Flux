using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// A <c>[Reducer]</c>-decorated method found by <see cref="ReducerDiscovery.Transform"/>.
/// </summary>
/// <param name="Method">The decorated method, where ZFLUX003 is reported.</param>
/// <param name="Info">
/// The reducer, or <see langword="null"/> when the method has no state and action parameter of a
/// named type, which fails ZFLUX003.
/// </param>
internal sealed record ReducerCandidate(IMethodSymbol Method, ReducerInfo? Info);
