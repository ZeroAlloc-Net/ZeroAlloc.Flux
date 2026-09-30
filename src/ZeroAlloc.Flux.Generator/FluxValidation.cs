using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Joins the discovered features and reducers: gathers the diagnostics each model found when it
/// was built, and runs the checks that need the whole set, ZFLUX001 and ZFLUX002.
/// </summary>
internal static class FluxValidation
{
    /// <summary>The validated model and every ZFLUX diagnostic, in report order.</summary>
    /// <param name="Features">Every discovered feature, including those with diagnostics.</param>
    /// <param name="Reducers">The reducers whose state is a known feature.</param>
    /// <param name="Diagnostics">The diagnostics to report.</param>
    public sealed record Result(
        ImmutableArray<FeatureInfo> Features,
        ImmutableArray<ReducerInfo> Reducers,
        ImmutableArray<DiagnosticInfo> Diagnostics);

    public static Result Validate(
        ImmutableArray<FeatureInfo?> rawFeatures,
        ImmutableArray<ReducerCandidate?> rawReducers)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // ZFLUX005 and ZFLUX004, found per feature when its model was built.
        var features = ImmutableArray.CreateBuilder<FeatureInfo>();
        var featureNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var f in rawFeatures)
        {
            if (f is null) continue;
            features.Add(f);
            featureNames.Add(f.FullyQualifiedName);
            foreach (var d in f.Diagnostics) diagnostics.Add(d);
        }

        // ZFLUX003, found per reducer when its model was built. Then ZFLUX001: the reducer's
        // state must be a known [Feature]. At the state parameter.
        var reducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        foreach (var r in rawReducers)
        {
            if (r is null) continue;
            if (r.Info is null)
            {
                if (r.SignatureError is not null) diagnostics.Add(r.SignatureError);
                continue;
            }

            if (!featureNames.Contains(r.Info.StateTypeFqn))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.ZFLUX001_ReducerOnNonFeatureState,
                    r.Info.StateParameterLocation,
                    EquatableArray<LocationInfo>.Empty,
                    r.Info.MethodDisplay,
                    r.Info.StateTypeFqn));
                continue;
            }

            reducers.Add(r.Info);
        }

        // ZFLUX002 — duplicate (owning, state, action) within a feature. At the later reducer.
        diagnostics.AddRange(ReducerDiscovery.FindDuplicates(reducers));

        return new Result(features.ToImmutable(), reducers.ToImmutable(), diagnostics.ToImmutable());
    }
}
