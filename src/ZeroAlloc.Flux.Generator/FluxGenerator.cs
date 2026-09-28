using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Source generator for ZeroAlloc.Flux: discovers <c>[Feature]</c> types and their
/// <c>[Reducer]</c> methods, then emits per-feature <c>IStore&lt;TState&gt;</c> wiring, a
/// global <c>FluxDispatcher</c>, and the <c>AddZeroAllocFlux</c> DI extension.
/// </summary>
/// <remarks>
/// <para>The <see cref="Initialize"/> pipeline shape:
/// <list type="number">
///   <item>Two <c>ForAttributeWithMetadataName</c> branches discover features +
///         reducers via <see cref="FeatureDiscovery.Transform"/> and
///         <see cref="ReducerDiscovery.Transform"/>.</item>
///   <item>Both branches <c>.Collect()</c> into immutable arrays, then are
///         <c>.Combine()</c>d with each other and with
///         <see cref="IncrementalGeneratorInitializationContext.CompilationProvider"/>
///         (the latter is needed so ZFLUX001 can cross-check reducer state types
///         against the known feature set and so <see cref="InitialStateValidator"/>
///         can resolve <c>System.IServiceProvider</c>).</item>
///   <item>A single <c>RegisterSourceOutput</c> stage fires diagnostics
///         (ZFLUX001 / ZFLUX002 / ZFLUX004 / ZFLUX005) and emits Store_*,
///         FluxDispatcher, and FluxServiceCollectionExtensions sources.</item>
/// </list>
/// </para>
/// </remarks>
// TODO(perf, ZeroAlloc.Flux#132): make FeatureInfo/ReducerInfo cache-hygienic — currently holds
// ISymbol refs which break cross-compilation equality for the incremental cache.
// The generator therefore re-runs on every change instead of incrementally caching;
// costs build perf but not correctness. Fix is to project the transform output into
// a value-equal intermediate (strings + flags) and resolve symbols downstream. Its
// diagnostic locations then need to be cache-safe, see SourceLocations.
[Generator(LanguageNames.CSharp)]
public sealed class FluxGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var features = context.SyntaxProvider.ForAttributeWithMetadataName(
                FeatureDiscovery.FeatureAttributeFullName,
                predicate: static (n, _) => n is TypeDeclarationSyntax,
                transform: static (ctx, ct) => FeatureDiscovery.Transform(ctx, ct))
            .Collect();

        var reducers = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReducerDiscovery.ReducerAttributeFullName,
                predicate: static (n, _) => n is MethodDeclarationSyntax,
                transform: static (ctx, ct) => ReducerDiscovery.Transform(ctx, ct))
            .Collect();

        var combined = features.Combine(reducers).Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, triple) =>
        {
            var ((featureArr, reducerArr), compilation) = triple;
            Execute(spc, featureArr, reducerArr, compilation);
        });
    }

    private static void Execute(
        SourceProductionContext spc,
        ImmutableArray<FeatureInfo?> rawFeatures,
        ImmutableArray<ReducerCandidate?> rawReducers,
        Compilation compilation)
    {
        // Drop nulls from Transform's filter step.
        var features = ImmutableArray.CreateBuilder<FeatureInfo>();
        foreach (var f in rawFeatures) if (f is not null) features.Add(f);

        // ZFLUX003 — a [Reducer] without a state and an action parameter of a named type has no
        // ReducerInfo. It is reported here, at the method, rather than dropped without a word.
        var reducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        foreach (var r in rawReducers)
        {
            if (r is null) continue;
            if (r.Info is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                    SourceLocations.Of(r.Method),
                    r.Method.ToDisplayString()));
                continue;
            }
            reducers.Add(r.Info);
        }

        // ZFLUX005 — non-partial features. At the feature type.
        foreach (var f in features)
        {
            if (!f.IsPartial)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ZFLUX005_FeatureNotPartial,
                    SourceLocations.Of(f.TypeSymbol),
                    f.FullyQualifiedName));
            }
        }

        // ZFLUX004 — InitialState factory validation (needs Compilation for IServiceProvider).
        foreach (var f in features)
        {
            var diag = InitialStateValidator.Validate(f, compilation);
            if (diag is not null) spc.ReportDiagnostic(diag);
        }

        // ZFLUX003 — the rest of the signature rule: public, static, and returning the state
        // type, mirroring DiscoverFromCompilation. At the method.
        var validReducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        foreach (var r in reducers)
        {
            var m = r.MethodSymbol;
            var signatureOk =
                m.DeclaredAccessibility == Accessibility.Public &&
                m.IsStatic &&
                m.Parameters.Length >= 2 &&
                SymbolEqualityComparer.Default.Equals(m.ReturnType, m.Parameters[0].Type);
            if (!signatureOk)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ZFLUX003_ReducerSignatureInvalid,
                    SourceLocations.Of(m),
                    m.ToDisplayString()));
                continue;
            }
            validReducers.Add(r);
        }

        // ZFLUX001 — reducer state must be a known [Feature]. At the state parameter.
        var featureSet = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var f in features) featureSet.Add(f.TypeSymbol);

        var crossCheckedReducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        foreach (var r in validReducers)
        {
            if (!featureSet.Contains(r.StateType))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ZFLUX001_ReducerOnNonFeatureState,
                    SourceLocations.Of(r.MethodSymbol.Parameters[0]),
                    r.MethodSymbol.ToDisplayString(),
                    r.StateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                continue;
            }
            crossCheckedReducers.Add(r);
        }

        // ZFLUX002 — duplicate (Owning, State, Action) within a feature. At the later reducer.
        foreach (var diag in ReducerDiscovery.FindDuplicates(crossCheckedReducers))
        {
            spc.ReportDiagnostic(diag);
        }

        if (features.Count == 0) return;

        // Per-feature Store emit. We emit even for non-partial features so the generator's
        // output remains observable; the C# compiler will subsequently surface the partial
        // mismatch as a normal diagnostic alongside ZFLUX005.
        var featuresImmutable = features.ToImmutable();
        var reducersImmutable = crossCheckedReducers.ToImmutable();

        foreach (var feature in featuresImmutable)
        {
            var storeSrc = StoreEmitter.Emit(feature);
            spc.AddSource(
                $"{StoreEmitter.GetStoreClassName(feature)}.g.cs",
                SourceText.From(storeSrc, Encoding.UTF8));
        }

        var dispatcherSrc = DispatcherEmitter.Emit(featuresImmutable, reducersImmutable);
        spc.AddSource("FluxDispatcher.g.cs", SourceText.From(dispatcherSrc, Encoding.UTF8));

        var diSrc = ServiceCollectionExtensionsEmitter.Emit(featuresImmutable);
        spc.AddSource("FluxServiceCollectionExtensions.g.cs", SourceText.From(diSrc, Encoding.UTF8));
    }
}
