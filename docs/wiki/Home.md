# Purview.Unions

`Purview.Unions` generates the C# 15 **custom union type** shape so a value-type union case is never boxed by
construction, `TryGetValue`, `is` or `switch`. It also ships a hand-written `Union<T1, T2>` for projects that do
not use a generator.

| Page | Read it for |
| --- | --- |
| [Getting Started](Getting-Started.md) | install, declare a union and use it |
| [Union Design](Union-Design.md) | the compiler contract, the seven experiments and the polyfill decision |
| [Diagnostics](Diagnostics.md) | every `UNI0001`–`UNI0016`/`UNI9000` finding and its fix |
| [Interop](Interop.md) | Results, Telemetry, ZodSharp, JSON, trimming and AOT |
| [Trimming and AOT](Trimming-AOT.md) | why the generated shape and `Union<T1, T2>` are trim- and AOT-safe |
| [Performance](Performance.md) | the BenchmarkDotNet suite, measured results and what an allocation means |
| [Release Flow](Release-Flow.md) | the PR and release gates, pack validation and how a version ships |

The package targets `net10.0` and `net11.0`. On `net10.0` the generator emits internal `[Embedded]` polyfills for
`UnionAttribute`/`IUnion`; on `net11.0` the BCL types are used.
