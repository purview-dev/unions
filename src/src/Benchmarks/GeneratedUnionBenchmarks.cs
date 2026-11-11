using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

// Surface A: a non-generic union declared with a typeof list.
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;

/// <summary>
/// Measures the generated custom-union shape for a two-case value/reference union.
/// </summary>
/// <remarks>
/// <para>
/// Every benchmark performs <see cref="Operations"/> operations and declares them through
/// <see cref="BenchmarkAttribute.OperationsPerInvoke"/>, so a sub-nanosecond operation is still measurable and
/// the JIT cannot fold the loop body away. The reported mean and allocation are per operation.
/// </para>
/// <para>
/// Every operation except <see cref="Value_Int"/> must report <c>0 B</c> allocated: construction,
/// <c>TryGetValue</c>, <c>is</c>, <c>switch</c> and <c>HasValue</c> all use the non-boxing access pattern.
/// <c>Value</c> returns <see cref="object"/> and boxes a value-type case by design.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GeneratedUnionBenchmarks
{
	const int Operations = 1_000;

	readonly IntOrString _intValue = 42;
	readonly IntOrString _stringValue = "hello";

	[Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
	public int Create_Int()
	{
		var total = 0;

		for (var index = 1; index <= Operations; index++)
		{
			IntOrString value = index;

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
			IntOrString value = "hello";

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
	public int Is_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_intValue is int)
				total++;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Switch_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			total += Match(_intValue);

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Switch_String()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			total += Match(_stringValue);

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

	static int Match(IntOrString value) =>
		value switch
		{
			int number => number,
			string text => text.Length,
			_ => -1,
		};
}
