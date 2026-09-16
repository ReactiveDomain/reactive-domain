; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.18.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
RD0001 | Usage | Warning | ReadModelHandleAnalyzer — a public `Handle(T)` on a read model lets a caller bypass the queue.
