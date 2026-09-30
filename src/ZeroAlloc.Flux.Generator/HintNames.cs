using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Hint names of generated files.
/// </summary>
internal static class HintNames
{
    /// <summary>
    /// The hint name of a feature's generated store file, unique within the compilation: the
    /// namespace, then the containing types and the feature joined by <c>+</c>, each with its
    /// arity, then <c>.Store.g.cs</c>, as in <c>App.Outer`1+CounterState.Store.g.cs</c>. A feature
    /// in the global namespace has no namespace part.
    /// </summary>
    /// <remarks>
    /// Nesting is written with <c>+</c> rather than a dot, as in a type's metadata name, so the
    /// name shows which parts are containing types. The old name replaced every dot with
    /// <c>_</c>, so <c>App_X.CounterState</c> and <c>App.X_CounterState</c> shared it. Every store file
    /// ends in <c>.Store.g.cs</c>, so none can take the name of <c>FluxDispatcher.g.cs</c> or
    /// <c>FluxServiceCollectionExtensions.g.cs</c>. Roslyn compares hint names ignoring case, so
    /// features whose names differ only in case still collide.
    /// </remarks>
    public static string ForFeature(INamedTypeSymbol feature)
    {
        var sb = new StringBuilder();
        AppendNamespace(sb, feature.ContainingNamespace);
        AppendTypeChain(sb, feature);
        return Sanitize(sb.ToString()) + ".Store.g.cs";
    }

    /// <summary>
    /// Keeps the characters an identifier, a namespace separator or an arity is written with,
    /// and escapes every other UTF-16 code unit as <c>-uXXXX</c>. No identifier contains a
    /// <c>-</c>, so an escaped name never collides with a name that needed no escaping.
    /// </summary>
    public static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '.' or '+' or '`')
            {
                sb.Append(c);
                continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]) &&
                IsIdentifierCategory(CharUnicodeInfo.GetUnicodeCategory(name, i)))
            {
                sb.Append(c).Append(name[i + 1]);
                i++;
                continue;
            }

            if (!char.IsSurrogate(c) && IsIdentifierCategory(CharUnicodeInfo.GetUnicodeCategory(c)))
            {
                sb.Append(c);
                continue;
            }

            sb.Append("-u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static void AppendNamespace(StringBuilder sb, INamespaceSymbol? ns)
    {
        if (ns is null || ns.IsGlobalNamespace) return;
        AppendNamespace(sb, ns.ContainingNamespace);
        sb.Append(ns.Name).Append('.');
    }

    private static void AppendTypeChain(StringBuilder sb, INamedTypeSymbol type)
    {
        if (type.ContainingType is { } outer)
        {
            AppendTypeChain(sb, outer);
            sb.Append('+');
        }
        sb.Append(type.Name);
        if (type.Arity > 0) sb.Append('`').Append(type.Arity.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsIdentifierCategory(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
        UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber or
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
}
