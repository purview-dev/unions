using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

/// <summary>
/// The object-backed baseline the union shape exists to avoid.
/// </summary>
/// <remarks>
/// A single <see cref="object"/> field is what the C# <c>union</c> keyword lowers to. Storing a value-type
/// case boxes it, so <see cref="Create_Int"/> and <see cref="Store_Int"/> allocate; pattern matching an
/// already-boxed value does not. Compare these rows with <c>GeneratedUnionBenchmarks</c>.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BoxedBaselineBenchmarks
{
	readonly object _boxed = 42;

	[Benchmark(Baseline = true)]
	public object Create_Int() => 42;

	[Benchmark]
	public int Pattern_Int() => _boxed is int value ? value : -1;

	[Benchmark]
	public int Switch_Int() =>
		_boxed switch
		{
			int number => number,
			string text => text.Length,
			_ => -1,
		};
}
