using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Joins the discovered features and reducers: gathers the diagnostics each model found when it
/// was built, and runs the checks that need the whole set, ZFLUX001, ZFLUX002 and ZFLUX008.
/// </summary>
internal static class FluxValidation
{
    /// <summary>The validated model and every ZFLUX diagnostic, in report order.</summary>
    /// <param name="Features">
    /// The features to generate a store for: every discovered feature except those that report
    /// ZFLUX006, ZFLUX007 or ZFLUX008. A feature with another diagnostic is still generated.
    /// </param>
    /// <param name="Reducers">The reducers whose state is one of <paramref name="Features"/>.</param>
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

        // ZFLUX004 to ZFLUX007, found per feature when its model was built.
        var candidates = new List<FeatureInfo>();
        var featureNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var f in rawFeatures)
        {
            // A type whose partial declarations each carry [Feature] is found once per
            // declaration, with the same model; the compiler reports the repeated attribute.
            if (f is null || !featureNames.Add(f.FullyQualifiedName)) continue;
            foreach (var d in f.Diagnostics) diagnostics.Add(d);
            if (f.CanGenerate) candidates.Add(f);
        }

        // ZFLUX008 — features whose store files would differ only in case. At the later feature.
        var skipped = FindCaseCollisions(candidates, diagnostics);
        var features = ImmutableArray.CreateBuilder<FeatureInfo>();
        var generatedNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var f in candidates)
        {
            if (skipped.Contains(f.FullyQualifiedName)) continue;
            features.Add(f);
            generatedNames.Add(f.FullyQualifiedName);
        }

        // ZFLUX003, found per reducer when its model was built. Then ZFLUX001: the reducer's
        // state must be a known [Feature]. At the state parameter. A reducer on a feature that
        // is not generated is left out silently: that feature already reports why.
        var reducers = ImmutableArray.CreateBuilder<ReducerInfo>();
        foreach (var r in rawReducers)
        {
            if (r is null) continue;
            if (r.Info is null)
            {
                if (r.SignatureError is not null) diagnostics.Add(r.SignatureError);
                continue;
            }

            if (generatedNames.Contains(r.Info.StateTypeFqn))
            {
                reducers.Add(r.Info);
                continue;
            }

            if (featureNames.Contains(r.Info.StateTypeFqn) || featureNames.Contains(r.Info.StateTypeDefinitionFqn))
            {
                continue;
            }

            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.ZFLUX001_ReducerOnNonFeatureState,
                r.Info.StateParameterLocation,
                EquatableArray<LocationInfo>.Empty,
                r.Info.MethodDisplay,
                r.Info.StateTypeFqn));
        }

        // ZFLUX002 — duplicate (owning, state, action) within a feature. At the later reducer.
        diagnostics.AddRange(ReducerDiscovery.FindDuplicates(reducers));

        return new Result(features.ToImmutable(), reducers.ToImmutable(), diagnostics.ToImmutable());
    }

    /// <summary>
    /// Roslyn compares hint names ignoring case, so two features whose hint names differ only in
    /// case cannot both have a store file. In each such group the feature declared first, by file
    /// path and then position, is kept; every later one reports ZFLUX008 at its name, with the
    /// kept feature as the additional location. The order of <paramref name="features"/> does not
    /// change which one is kept.
    /// </summary>
    /// <returns>The fully qualified names of the features to leave out.</returns>
    private static HashSet<string> FindCaseCollisions(
        List<FeatureInfo> features,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var skipped = new HashSet<string>(System.StringComparer.Ordinal);
        var groups = new Dictionary<string, List<FeatureInfo>>(System.StringComparer.OrdinalIgnoreCase);
        var order = new List<List<FeatureInfo>>();
        foreach (var f in features)
        {
            if (!groups.TryGetValue(f.HintName, out var group))
            {
                group = new List<FeatureInfo>();
                groups.Add(f.HintName, group);
                order.Add(group);
            }
            group.Add(f);
        }

        foreach (var group in order)
        {
            if (group.Count < 2) continue;

            group.Sort(static (x, y) => LocationInfo.Compare(x.Location, y.Location));
            var kept = group[0];
            var keptLocation = kept.Location is { } location
                ? new EquatableArray<LocationInfo>(ImmutableArray.Create(location))
                : EquatableArray<LocationInfo>.Empty;

            for (var i = 1; i < group.Count; i++)
            {
                skipped.Add(group[i].FullyQualifiedName);
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.ZFLUX008_FeatureNamesDifferOnlyInCase,
                    group[i].Location,
                    keptLocation,
                    group[i].FullyQualifiedName,
                    kept.FullyQualifiedName));
            }
        }

        return skipped;
    }
}
