# Performance

The allocation claim is the reason this package exists: a value-type union case is never boxed by construction,
`TryGetValue`, `is` or `switch`. The committed BenchmarkDotNet suite measures that claim, the generator that
produces the shape, and the object-backed baseline the shape replaces.

Results are produced by [`src/src/Benchmarks`](../../src/src/Benchmarks) and folded here for reference. Every
benchmark batches 1,000 operations through `OperationsPerInvoke`, so a sub-nanosecond operation is measurable
and the JIT cannot fold the loop body away; the reported mean and allocation are per operation.

## How to run

```bash
# Full suite on .NET 10
just perf-tests

# Full suite on .NET 11
just perf-tests-net11

# One suite (the `--` passes the filter to BenchmarkDotNet)
just perf-tests -- --filter "*GeneratedUnionBenchmarks*"
```

Always use a Release build. BenchmarkDotNet writes `BenchmarkDotNet.Artifacts/` at the repository root (the
working directory the recipe runs in); the Markdown reports are the source for the tables below.

The suite pins BenchmarkDotNet `0.16.0-preview.2` because `0.15.8` does not recognise the .NET 11 runtime.

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

- BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9818)
- 13th Gen Intel Core i9-13900KF 3.00 GHz, 1 CPU, 32 logical / 24 physical cores; 63.77 GB total, 38.32 GB available
- .NET SDK 11.0.100; .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
- `IterationCount=5  LaunchCount=1  WarmupCount=3`

These are the recorded `net10.0` numbers. The suite also targets `net11.0`; run `just perf-tests-net11` to
measure the current runtime. Numbers from one machine are indicative — use them for relative comparison, and
re-run before quoting absolute values.

## Generated union (surface A)

| Method             | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------------- |----------:|----------:|----------:|-------:|----------:|
| Create_Int         | 0.2200 ns | 0.0182 ns | 0.0028 ns |      - |         - |
| Create_String      | 0.1914 ns | 0.0122 ns | 0.0032 ns |      - |         - |
| TryGetValue_Int    | 0.2670 ns | 0.1613 ns | 0.0419 ns |      - |         - |
| TryGetValue_String | 0.3538 ns | 0.2877 ns | 0.0747 ns |      - |         - |
| TryGetValue_Miss   | 0.2575 ns | 0.0840 ns | 0.0218 ns |      - |         - |
| HasValue_Int       | 0.2377 ns | 0.0204 ns | 0.0053 ns |      - |         - |
| Is_Int             | 0.2158 ns | 0.0463 ns | 0.0120 ns |      - |         - |
| Switch_Int         | 0.1916 ns | 0.0069 ns | 0.0018 ns |      - |         - |
| Switch_String      | 0.3762 ns | 0.0209 ns | 0.0054 ns |      - |         - |
| Value_Int          | 1.7450 ns | 0.5377 ns | 0.1396 ns | 0.0013 |     24 B |

Construction, both `TryGetValue` directions, the miss path, `HasValue`, `is` and both `switch` shapes allocate
**0 B**. `Value` allocates 24 bytes: the box for the `int` case.

## Hand-written union (surface D)

| Method             | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------------- |----------:|----------:|----------:|-------:|----------:|
| Create_Int         | 0.2120 ns | 0.0043 ns | 0.0011 ns |      - |         - |
| Create_String      | 0.1817 ns | 0.0055 ns | 0.0008 ns |      - |         - |
| TryGetValue_Int    | 0.1843 ns | 0.0016 ns | 0.0004 ns |      - |         - |
| TryGetValue_String | 0.1855 ns | 0.0040 ns | 0.0010 ns |      - |         - |
| TryGetValue_Miss   | 0.1830 ns | 0.0009 ns | 0.0001 ns |      - |         - |
| HasValue_Int       | 0.1837 ns | 0.0019 ns | 0.0003 ns |      - |         - |
| Tag_Int            | 0.1805 ns | 0.0008 ns | 0.0002 ns |      - |         - |
| Match_Int          | 0.4778 ns | 0.0054 ns | 0.0008 ns |      - |         - |
| Value_Int          | 1.4837 ns | 0.0154 ns | 0.0024 ns | 0.0013 |     24 B |

`Union<T1, T2>` has the same allocation profile as the generated shape: everything except `Value` is 0 B.

## Generic unions (surfaces B and C)

| Method                    | Mean      | Error     | StdDev    | Gen0   | Allocated |
|-------------------------- |----------:|----------:|----------:|-------:|----------:|
| Maybe_CreateSome          | 0.1832 ns | 0.0072 ns | 0.0011 ns |      - |         - |
| Maybe_CreateNone          | 0.1899 ns | 0.0112 ns | 0.0029 ns |      - |         - |
| Maybe_TryGetValue_Some    | 0.2185 ns | 0.0035 ns | 0.0009 ns |      - |         - |
| Maybe_TryGetValue_None    | 0.1997 ns | 0.0034 ns | 0.0009 ns |      - |         - |
| Outcome_CreateValue       | 0.2244 ns | 0.0354 ns | 0.0092 ns |      - |         - |
| Outcome_CreateError       | 3.8637 ns | 0.0569 ns | 0.0148 ns | 0.0064 |    120 B |
| Outcome_TryGetValue_Value | 0.1837 ns | 0.0043 ns | 0.0007 ns |      - |         - |
| Outcome_TryGetValue_Error | 0.2717 ns | 0.0040 ns | 0.0006 ns |      - |         - |

The generic surfaces behave like the non-generic ones. `Outcome_CreateError` allocates 120 bytes because the
benchmark constructs a new `InvalidOperationException` for every operation; that is the exception, not the
union. Reading the same error case through `Outcome_TryGetValue_Error` is 0 B.

## Object-backed baseline

| Method      | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated |
|------------ |----------:|----------:|----------:|------:|-------:|----------:|
| Create_Int  | 2.1729 ns | 0.9646 ns | 0.2505 ns |  1.00 | 0.0013 |     24 B |
| Pattern_Int | 0.3319 ns | 0.0142 ns | 0.0022 ns |  0.15 |      - |         - |
| Switch_Int  | 0.3174 ns | 0.0323 ns | 0.0084 ns |  0.15 |      - |         - |

This is the shape a `union` declaration lowers to: one `object` field, so storing a value-type case boxes it
(24 B and a slower path), and matching an already-boxed value does not. The generated union and
`Union<T1, T2>` remove the boxing at entry.

## Generator

| Method         | UnionCount | Mean       | Error      | StdDev    | Ratio | Gen0     | Gen1     | Allocated  |
|--------------- |----------- |-----------:|-----------:|----------:|------:|---------:|---------:|-----------:|
| **ColdRun**        | **1**          |   **668.4 μs** |  **25.93 μs** |   **6.73 μs** |  **1.00** |  **17.5781** |   **4.8828** |  **328.28 KB** |
| IncrementalRun | 1          |   128.8 μs |    4.92 μs |    0.76 μs |  0.19 |   2.6855 |   0.4883 |   51.91 KB |
| **ColdRun**        | **10**         |   **897.1 μs** | **559.07 μs** | **145.19 μs** |  **1.00** |  **39.0625** |   **8.7891** |  **730.17 KB** |
| IncrementalRun | 10         |   223.7 μs |   14.83 μs |    3.85 μs |  0.25 |   7.5684 |   0.4883 |  139.65 KB |
| **ColdRun**        | **100**        | **2,347.0 μs** |  **69.76 μs** |  **10.80 μs** |  **1.00** | **261.7188** | **148.4375** | **4835.03 KB** |
| IncrementalRun | 100        | 1,244.5 μs |   71.05 μs |   18.45 μs |  0.53 |  58.5938 |   9.7656 | 1087.95 KB |

`ColdRun` constructs a fresh driver for every operation, which is the first generation in an IDE or build.
`IncrementalRun` reuses the driver and compilation after a warm run, so it measures the cached path. The
incremental path is 19–53% of the cold path and 16–23% of its allocation; the remainder is Roslyn updating the
compilation with the generated trees, not the generator re-discovering cases.

## Interpreting the results

1. **Mean** — average execution time.
2. **Error / StdDev** — noise and spread.
3. **Gen 0/1** — collections per 1000 operations.
4. **Allocated** — managed bytes per operation.

Signs to investigate:

- **An allocation on construction, `TryGetValue`, `is`, `switch` or `HasValue`** means the non-boxing access
  pattern regressed.
- **An allocation through `Value`** is expected for a value-type case and is the documented cost of the contract.
- **A growing gap between `ColdRun` and `IncrementalRun`** means an incremental pipeline stage stopped being
  value-equatable and is re-running on every keystroke.

## Regenerating the tables

Run the suite, then copy the `*-report-github.md` tables into the sections above and update the environment
header. The benchmark project targets `net10.0` and `net11.0`; the recipe defaults to `net10.0` so a recorded
run is reproducible against the LTS line.
