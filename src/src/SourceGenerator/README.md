# Purview.Unions source generator

An incremental Roslyn generator that emits the C# 15 **custom union type** shape for declarations opted in
with `[GenerateUnion]`. It ships inside the [`Purview.Unions`](https://www.nuget.org/packages/Purview.Unions)
package under `analyzers/dotnet/cs`; it is not published as a separate package.

## What it emits

For `[GenerateUnion(typeof(int), typeof(string))] public readonly partial struct IntOrString`:

- `[System.Runtime.CompilerServices.Union]` and `: IUnion`;
- one typed field per case and a `byte`/`ushort` tag;
- one public constructor per case, with a null argument leaving the union empty;
- `object? Value`, `bool HasValue`, and one `TryGetValue(out TCase)` per case;
- on `net10.0`, internal `[Embedded]` polyfills for `UnionAttribute`/`IUnion`.

It never emits a `union` declaration. See `docs/wiki/Union-Design.md` for the emitted shape line by line.

## Pipeline

```
CompilationProvider ─▶ GetHasUnionAttribute ─┐
                                            ├─▶ GetUnionPolyfillRequest ─▶ polyfill output
ForAttributeWithMetadataName ───────────────┤
  [GenerateUnion]                           │
    └─▶ UnionDiscovery.CreateTarget ────────┴─▶ RegisterSourceOutput (one per union)
```

- Discovery converts `INamedTypeSymbol` into `UnionModel`: `TypeReference` literals, `EquatableArray`, and
  source spans. No symbol, compilation, syntax node or `Location` enters cached state.
- `UnionAnalysis` is the single implementation of the rules; the analyzer consumes it too.
- The generator reports only `UNI9000` (an unexpected failure). The per-target rules are the analyzer's.
- Hint names are derived from the metadata identity: `<namespace>.<containing types>.<name>[_arity].g.cs`.

## Build properties

| Property | Default | Purpose |
| --- | --- | --- |
| `DisableUnionsSourceGenerator` | `false` | Disables generation and the polyfills |

Declared as both a `PurviewGeneratorVisibleProperty` and a `CompilerVisibleProperty`, and shipped to
`PackageReference` consumers as `buildTransitive/Purview.Unions.props`. The framework's `PSGF0003` validation
keeps the two in sync.

## Diagnostics

`UNI0001`–`UNI0016` and `UNI9000`; see `docs/wiki/Diagnostics.md`. `AnalyzerReleases.Unshipped.md` is the
release-tracked catalogue (RS2008).

`UNI0000` and `UNI9001` are suppression descriptors, not rules: the assembly also ships
`UnionMembersNestingSuppressor`, which suppresses `CA1034` and `CA1515` on a union's public nested
`IUnionMembers` provider.

## Tests

`src/tests/SourceGenerator.UnitTests` contains the Phase 0 compiler experiments, generation and determinism
tests, the incremental-cache matrix, every diagnostic, the code fixes, both target frameworks, and the
no-boxing assertion (`GC.GetAllocatedBytesForCurrentThread()`).
