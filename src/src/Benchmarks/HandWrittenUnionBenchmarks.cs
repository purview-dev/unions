using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

/// <summary>
/// Measures the hand-written <see cref="Union{T1,T2}"/> (surface D).
/// </summary>
/// <remarks>
/// It stores each case in its own typed field, so creation, <c>TryGetValue</c>, <c>Match</c> and <c>Tag</c>
/// are allocation-free. <c>Value</c> boxes a value-type case for the same reason as the generated shape.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class HandWrittenUnionBenchmarks
{
	readonly Union<int, string> _intValue = 42;
	readonly Union<int, string> _stringValue = "hello";

	[Benchmark(Baseline = true)]
	public Union<int, string> Create_Int() => 42;

	[Benchmark]
	public Union<int, string> Create_String() => "hello";

	[Benchmark]
	public int TryGetValue_Int() => _intValue.TryGetValue(out int value) ? value : -1;

	[Benchmark]
	public int TryGetValue_String() => _stringValue.TryGetValue(out string? value) ? value.Length : -1;

	[Benchmark]
	public bool TryGetValue_Miss() => _intValue.TryGetValue(out string? _);

	[Benchmark]
	public bool HasValue_Int() => _intValue.HasValue;

	[Benchmark]
	public int Tag_Int() => _intValue.Tag;

	[Benchmark]
	public int Match_Int() => _intValue.Match(static number => number, static text => text.Length);

	[Benchmark]
	public object? Value_Int() => _intValue.Value;
}
