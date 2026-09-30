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
/// <param name="HintName">
/// The hint name of the feature's generated store file, from <see cref="HintNames.ForFeature"/>.
/// </param>
/// <param name="StoreClassName">
/// The name of the feature's generated store class, from
/// <see cref="StoreEmitter.GetStoreClassName"/>.
/// </param>
/// <param name="CanGenerate">
/// <see langword="false"/> when no store can be generated for the feature: it is generic or nested
/// in a generic type, <c>ZFLUX006</c>, or not accessible to the rest of its assembly,
/// <c>ZFLUX007</c>.
/// </param>
/// <param name="Location">The feature's identifier, where <c>ZFLUX008</c> is reported.</param>
/// <param name="Diagnostics">
/// ZFLUX004 to ZFLUX007 for this feature, found when it was built.
/// </param>
internal sealed record FeatureInfo(
    string FullyQualifiedName,
    string Name,
    bool IsStruct,
    bool IsPartial,
    string? InitialStateFactoryName,
    string HintName,
    string StoreClassName,
    bool CanGenerate,
    LocationInfo? Location,
    EquatableArray<DiagnosticInfo> Diagnostics);
