using System.Diagnostics.CodeAnalysis;

namespace Purview.Unions.SourceGenerator;

// Surface A: a non-generic union declared with a typeof list.
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;

// Surface B: a generic union declared with partial static factory signatures.
[GenerateUnion]
public readonly partial struct Maybe<T>
{
	internal static partial Maybe<T> Some(Some<T> value);

	internal static partial Maybe<T> None(None value);
}

public readonly record struct Some<T>(T Value);

public readonly record struct None;

// Surface B: an internal union with internal case types. The generated creation members are public
// members of an internal type, so an internal case type is sufficient; UNI0005 must not block it.
readonly record struct InternalSome<T>(T Value);

readonly record struct InternalNone;

[GenerateUnion]
readonly partial struct InternalMaybe<T>
{
	internal static partial InternalMaybe<T> Some(InternalSome<T> value);

	internal static partial InternalMaybe<T> None(InternalNone value);
}

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

// A class-based union: the same shape, with a null reference as the empty union.
[GenerateUnion(typeof(Uri), typeof(string))]
public partial class UriOrString;
