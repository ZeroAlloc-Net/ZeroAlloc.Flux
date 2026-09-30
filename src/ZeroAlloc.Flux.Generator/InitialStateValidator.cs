using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Validates the <c>[Feature(InitialState = "Name")]</c> factory contract: a method named
/// <c>Name</c> must exist on the feature type with signature
/// <c>public static TFeature Name(IServiceProvider)</c>. Mismatches surface as
/// <c>ZFLUX004</c>.
/// </summary>
internal static class InitialStateValidator
{
    /// <summary>
    /// Returns ZFLUX004 when the factory is missing or has the wrong signature, or
    /// <see langword="null"/> when the contract is satisfied or no factory is named.
    /// </summary>
    /// <remarks>
    /// A missing factory is reported at the <c>InitialState</c> argument that names it, and a
    /// factory with the wrong signature at the factory method.
    /// </remarks>
    /// <param name="feature">The <c>[Feature]</c>-decorated type.</param>
    /// <param name="factoryName">The factory name the attribute declared, if any.</param>
    /// <param name="initialStateLocation">The <c>InitialState</c> argument, if in source.</param>
    /// <param name="compilation">Compilation used to resolve <see cref="System.IServiceProvider"/>.</param>
    public static DiagnosticInfo? Validate(
        INamedTypeSymbol feature,
        string? factoryName,
        Location? initialStateLocation,
        Compilation compilation)
    {
        if (factoryName is null) return null;
        var serviceProvider = compilation.GetTypeByMetadataName("System.IServiceProvider");
        var featureFqn = feature.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        IMethodSymbol? candidate = null;
        foreach (var member in feature.GetMembers(factoryName))
        {
            if (member is IMethodSymbol m)
            {
                candidate = m;
                break;
            }
        }

        if (candidate is null)
        {
            return DiagnosticInfo.Create(
                Diagnostics.ZFLUX004_InitialStateFactoryInvalid,
                initialStateLocation ?? SourceLocations.Of(feature),
                featureFqn,
                factoryName);
        }

        var signatureOk =
            candidate.DeclaredAccessibility == Accessibility.Public &&
            candidate.IsStatic &&
            SymbolEqualityComparer.Default.Equals(candidate.ReturnType, feature) &&
            candidate.Parameters.Length == 1 &&
            serviceProvider is not null &&
            SymbolEqualityComparer.Default.Equals(candidate.Parameters[0].Type, serviceProvider);

        if (!signatureOk)
        {
            return DiagnosticInfo.Create(
                Diagnostics.ZFLUX004_InitialStateFactoryInvalid,
                SourceLocations.Of(candidate),
                featureFqn,
                factoryName);
        }

        return null;
    }
}
