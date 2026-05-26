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
    /// Returns a <see cref="Diagnostic"/> (ZFLUX004) when the factory is missing or has the
    /// wrong signature, or <see langword="null"/> when the contract is satisfied.
    /// </summary>
    /// <param name="feature">The <c>[Feature]</c>-decorated type that declared the factory name.</param>
    /// <param name="factoryName">The name supplied to <c>[Feature(InitialState = "Name")]</c>.</param>
    /// <param name="compilation">Compilation used to resolve <see cref="System.IServiceProvider"/>.</param>
    public static Diagnostic? Validate(INamedTypeSymbol feature, string factoryName, Compilation compilation)
    {
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
            return Diagnostic.Create(
                Diagnostics.ZFLUX004_InitialStateFactoryInvalid,
                feature.Locations.Length > 0 ? feature.Locations[0] : Location.None,
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
            return Diagnostic.Create(
                Diagnostics.ZFLUX004_InitialStateFactoryInvalid,
                candidate.Locations.Length > 0 ? candidate.Locations[0] : Location.None,
                featureFqn,
                factoryName);
        }

        return null;
    }
}
