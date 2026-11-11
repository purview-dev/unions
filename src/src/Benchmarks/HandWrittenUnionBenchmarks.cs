using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

/// <summary>
/// Measures the hand-written <see cref="Union{T1,T2}"/> (surface D).
/// </summary>
/// <remarks>
/// It stores each case in its own typed field, so creation, <c>TryGetValue</c>, <c>Match</c> and <c>Tag</c>
/// are allocation-free. <c>Value</c> boxes a value-type case for the same reason as the generated shape.
/// Every benchmark batches <see cref="Operations"/> operations through
/// <see cref="BenchmarkAttribute.OperationsPerInvoke"/>; the reported figures are per operation.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class HandWrittenUnionBenchmarks
{
	const int Operations = 1_000;

	readonly Union<int, string> _intValue = 42;
	readonly Union<int, string> _stringValue = "hello";

	[Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
	public int Create_Int()
	{
		var total = 0;

		for (var index = 1; index <= Operations; index++)
		{
			Union<int, string> value = index;

			if (value.TryGetValue(out int number))
				total += number;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Create_String()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			Union<int, string> value = "hello";

			if (value.TryGetValue(out string? text))
				total += text.Length;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int TryGetValue_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_intValue.TryGetValue(out int value))
				total += value;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int TryGetValue_String()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_stringValue.TryGetValue(out string? text))
				total += text.Length;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int TryGetValue_Miss()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (!_intValue.TryGetValue(out string? _))
				total++;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int HasValue_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_intValue.HasValue)
				total++;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Tag_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			total += _intValue.Tag;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Match_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			total += _intValue.Match(static number => number, static text => text.Length);

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Value_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_intValue.Value is int value)
				total += value;

		return total;
	}
}
