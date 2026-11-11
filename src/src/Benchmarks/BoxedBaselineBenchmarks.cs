using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

/// <summary>
/// The object-backed baseline the union shape exists to avoid.
/// </summary>
/// <remarks>
/// <para>
/// A single <see cref="object"/> field is what the C# <c>union</c> keyword lowers to. Storing a value-type
/// case boxes it, so <see cref="Create_Int"/> allocates; pattern matching an already-boxed value does not.
/// Compare these rows with <see cref="GeneratedUnionBenchmarks"/>.
/// </para>
/// <para>
/// Every benchmark batches <see cref="Operations"/> operations through
/// <see cref="BenchmarkAttribute.OperationsPerInvoke"/>; the reported figures are per operation.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BoxedBaselineBenchmarks
{
	const int Operations = 1_000;

	readonly object _boxed = 42;

	[Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
	public int Create_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			object value = index;

			if (value is int number)
				total += number;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Pattern_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_boxed is int value)
				total += value;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Switch_Int()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			total += Match(_boxed);

		return total;
	}

	static int Match(object value) =>
		value switch
		{
			int number => number,
			string text => text.Length,
			_ => -1,
		};
}
