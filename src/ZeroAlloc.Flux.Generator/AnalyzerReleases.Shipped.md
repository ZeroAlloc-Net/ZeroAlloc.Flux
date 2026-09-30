; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID  | Category       | Severity | Notes
---------|----------------|----------|-----------------------------------------------------------------------------
ZFLUX001 | ZeroAlloc.Flux | Error    | [Reducer] method's state parameter type isn't decorated with [Feature]
ZFLUX002 | ZeroAlloc.Flux | Error    | Two [Reducer] methods in the same feature target the same action type
ZFLUX003 | ZeroAlloc.Flux | Error    | [Reducer] method has invalid signature
ZFLUX004 | ZeroAlloc.Flux | Error    | [Feature(InitialState = ...)] factory method not found or has wrong signature
ZFLUX005 | ZeroAlloc.Flux | Error    | [Feature] type must be declared partial

## Release 1.1.8

### New Rules

Rule ID  | Category       | Severity | Notes
---------|----------------|----------|-----------------------------------------------------------------------------
ZFLUX006 | ZeroAlloc.Flux | Error    | [Feature] type must not be generic
ZFLUX007 | ZeroAlloc.Flux | Error    | [Feature] type must be accessible to the rest of its assembly
ZFLUX008 | ZeroAlloc.Flux | Error    | Two [Feature] types have qualified names that differ only in case
