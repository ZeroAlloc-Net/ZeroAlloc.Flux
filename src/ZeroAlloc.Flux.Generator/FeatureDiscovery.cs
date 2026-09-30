using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Builds a <see cref="FeatureInfo"/> for each <c>[Feature]</c>-decorated type, with the
/// ZFLUX004 to ZFLUX007 diagnostics that concern it alone.
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

        // A store is generated only for a closed type that the generated code can name.
        var canGenerate = true;
        if (IsGenericOrInGenericType(type))
        {
            // ZFLUX006 — at the feature type.
            canGenerate = false;
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.ZFLUX006_GenericFeature,
                SourceLocations.Of(type),
                fqn));
        }
        else if (!IsAccessibleToAssembly(type))
        {
            // ZFLUX007 — at the feature type.
            canGenerate = false;
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.ZFLUX007_FeatureNotAccessible,
                SourceLocations.Of(type),
                fqn));
        }

        return new FeatureInfo(
            fqn,
            type.Name,
            isStruct,
            isPartial,
            initialState,
            HintNames.ForFeature(type),
            StoreEmitter.GetStoreClassName(type),
            canGenerate,
            LocationInfo.From(SourceLocations.Of(type)),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    /// <summary>
    /// The generator only sees a generic feature's definition, never the type arguments it is
    /// used with, so it has no closed type to make a store for.
    /// </summary>
    private static bool IsGenericOrInGenericType(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.Arity > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// The generated store, dispatcher and registrations live in their own namespace, so they
    /// can name the feature only if it and every type containing it are public, internal or
    /// protected internal, and it is not a file-local type.
    /// </summary>
    private static bool IsAccessibleToAssembly(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return false;
            if (t.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }
        return true;
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
