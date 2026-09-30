namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// A <c>[Reducer]</c>-decorated method found by <see cref="ReducerDiscovery.Transform"/>: either
/// a reducer with a valid signature, or the ZFLUX003 its signature fails.
/// </summary>
/// <param name="Info">The reducer, or <see langword="null"/> when its signature is invalid.</param>
/// <param name="SignatureError">ZFLUX003 when <paramref name="Info"/> is <see langword="null"/>.</param>
internal sealed record ReducerCandidate(ReducerInfo? Info, DiagnosticInfo? SignatureError);
