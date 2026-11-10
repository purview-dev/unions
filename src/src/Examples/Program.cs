using System.Diagnostics.CodeAnalysis;

namespace Purview.Unions.Examples;

// Surface A: the case list is a typeof list on the attribute. The generator emits the union members,
// including the [Union] attribute the compiler recognises.
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;

// Surface B: the case list is the set of partial static factory signatures, which is how a generic
// union names a type parameter (typeof(T) is CS0416).
[GenerateUnion]
public readonly partial struct Maybe<T>
{
	internal static partial Maybe<T> Some(Some<T> value);

	internal static partial Maybe<T> None(None value);
}

public readonly record struct Some<T>(T Value);

public readonly record struct None;

// Surface C: the case list is a nested IUnionMembers provider. The provider's Create bodies delegate to
// the generator's CreateCaseN methods; a generator cannot add bodies to a source-declared interface.
[GenerateUnion]
public readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
	// The language requires the union member provider to be a public nested interface named IUnionMembers.
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

static class Program
{
	static void Main()
	{
		PrintSurfaceA();
		PrintSurfaceB();
		PrintSurfaceC();
		PrintSurfaceD();
		PrintAllocations();
	}

	static void PrintSurfaceA()
	{
		IntOrString value = 42;

		if (value.TryGetValue(out int number))
			Console.WriteLine($"A: int {number}; has-value={value.HasValue}");

		IntOrString text = "hello";
		if (text.TryGetValue(out string message))
			Console.WriteLine($"A: string {message}; has-value={text.HasValue}");

		IntOrString empty = default;
		Console.WriteLine($"A: default has-value={empty.HasValue}; value={empty.Value ?? "<null>"}");
	}

	static void PrintSurfaceB()
	{
		Maybe<int> some = new Some<int>(7);
		Maybe<int> none = new None();

		Console.WriteLine(
			$"B: some={some.TryGetValue(out Some<int> value) && value.Value == 7}; none={none.TryGetValue(out None _)}"
		);
	}

	static void PrintSurfaceC()
	{
		Outcome<int> outcome = 9;

		if (outcome.TryGetValue(out int number))
			Console.WriteLine($"C: int {number}");

		Outcome<int> failure = new InvalidOperationException("boom");
		if (failure.TryGetValue(out Exception error))
			Console.WriteLine($"C: error {error.Message}");
	}

	static void PrintSurfaceD()
	{
		Union<int, string> value = 42;

		if (value.TryGetValue(out int number))
			Console.WriteLine($"D: int {number}; tag={value.Tag}");

		Union<int, string> text = "hello";
		Console.WriteLine($"D: {text.Match(n => $"int {n}", s => $"string {s}")}; text={text.Value}");
	}

	static void PrintAllocations()
	{
		IntOrString value = 42;

		// Warm the paths so JIT/tiering allocations are not attributed to the measurement.
		for (var index = 0; index < 100; index++)
		{
			_ = value.TryGetValue(out int _);
			_ = value is int;
		}

		var beforeIs = GC.GetAllocatedBytesForCurrentThread();
		_ = value is int;
		var afterIs = GC.GetAllocatedBytesForCurrentThread();

		var beforeTry = GC.GetAllocatedBytesForCurrentThread();
		_ = value.TryGetValue(out int _);
		var afterTry = GC.GetAllocatedBytesForCurrentThread();

		var beforeValue = GC.GetAllocatedBytesForCurrentThread();
		_ = value.Value;
		var afterValue = GC.GetAllocatedBytesForCurrentThread();

		Console.WriteLine(
			$"alloc: is={afterIs - beforeIs} tryget={afterTry - beforeTry} value={afterValue - beforeValue}"
		);
	}
}
