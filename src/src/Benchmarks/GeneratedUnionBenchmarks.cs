using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

// Surface A: a non-generic union declared with a typeof list.
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;

/// <summary>
/// Measures the generated custom-union shape for a two-case value/reference union.
/// </summary>
/// <remarks>
/// Every operation except <see cref="Value_Int"/> must report <c>0 B</c> allocated: construction,
/// <c>TryGetValue</c>, <c>is</c>, <c>switch</c> and <c>HasValue</c> all use the non-boxing access pattern.
/// <c>Value</c> returns <see cref="object"/> and boxes a value-type case by design.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GeneratedUnionBenchmarks
{
	readonly IntOrString _intValue = 42;
	readonly IntOrString _stringValue = "hello";

	[Benchmark(Baseline = true)]
	public IntOrString Create_Int() => 42;

	[Benchmark]
	public IntOrString Create_String() => "hello";

	[Benchmark]
	public int TryGetValue_Int() => _intValue.TryGetValue(out int value) ? value : -1;

	[Benchmark]
	public bool TryGetValue_Miss() => _intValue.TryGetValue(out string? _);

	[Benchmark]
	public bool HasValue_Int() => _intValue.HasValue;

	[Benchmark]
	public bool Is_Int() => _intValue is int;

	[Benchmark]
	public int Switch_Int() => Match(_intValue);

	[Benchmark]
	public int Switch_String() => Match(_stringValue);

	[Benchmark]
	public object? Value_Int() => _intValue.Value;

	static int Match(IntOrString value) =>
		value switch
		{
			int number => number,
			string text => text.Length,
			_ => -1,
		};
}
