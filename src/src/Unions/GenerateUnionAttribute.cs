namespace Purview.Unions;

/// <summary>
/// Opts a <see langword="partial"/> <see langword="struct"/>, <see langword="class"/> or record into
/// union generation.
/// </summary>
/// <remarks>
/// <para>
/// The generator emits the C# 15 custom-union shape for the annotated type: the
/// <c>[System.Runtime.CompilerServices.Union]</c> attribute, one public constructor per case type, a
/// <c>Value</c> property and the non-boxing <c>HasValue</c>/<c>TryGetValue</c> members. That shape is
/// what makes value-type cases allocation-free in pattern matching, which a <c>union</c> declaration
/// cannot be: the declaration always stores <see cref="object"/> and boxes every value-type case.
/// </para>
/// <para>
/// Cases are resolved from the first source-visible list present on the declaration, in this order:
/// the <see cref="Cases"/> list supplied to this attribute, the declaration's own partial
/// <see langword="static"/> factory signatures, a nested <c>IUnionMembers</c> provider, or its
/// existing public single-parameter constructors. A generic union must use one of the last three,
/// because <c>typeof(T)</c> is not a legal attribute argument (<c>CS0416</c>).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateUnionAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenerateUnionAttribute"/> class with the case types
	/// in declaration order.
	/// </summary>
	/// <param name="cases">The case types of the generated union.</param>
	public GenerateUnionAttribute(params Type[] cases) => Cases = cases;

	/// <summary>
	/// Gets the case types of the generated union, in declaration order.
	/// </summary>
	public IReadOnlyList<Type> Cases { get; }
}
