namespace Purview.Unions;

/// <summary>
/// A framework-neutral, read-only view over a union value.
/// </summary>
/// <remarks>
/// <para>
/// On .NET 11 and later the BCL defines <c>System.Runtime.CompilerServices.IUnion</c>, which exposes
/// only <see cref="Value"/>. This interface adds <see cref="Tag"/> so a hand-written or generated union
/// can follow its active case without unboxing it, and it is available on every target framework,
/// including .NET 10, so a consumer can hold a union behind one type without multi-targeting its own
/// code.
/// </para>
/// <para>
/// <see cref="Union{T1,T2}"/> implements this interface on every target framework. Generated unions
/// implement <c>System.Runtime.CompilerServices.IUnion</c> (or the generator's internal polyfill on
/// .NET 10) because that is the interface the language's union contract is built around.
/// </para>
/// </remarks>
public interface IUnion
{
	/// <summary>
	/// Gets the boxed value of the active case, or <see langword="null"/> when the union is empty.
	/// Prefer the non-boxing <c>TryGetValue</c> members on a concrete union.
	/// </summary>
	object? Value { get; }

	/// <summary>
	/// Gets the tag identifying which case is active: <c>0</c> when the union is empty, then <c>1</c>,
	/// <c>2</c>, … for the case types in declaration order.
	/// </summary>
	int Tag { get; }
}
