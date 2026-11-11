using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;

namespace Purview.Unions.Benchmarks;

// Surface B: a generic union declared with partial static factory signatures.
[GenerateUnion]
public readonly partial struct Maybe<T>
{
	internal static partial Maybe<T> Some(Some<T> value);

	internal static partial Maybe<T> None(None value);
}

public readonly record struct Some<T>(T Value);

public readonly record struct None;

// Surface C: a generic union declared with a nested IUnionMembers provider.
[GenerateUnion]
public readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
	[SuppressMessage(
		"Design",
		"CA1034:Nested types should not be visible",
		Justification = "The C# union member provider is required by the language to be a public nested interface named IUnionMembers."
	)]
	public interface IUnionMembers
	{
		static Outcome<T> Create(T? value) => Outcome<T>.CreateCase0(value);

		static Outcome<T> Create(Exception? value) => Outcome<T>.CreateCase1(value);

		object? Value { get; }

		bool HasValue { get; }

		bool TryGetValue(out T value);

		bool TryGetValue(out Exception value);
	}
}

/// <summary>
/// Measures the generic declaration surfaces, where the case type is a type parameter.
/// </summary>
/// <remarks>
/// Every benchmark batches <see cref="Operations"/> operations through
/// <see cref="BenchmarkAttribute.OperationsPerInvoke"/>; the reported figures are per operation.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GenericUnionBenchmarks
{
	const int Operations = 1_000;

	readonly Maybe<int> _some = new Some<int>(7);
	readonly Maybe<int> _none = CreateNone();
	readonly Outcome<int> _outcome = 9;
	readonly Outcome<int> _failure = new InvalidOperationException("boom");

	[Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
	public int Maybe_CreateSome()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			Maybe<int> value = new Some<int>(7);

			if (value.TryGetValue(out Some<int> some))
				total += some.Value;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Maybe_CreateNone()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			Maybe<int> value = new None();

			if (value.TryGetValue(out None _))
				total++;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Maybe_TryGetValue_Some()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_some.TryGetValue(out Some<int> some))
				total += some.Value;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Maybe_TryGetValue_None()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_none.TryGetValue(out None _))
				total++;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Outcome_CreateValue()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			Outcome<int> value = index;

			if (value.TryGetValue(out int number))
				total += number;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Outcome_CreateError()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
		{
			Outcome<int> value = new InvalidOperationException("boom");

			if (value.TryGetValue(out Exception? _))
				total++;
		}

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Outcome_TryGetValue_Value()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_outcome.TryGetValue(out int value))
				total += value;

		return total;
	}

	[Benchmark(OperationsPerInvoke = Operations)]
	public int Outcome_TryGetValue_Error()
	{
		var total = 0;

		for (var index = 0; index < Operations; index++)
			if (_failure.TryGetValue(out Exception? _))
				total++;

		return total;
	}

	static Maybe<int> CreateNone() => new None();
}
