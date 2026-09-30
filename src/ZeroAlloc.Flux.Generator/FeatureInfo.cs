namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// A <c>[Feature]</c>-decorated type found by <see cref="FeatureDiscovery"/>. Value data only, no
/// symbols, so it compares equal across compilations and the pipeline can cache it.
/// </summary>
/// <param name="FullyQualifiedName">
/// Fully-qualified type name including <c>global::</c> prefix, e.g.
/// <c>global::MyNamespace.CounterState</c>.
/// </param>
/// <param name="Name">The bare type name, e.g. <c>CounterState</c>.</param>
/// <param name="IsStruct"><see langword="true"/> when the feature is a record struct.</param>
/// <param name="IsPartial">
/// <see langword="false"/> when none of the type's syntax declarations carry the
/// <c>partial</c> modifier — fires <c>ZFLUX005</c> in that case.
/// </param>
/// <param name="InitialStateFactoryName">
/// The value of <c>[Feature(InitialState = "Name")]</c>, or <see langword="null"/> when
/// the named-arg isn't supplied. Validated by <see cref="InitialStateValidator"/>.
/// </param>
/// <param name="Diagnostics">ZFLUX005 and ZFLUX004 for this feature, found when it was built.</param>
internal sealed record FeatureInfo(
    string FullyQualifiedName,
    string Name,
    bool IsStruct,
    bool IsPartial,
    string? InitialStateFactoryName,
    EquatableArray<DiagnosticInfo> Diagnostics);
