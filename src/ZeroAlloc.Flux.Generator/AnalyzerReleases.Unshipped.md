; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category       | Severity | Notes
---------|----------------|----------|-----------------------------------------------------------------------------
ZFLUX006 | ZeroAlloc.Flux | Error    | [Feature] type must not be generic
ZFLUX007 | ZeroAlloc.Flux | Error    | [Feature] type must be accessible to the rest of its assembly
ZFLUX008 | ZeroAlloc.Flux | Error    | Two [Feature] types have qualified names that differ only in case
