; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule shipped in 1.0.0 is recorded in AnalyzerReleases.Shipped.md under `## Release 1.0.0`. Add a
; new or changed rule here, and move it across when the next version ships.
;
; UNI0007 is not reported while the generator emits the [Union] attribute itself. It is kept for the
; documented fallback path, where attribute emission is unavailable and the author adds the attribute by
; hand; AddUnionAttributeCodeFixProvider offers that edit.
