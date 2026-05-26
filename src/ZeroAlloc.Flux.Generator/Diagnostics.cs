using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

internal static class Diagnostics
{
    private const string Category = "ZeroAlloc.Flux";

    public static readonly DiagnosticDescriptor ZFLUX001_ReducerOnNonFeatureState = new(
        id: "ZFLUX001",
        title: "[Reducer] method's state parameter type isn't decorated with [Feature]",
        messageFormat: "Reducer '{0}' has state parameter type '{1}' which is not decorated with [Feature]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ZFLUX002_DuplicateReducerInFeature = new(
        id: "ZFLUX002",
        title: "Two [Reducer] methods in the same feature target the same action type",
        messageFormat: "Feature '{0}' has two [Reducer] methods targeting action type '{1}' — within a feature, each action type may have at most one reducer",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ZFLUX003_ReducerSignatureInvalid = new(
        id: "ZFLUX003",
        title: "[Reducer] method has invalid signature",
        messageFormat: "Reducer '{0}' must be public static, accept at least two parameters, and return the same type as its first parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ZFLUX004_InitialStateFactoryInvalid = new(
        id: "ZFLUX004",
        title: "[Feature(InitialState = ...)] factory method not found or has wrong signature",
        messageFormat: "Feature '{0}' references InitialState factory '{1}' which must be 'public static {0} {1}(IServiceProvider)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ZFLUX005_FeatureNotPartial = new(
        id: "ZFLUX005",
        title: "[Feature] type must be declared partial",
        messageFormat: "Feature '{0}' must be declared partial so the generator can emit alongside it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
