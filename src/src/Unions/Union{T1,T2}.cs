using System.Diagnostics.CodeAnalysis;

namespace Purview.Unions;

/// <summary>
/// A non-boxing discriminated union of two case types, usable on every target framework without a
/// source generator.
/// </summary>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <remarks>
/// <para>
/// Create a value with the implicit conversions from <typeparamref name="T1"/> or
/// <typeparamref name="T2"/>, with <see cref="Create(T1)"/>/<see cref="Create(T2)"/>, or with the public
/// constructors. Read it with the non-boxing <see cref="TryGetValue(out T1)"/> overloads,
/// <see cref="Match{TResult}"/>, or <see cref="Switch"/>.
/// </para>
/// <para>
/// Each case is stored in its own typed field, so a value-type case is never boxed by construction or
/// by <c>TryGetValue</c>. Only <see cref="Value"/> boxes, because its return type is
/// <see cref="object"/> — that is inherent to the union access contract.
/// </para>
/// <para>
/// On <c>net11.0</c> and later the type also carries
/// <c>[System.Runtime.CompilerServices.Union]</c> and implements the BCL
/// <c>System.Runtime.CompilerServices.IUnion</c>, so the compiler treats it as a union: it is
/// exhaustive over <typeparamref name="T1"/> and <typeparamref name="T2"/>, and type patterns use
/// <see cref="TryGetValue(out T1)"/> without boxing. The explicitly declared implicit operators take
/// priority over the compiler's union conversions, as the language specifies. On <c>net10.0</c> the
/// type is an ordinary struct with the same API.
/// </para>
/// <para>
/// <see cref="Tag"/> uses the same numbering on every framework: <c>0</c> is an empty union, <c>1</c> is
/// <typeparamref name="T1"/>, <c>2</c> is <typeparamref name="T2"/>. A null reference or nullable value
/// therefore creates an empty union rather than a union holding null.
/// </para>
/// </remarks>
#if NET11_0_OR_GREATER
[System.Runtime.CompilerServices.Union]
public readonly struct Union<T1, T2> : IUnion, IEquatable<Union<T1, T2>>, System.Runtime.CompilerServices.IUnion
#else
public readonly struct Union<T1, T2> : IUnion, IEquatable<Union<T1, T2>>
#endif
{
	const byte First = 1;
	const byte Second = 2;

	readonly T1? _value1;
	readonly T2? _value2;
	readonly byte _tag;

	/// <summary>
	/// Initializes a union holding a <typeparamref name="T1"/> value.
	/// </summary>
	/// <param name="value">
	/// The value. A null reference or a null nullable value leaves the union empty.
	/// </param>
	public Union(T1 value)
	{
		if (CaseTraits<T1>.CanBeNull && value is null)
			return;

		_value1 = value;
		_tag = First;
	}

	/// <summary>
	/// Initializes a union holding a <typeparamref name="T2"/> value.
	/// </summary>
	/// <param name="value">
	/// The value. A null reference or a null nullable value leaves the union empty.
	/// </param>
	public Union(T2 value)
	{
		if (CaseTraits<T2>.CanBeNull && value is null)
			return;

		_value2 = value;
		_tag = Second;
	}

	/// <summary>
	/// Whether a <typeparamref name="T"/> case can hold <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// Read before the null test so a non-nullable value-type case never evaluates (and therefore never
	/// boxes) <c>value is null</c>. This is runtime metadata, not reflection over members, so it is
	/// trimming- and Native AOT-safe.
	/// </remarks>
	/// <typeparam name="T">The case type to inspect.</typeparam>
	static class CaseTraits<T>
	{
		/// <summary>
		/// Gets a value indicating whether a <typeparamref name="T"/> can be null.
		/// </summary>
		public static readonly bool CanBeNull =
			!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
	}

	/// <summary>
	/// Creates a union holding a <typeparamref name="T1"/> value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A union holding <paramref name="value"/>, or an empty union when it is null.</returns>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2> Create(T1 value) => new(value);

	/// <summary>
	/// Creates a union holding a <typeparamref name="T2"/> value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A union holding <paramref name="value"/>, or an empty union when it is null.</returns>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2> Create(T2 value) => new(value);

	/// <summary>
	/// Implicitly converts a <typeparamref name="T1"/> to the union.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A union holding <paramref name="value"/>, or an empty union when it is null.</returns>
	public static implicit operator Union<T1, T2>(T1? value) => new(value!);

	/// <summary>
	/// Implicitly converts a <typeparamref name="T2"/> to the union.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A union holding <paramref name="value"/>, or an empty union when it is null.</returns>
	public static implicit operator Union<T1, T2>(T2? value) => new(value!);

	/// <inheritdoc />
	public int Tag => _tag;

	/// <inheritdoc />
	public object? Value =>
		_tag switch
		{
			First => _value1,
			Second => _value2,
			_ => null,
		};

	/// <summary>
	/// Gets a value indicating whether the union holds a non-null value.
	/// </summary>
	public bool HasValue =>
		_tag switch
		{
			First => _value1 is not null,
			Second => _value2 is not null,
			_ => false,
		};

	/// <summary>
	/// Gets the <typeparamref name="T1"/> value without boxing.
	/// </summary>
	/// <param name="value">The value when the first case is active.</param>
	/// <returns><see langword="true"/> when the union holds a non-null <typeparamref name="T1"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T1? value)
	{
		if (_tag == First)
		{
			value = _value1!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Gets the <typeparamref name="T2"/> value without boxing.
	/// </summary>
	/// <param name="value">The value when the second case is active.</param>
	/// <returns><see langword="true"/> when the union holds a non-null <typeparamref name="T2"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T2? value)
	{
		if (_tag == Second)
		{
			value = _value2!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Folds the union into a single value without boxing.
	/// </summary>
	/// <typeparam name="TResult">The type of the folded value.</typeparam>
	/// <param name="first">Produces the result for the first case.</param>
	/// <param name="second">Produces the result for the second case.</param>
	/// <returns>The matching case's result.</returns>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);

		return _tag switch
		{
			First => first(_value1!),
			Second => second(_value2!),
			_ => throw new InvalidOperationException("The union is empty."),
		};
	}

	/// <summary>
	/// Invokes the action for the active case.
	/// </summary>
	/// <param name="first">The action for the first case.</param>
	/// <param name="second">The action for the second case.</param>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public void Switch(Action<T1> first, Action<T2> second)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);

		switch (_tag)
		{
			case First:
				first(_value1!);
				return;
			case Second:
				second(_value2!);
				return;
			default:
				throw new InvalidOperationException("The union is empty.");
		}
	}

	/// <inheritdoc />
	public bool Equals(Union<T1, T2> other) =>
		_tag == other._tag
		&& EqualityComparer<T1?>.Default.Equals(_value1, other._value1)
		&& EqualityComparer<T2?>.Default.Equals(_value2, other._value2);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is Union<T1, T2> other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode()
	{
		unchecked
		{
			var hashCode = (int)_tag;
			hashCode = (hashCode * 397) ^ (_tag == First ? EqualityComparer<T1?>.Default.GetHashCode(_value1!) : 0);
			hashCode = (hashCode * 397) ^ (_tag == Second ? EqualityComparer<T2?>.Default.GetHashCode(_value2!) : 0);
			return hashCode;
		}
	}

	/// <summary>
	/// Determines whether two unions hold the same case and value.
	/// </summary>
	/// <param name="left">The first union.</param>
	/// <param name="right">The second union.</param>
	/// <returns><see langword="true"/> when the unions are equal.</returns>
	public static bool operator ==(Union<T1, T2> left, Union<T1, T2> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two unions differ.
	/// </summary>
	/// <param name="left">The first union.</param>
	/// <param name="right">The second union.</param>
	/// <returns><see langword="true"/> when the unions differ.</returns>
	public static bool operator !=(Union<T1, T2> left, Union<T1, T2> right) => !left.Equals(right);

	/// <summary>
	/// Returns a string representation of the active value.
	/// </summary>
	/// <returns>The active value's text, or <c>&lt;empty&gt;</c>.</returns>
	public override string ToString() => Value?.ToString() ?? "<empty>";
}
