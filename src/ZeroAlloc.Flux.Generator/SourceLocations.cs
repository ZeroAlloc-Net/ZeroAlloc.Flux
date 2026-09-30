using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Where the ZFLUX diagnostics are reported.
/// </summary>
/// <remarks>
/// A symbol's own location is a source location in one of the compilation's trees. The compiler
/// applies <c>#pragma warning disable</c> to such a location, and the IDE can navigate to it. The
/// transforms capture it as a <see cref="LocationInfo"/>, which keeps the tree, so the cached
/// model rebuilds the same source location; a location rebuilt from a file path would be neither.
/// </remarks>
internal static class SourceLocations
{
    /// <summary>
    /// The symbol's first location in source, or <see cref="Location.None"/> when it has none.
    /// </summary>
    public static Location Of(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource) return location;
        }

        return Location.None;
    }
}
