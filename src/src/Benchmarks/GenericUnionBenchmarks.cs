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
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GenericUnionBenchmarks
{
	readonly Maybe<int> _some = new Some<int>(7);
	readonly Maybe<int> _none = CreateNone();
	readonly Outcome<int> _outcome = 9;
	readonly Outcome<int> _failure = new InvalidOperationException("boom");

	[Benchmark(Baseline = true)]
	public Maybe<int> Maybe_CreateSome() => new Some<int>(7);

	[Benchmark]
	public Maybe<int> Maybe_CreateNone() => new None();

	[Benchmark]
	public int Maybe_TryGetValue_Some() => _some.TryGetValue(out Some<int> value) ? value.Value : -1;

	[Benchmark]
	public bool Maybe_TryGetValue_None() => _none.TryGetValue(out None _);

	[Benchmark]
	public Outcome<int> Outcome_CreateValue() => 9;

	[Benchmark]
	public Outcome<int> Outcome_CreateError() => new InvalidOperationException("boom");

	[Benchmark]
	public int Outcome_TryGetValue_Value() => _outcome.TryGetValue(out int value) ? value : -1;

	[Benchmark]
	public bool Outcome_TryGetValue_Error() => _failure.TryGetValue(out Exception? _);

	static Maybe<int> CreateNone() => new None();
}
