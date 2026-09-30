using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// A diagnostic found while building a model, reported when the sources are emitted.
/// </summary>
/// <remarks>
/// A <see cref="Diagnostic"/> compares its message arguments by reference, so one rebuilt from
/// the same source never compares equal to the last, and a model holding it is never served from
/// the cache. Here the arguments are strings compared by value, and the locations are
/// <see cref="LocationInfo"/>s.
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<LocationInfo> AdditionalLocations,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs) =>
        Create(descriptor, LocationInfo.From(location), EquatableArray<LocationInfo>.Empty, messageArgs);

    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        LocationInfo? location,
        EquatableArray<LocationInfo> additionalLocations,
        params string[] messageArgs) =>
        new(descriptor, location, additionalLocations, new EquatableArray<string>(ImmutableArray.Create(messageArgs)));

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < args.Length; i++)
            args[i] = MessageArgs[i];

        var additional = new List<Location>(AdditionalLocations.Count);
        foreach (var location in AdditionalLocations)
            additional.Add(location.ToLocation());

        return Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            additional,
            args);
    }
}
