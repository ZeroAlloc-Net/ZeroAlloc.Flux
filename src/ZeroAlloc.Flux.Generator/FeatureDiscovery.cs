using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Discovers <c>[Feature]</c>-decorated types and produces <see cref="FeatureInfo"/>
/// records for downstream emit. Exposes two entry points:
/// <list type="bullet">
///   <item><see cref="Transform"/> — pipeline hook for
///         <c>SyntaxProvider.ForAttributeWithMetadataName</c> (wired in Task 2.9).</item>
///   <item><see cref="DiscoverFromCompilation"/> — direct compilation walk used by unit tests
///         and as a pre-pipeline shim; survives Task 2.9.</item>
/// </list>
/// </summary>
internal static class FeatureDiscovery
{
    /// <summary>Metadata name used by <c>ForAttributeWithMetadataName</c>.</summary>
    public const string FeatureAttributeFullName = "ZeroAlloc.Flux.FeatureAttribute";

    /// <summary>
    /// Transform delegate body for the <c>IIncrementalGenerator</c> pipeline. Builds a
    /// <see cref="FeatureInfo"/> from a single attribute match; returns <see langword="null"/>
    /// if the target symbol isn't an <see cref="INamedTypeSymbol"/>.
    /// </summary>
    /// <remarks>
    /// Diagnostics (e.g. ZFLUX005) aren't emitted here — the caller decides where to
    /// surface them. <see cref="DiscoverFromCompilation"/> emits them inline; the pipeline
    /// path emits them from a downstream <c>RegisterSourceOutput</c> stage.
    /// </remarks>
    public static FeatureInfo? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        return BuildFeatureInfo(type, ctx.Attributes);
    }

    /// <summary>
    /// Walks the supplied <paramref name="compilation"/> to find every <c>[Feature]</c>-decorated
    /// type. Returns the discovered features plus any validation diagnostics
    /// (ZFLUX005 for non-partial, ZFLUX004 for invalid InitialState factories).
    /// </summary>
    /// <remarks>
    /// This is the test-friendly entry point used by <c>FeatureDiscoveryTests</c> and
    /// <c>InitialStateValidatorTests</c>. The pipeline path in Task 2.9 uses
    /// <see cref="Transform"/> instead.
    /// </remarks>
    public static (ImmutableArray<FeatureInfo> Features, ImmutableArray<Diagnostic> Diagnostics)
        DiscoverFromCompilation(Compilation compilation)
    {
        var featureAttr = compilation.GetTypeByMetadataName(FeatureAttributeFullName);
        if (featureAttr is null)
        {
            return (ImmutableArray<FeatureInfo>.Empty, ImmutableArray<Diagnostic>.Empty);
        }

        var features = ImmutableArray.CreateBuilder<FeatureInfo>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        WalkNamespace(compilation.SourceModule.GlobalNamespace, featureAttr, features);

        foreach (var info in features)
        {
            if (!info.IsPartial)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.ZFLUX005_FeatureNotPartial,
                    info.TypeSymbol.Locations.Length > 0 ? info.TypeSymbol.Locations[0] : Location.None,
                    info.FullyQualifiedName));
            }

            if (info.InitialStateFactoryName is not null)
            {
                var diag = InitialStateValidator.Validate(info.TypeSymbol, info.InitialStateFactoryName, compilation);
                if (diag is not null) diagnostics.Add(diag);
            }
        }

        return (features.ToImmutable(), diagnostics.ToImmutable());
    }

    private static void WalkNamespace(
        INamespaceOrTypeSymbol root,
        INamedTypeSymbol featureAttr,
        ImmutableArray<FeatureInfo>.Builder sink)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current is INamedTypeSymbol currentType)
            {
                ProcessType(currentType, featureAttr, sink);
            }

            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    stack.Push(ns);
                }
                else if (member is INamedTypeSymbol type)
                {
                    foreach (var nested in type.GetTypeMembers()) stack.Push(nested);
                    ProcessType(type, featureAttr, sink);
                }
            }
        }
    }

    private static void ProcessType(
        INamedTypeSymbol type,
        INamedTypeSymbol featureAttr,
        ImmutableArray<FeatureInfo>.Builder sink)
    {
        var attrs = type.GetAttributes();
        AttributeData? featureAttribute = null;
        foreach (var a in attrs)
        {
            if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, featureAttr))
            {
                featureAttribute = a;
                break;
            }
        }
        if (featureAttribute is null) return;

        var info = BuildFeatureInfo(type, ImmutableArray.Create(featureAttribute));
        if (info is not null) sink.Add(info);
    }

    private static FeatureInfo BuildFeatureInfo(INamedTypeSymbol type, ImmutableArray<AttributeData> featureAttributes)
    {
        string? initialState = null;
        foreach (var attr in featureAttributes)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key == "InitialState"
                    && kvp.Value.Value is string s
                    && !string.IsNullOrEmpty(s))
                {
                    initialState = s;
                    break;
                }
            }
            if (initialState is not null) break;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isStruct = type.TypeKind == TypeKind.Struct;
        var isPartial = IsDeclaredPartial(type);

        return new FeatureInfo(type, fqn, isStruct, isPartial, initialState);
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
