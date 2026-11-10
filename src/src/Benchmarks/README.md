# Purview.Unions performance benchmarks

BenchmarkDotNet suite for `Purview.Unions`. It measures the generated custom-union shape, the hand-written
`Union<T1, T2>`, the generic declaration surfaces and the generator itself, with an `object`-backed baseline
so the allocation difference is visible rather than asserted.

Every suite uses `[MemoryDiagnoser]` and a `[SimpleJob]` profile, so time and allocations are captured
together.

## Structure

| File | Covers |
| --- | --- |
| `GeneratedUnionBenchmarks.cs` | Surface A (`[GenerateUnion(typeof(int), typeof(string))]`): creation, `TryGetValue`, `HasValue`, `is`, `switch` and `Value` |
| `HandWrittenUnionBenchmarks.cs` | Surface D (`Union<T1, T2>`): creation, `TryGetValue`, `HasValue`, `Tag`, `Match` and `Value` |
| `GenericUnionBenchmarks.cs` | Surfaces B and C (`Maybe<T>`, `Outcome<T>`): factories, creation and `TryGetValue` |
| `BoxedBaselineBenchmarks.cs` | The `object`-backed shape a `union` declaration lowers to, for comparison |
| `GeneratorBenchmarks.cs` | `UnionsSourceGenerator` over 1, 10 and 100 union declarations, cold and incremental |

## How to run

From the repository root:

```bash
# Full suite on .NET 10
just perf-tests

# Full suite on .NET 11
just perf-tests -f net11.0

# One suite (the `--` passes the filter to BenchmarkDotNet)
just perf-tests -- --filter "*GeneratedUnionBenchmarks*"
```

Always use a Release build; running benchmarks in Debug produces meaningless numbers.

## Results

BenchmarkDotNet writes `BenchmarkDotNet.Artifacts/` in this folder: HTML and Markdown reports plus logs.
The committed results and their interpretation live in
[`docs/wiki/Performance.md`](../../../docs/wiki/Performance.md).

## Adding a suite

1. Add a `*.cs` file under `src/src/Benchmarks/`.
2. Mark the class with `[MemoryDiagnoser]` and `[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]`.
3. Build any state in the constructor or `[GlobalSetup]`, so setup is not measured.
4. Return the operation's result from each `[Benchmark]`; BenchmarkDotNet treats the return value as the
   operation and a discarded result can be optimised away.
5. Run it with `--filter "*MyBenchmarks*"` and confirm the class appears in the report.
