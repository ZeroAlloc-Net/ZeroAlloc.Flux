using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Builds a <see cref="FeatureInfo"/> for each <c>[Feature]</c>-decorated type, with the
/// ZFLUX005 and ZFLUX004 diagnostics that concern it alone.
/// </summary>
internal static class FeatureDiscovery
{
    /// <summary>Metadata name used by <c>ForAttributeWithMetadataName</c>.</summary>
    public const string FeatureAttributeFullName = "ZeroAlloc.Flux.FeatureAttribute";

    /// <summary>
    /// Transform for the <c>ForAttributeWithMetadataName</c> pipeline branch. Returns
    /// <see langword="null"/> if the target symbol isn't an <see cref="INamedTypeSymbol"/>.
    /// </summary>
    /// <remarks>
    /// The symbols are read here and projected into value data, so the model compares equal
    /// across compilations. The InitialState check resolves <see cref="System.IServiceProvider"/>
    /// through the semantic model's compilation, so the pipeline needs no
    /// <c>CompilationProvider</c>.
    /// </remarks>
    public static FeatureInfo? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        ct.ThrowIfCancellationRequested();

        string? initialState = null;
        Location? initialStateLocation = null;
        foreach (var attr in ctx.Attributes)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (string.Equals(kvp.Key, "InitialState", System.StringComparison.Ordinal)
                    && kvp.Value.Value is string s
                    && !string.IsNullOrEmpty(s))
                {
                    initialState = s;
                    initialStateLocation = NamedArgumentLocation(attr, "InitialState");
                    break;
                }
            }
            if (initialState is not null) break;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isStruct = type.TypeKind == TypeKind.Struct;
        var isPartial = IsDeclaredPartial(type);

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        if (!isPartial)
        {
            // ZFLUX005 — at the feature type.
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.ZFLUX005_FeatureNotPartial,
                SourceLocations.Of(type),
                fqn));
        }

        var initialStateError = InitialStateValidator.Validate(
            type, initialState, initialStateLocation, ctx.SemanticModel.Compilation);
        if (initialStateError is not null) diagnostics.Add(initialStateError);

        return new FeatureInfo(
            fqn,
            type.Name,
            isStruct,
            isPartial,
            initialState,
            HintNames.ForFeature(type),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static Location? NamedArgumentLocation(AttributeData attribute, string name)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax
            || syntax.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, name, System.StringComparison.Ordinal))
            {
                return argument.GetLocation();
            }
        }

        return null;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol type)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax decl)
            {
                foreach (var modifier in decl.Modifiers)
                {
                    if (modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
