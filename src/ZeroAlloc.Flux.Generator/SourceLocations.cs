using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Where the ZFLUX diagnostics are reported.
/// </summary>
/// <remarks>
/// The models hold the symbols of the compilation being generated, since every step reruns on each
/// compilation, so a symbol's own location is a source location in one of that compilation's
/// trees. The compiler applies <c>#pragma warning disable</c> to such a location, and the IDE can
/// navigate to it. A location rebuilt from a file path would be neither.
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

    /// <summary>
    /// Orders locations by file path, then position, so "the later declaration" is the same on
    /// every run.
    /// </summary>
    public static int Compare(Location x, Location y)
    {
        var byPath = string.CompareOrdinal(x.SourceTree?.FilePath, y.SourceTree?.FilePath);
        return byPath != 0 ? byPath : x.SourceSpan.Start.CompareTo(y.SourceSpan.Start);
    }
}
