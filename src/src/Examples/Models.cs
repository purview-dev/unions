namespace Purview.Unions.Examples;

// Surface A: the case list is a typeof list on the attribute. The generator emits the union members,
// including the [Union] attribute the compiler recognises.
[GenerateUnion(typeof(int), typeof(string))]
readonly partial struct IntOrString;

// Surface B: the case list is the set of partial static factory signatures, which is how a generic
// union names a type parameter (typeof(T) is CS0416).
[GenerateUnion]
readonly partial struct Maybe<T>
{
	internal static partial Maybe<T> Some(Some<T> value);

	internal static partial Maybe<T> None(None value);
}

readonly record struct Some<T>(T Value);

readonly record struct None;

// Surface C: the case list is a nested IUnionMembers provider. The provider's Create bodies delegate to
// the generator's CreateCaseN methods; a generator cannot add bodies to a source-declared interface.
[GenerateUnion]
readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
	// The language requires the union member provider to be a public nested interface named IUnionMembers.
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
