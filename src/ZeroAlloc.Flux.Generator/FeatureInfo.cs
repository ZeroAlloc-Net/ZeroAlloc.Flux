using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Metadata captured for each <c>[Feature]</c>-decorated type discovered by
/// <see cref="FeatureDiscovery"/>. Carries everything downstream emit + validation
/// passes need without having to re-walk the symbol table.
/// </summary>
/// <param name="TypeSymbol">The discovered <see cref="INamedTypeSymbol"/> itself.</param>
/// <param name="FullyQualifiedName">
/// Fully-qualified type name including <c>global::</c> prefix, e.g.
/// <c>global::MyNamespace.CounterState</c>.
/// </param>
/// <param name="IsStruct"><see langword="true"/> when the feature is a record struct.</param>
/// <param name="IsPartial">
/// <see langword="false"/> when none of the type's syntax declarations carry the
/// <c>partial</c> modifier — fires <c>ZFLUX005</c> in that case.
/// </param>
/// <param name="InitialStateFactoryName">
/// The value of <c>[Feature(InitialState = "Name")]</c>, or <see langword="null"/> when
/// the named-arg isn't supplied. Validated by <see cref="InitialStateValidator"/>.
/// </param>
internal sealed record FeatureInfo(
    INamedTypeSymbol TypeSymbol,
    string FullyQualifiedName,
    bool IsStruct,
    bool IsPartial,
    string? InitialStateFactoryName);
