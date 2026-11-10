# Performance

The allocation claim is the reason this package exists: a value-type union case is never boxed by construction,
`TryGetValue`, `is` or `switch`. The committed BenchmarkDotNet suite measures that claim, the generator that
produces the shape, and the object-backed baseline the shape replaces.

Results are produced by [`src/src/Benchmarks`](../../src/src/Benchmarks) and folded here for reference.

## How to run

```bash
# Full suite on .NET 10
just perf-tests

# Full suite on .NET 11
just perf-tests -f net11.0

# One suite (the `--` passes the filter to BenchmarkDotNet)
just perf-tests -- --filter "*GeneratedUnionBenchmarks*"
```

Always use a Release build. BenchmarkDotNet writes `BenchmarkDotNet.Artifacts/` in the project folder; the
Markdown reports are the source for the tables below.

## What is measured

| Suite | Surfaces |
| --- | --- |
| `GeneratedUnionBenchmarks` | A: `[GenerateUnion(typeof(int), typeof(string))]` — creation, `TryGetValue`, `HasValue`, `is`, `switch`, `Value` |
| `HandWrittenUnionBenchmarks` | D: `Union<T1, T2>` — creation, `TryGetValue`, `HasValue`, `Tag`, `Match`, `Value` |
| `GenericUnionBenchmarks` | B and C: `Maybe<T>` and `Outcome<T>` |
| `BoxedBaselineBenchmarks` | the single-`object` shape a `union` declaration lowers to |
| `GeneratorBenchmarks` | `UnionsSourceGenerator` over 1, 10 and 100 declarations, cold and incremental |

`Value` returns `object`, so a value-type case is boxed when it is read. That is inherent to the union contract
and is measured deliberately; do not “fix” it. Everything else in the generated shape must report `0 B`.

## Environment

<!-- Fill from the recorded run: BenchmarkDotNet version, OS, CPU, SDK, runtime. -->

## Generated union (surface A)

<!-- Fill from *GeneratedUnionBenchmarks*-report-github.md. -->

## Hand-written union (surface D)

<!-- Fill from *HandWrittenUnionBenchmarks*-report-github.md. -->

## Generic unions (surfaces B and C)

<!-- Fill from *GenericUnionBenchmarks*-report-github.md. -->

## Object-backed baseline

<!-- Fill from *BoxedBaselineBenchmarks*-report-github.md. -->

## Generator

<!-- Fill from *GeneratorBenchmarks*-report-github.md. -->

## Interpreting the results

1. **Mean** — average execution time.
2. **Error / StdDev** — noise and spread.
3. **Gen 0/1/2** — collections per 1000 operations.
4. **Allocated** — managed bytes per operation.

Signs to investigate:

- **An allocation on construction, `TryGetValue`, `is`, `switch` or `HasValue`** means the non-boxing access
  pattern regressed.
- **An allocation through `Value`** is expected for a value-type case and is the documented cost of the contract.
- **A regression in the generator's cold path** with no change to the incremental path usually means discovery
  started walking symbols it used to cache.

## Regenerating the tables

Run the suite, then copy the `*-report-github.md` tables into the sections above and update the environment
header. The benchmark project targets `net10.0` and `net11.0`; the recipe defaults to `net10.0` so a recorded
run is reproducible against the LTS line.
