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
///   <item>Two <c>ForAttributeWithMetadataName</c> branches build features and reducers via
///         <see cref="FeatureDiscovery.Transform"/> and <see cref="ReducerDiscovery.Transform"/>.
///         They read the symbols and return value-equal models with no <see cref="ISymbol"/>
///         or <see cref="Compilation"/>, carrying the diagnostics that concern one model alone
///         (ZFLUX003 to ZFLUX007) as <see cref="DiagnosticInfo"/>.</item>
///   <item>Both branches <c>.Collect()</c> into arrays and are <c>.Combine()</c>d.</item>
///   <item>A single <c>RegisterSourceOutput</c> stage runs <see cref="FluxValidation"/>, which
///         adds the cross-model checks ZFLUX001 / ZFLUX002 / ZFLUX008, reports every diagnostic,
///         and emits Store_*, FluxDispatcher, and FluxServiceCollectionExtensions sources for
///         every feature a store can be generated for.</item>
/// </list>
/// </para>
/// <para>
/// Because the models compare by value, an edit that leaves every model equal, such as an edit
/// to a file with no <c>[Feature]</c> or <c>[Reducer]</c>, finds every step cached and emits
/// nothing new.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FluxGenerator : IIncrementalGenerator
{
    /// <summary>Names of the tracked pipeline steps, for incrementality tests.</summary>
    internal static class TrackingNames
    {
        public const string Features = "Features";
        public const string Reducers = "Reducers";
        public const string CollectedFeatures = "CollectedFeatures";
        public const string CollectedReducers = "CollectedReducers";
        public const string Combined = "Combined";

        public static readonly string[] All =
        {
            Features, Reducers, CollectedFeatures, CollectedReducers, Combined,
        };
    }

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var features = context.SyntaxProvider.ForAttributeWithMetadataName(
                FeatureDiscovery.FeatureAttributeFullName,
                predicate: static (n, _) => n is TypeDeclarationSyntax,
                transform: static (ctx, ct) => FeatureDiscovery.Transform(ctx, ct))
            .WithTrackingName(TrackingNames.Features)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedFeatures);

        var reducers = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReducerDiscovery.ReducerAttributeFullName,
                predicate: static (n, _) => n is MethodDeclarationSyntax,
                transform: static (ctx, ct) => ReducerDiscovery.Transform(ctx, ct))
            .WithTrackingName(TrackingNames.Reducers)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedReducers);

        var combined = features.Combine(reducers).WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static void Execute(
        SourceProductionContext spc,
        ImmutableArray<FeatureInfo?> rawFeatures,
        ImmutableArray<ReducerCandidate?> rawReducers)
    {
        var model = FluxValidation.Validate(rawFeatures, rawReducers);
        foreach (var diagnostic in model.Diagnostics)
        {
            spc.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (model.Features.IsEmpty) return;

        // Per-feature Store emit. We emit even for non-partial features so the generator's
        // output remains observable; the C# compiler will subsequently surface the partial
        // mismatch as a normal diagnostic alongside ZFLUX005.
        foreach (var feature in model.Features)
        {
            var storeSrc = StoreEmitter.Emit(feature);
            spc.AddSource(feature.HintName, SourceText.From(storeSrc, Encoding.UTF8));
        }

        var dispatcherSrc = DispatcherEmitter.Emit(model.Features, model.Reducers);
        spc.AddSource("FluxDispatcher.g.cs", SourceText.From(dispatcherSrc, Encoding.UTF8));

        var diSrc = ServiceCollectionExtensionsEmitter.Emit(model.Features);
        spc.AddSource("FluxServiceCollectionExtensions.g.cs", SourceText.From(diSrc, Encoding.UTF8));
    }
}
