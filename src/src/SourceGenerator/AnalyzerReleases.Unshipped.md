; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule below ships in 0.1.0-prerelease.1. Move the entries to
; AnalyzerReleases.Shipped.md under that release when the next version is published.
;
; UNI0007 is not reported while the generator emits the [Union] attribute itself. It is kept for the
; documented fallback path, where attribute emission is unavailable and the author adds the attribute by
; hand; AddUnionAttributeCodeFixProvider offers that edit.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNI0001 | Purview.Unions.Usage | Error | Union declaration must be partial
UNI0002 | Purview.Unions.Usage | Error | Unsupported union declaration kind
UNI0003 | Purview.Unions.Usage | Error | Union has no cases
UNI0004 | Purview.Unions.Usage | Error | Duplicate union case type
UNI0005 | Purview.Unions.Usage | Error | Union case type is not accessible
UNI0006 | Purview.Unions.Usage | Error | Too many union cases
UNI0007 | Purview.Unions.Usage | Error | Union attribute is required
UNI0008 | Purview.Unions.Usage | Error | Union case creation is ambiguous
UNI0009 | Purview.Unions.Usage | Error | Unsupported union case type
UNI0010 | Purview.Unions.Usage | Error | Nullable value-type case has no usable access form
UNI0011 | Purview.Unions.Usage | Error | Generic union needs a source-visible case list
UNI0012 | Purview.Unions.Usage | Error | Existing constructors conflict with the case list
UNI0013 | Purview.Unions.Usage | Error | Containing type cannot be named
UNI0014 | Purview.Unions.Usage | Error | Union case lists conflict
UNI0015 | Purview.Unions.Usage | Warning | Single-case union is degenerate
UNI0016 | Purview.Unions.Usage | Error | Union declaration conflicts with a required member
UNI9000 | Purview.Unions.Usage | Error | Unhandled exception in the unions source generator
