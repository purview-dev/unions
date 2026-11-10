namespace Purview.Unions.SourceGenerator.Common;

/// <summary>
/// The identities of the types the generator emits and references.
/// </summary>
static class UnionsTypeLibrary
{
	/// <summary>
	/// The attribute that opts a declaration into generation.
	/// </summary>
	public static readonly TypeIdentity GenerateUnionAttribute = new(
		PropertyLibrary.GenerateUnionAttributeName,
		PropertyLibrary.UnionsNamespace
	);

	/// <summary>
	/// The attribute the compiler recognises a union type by.
	/// </summary>
	public static readonly TypeIdentity UnionAttribute = new(
		PropertyLibrary.UnionAttributeName,
		PropertyLibrary.CompilerServicesNamespace
	);

	/// <summary>
	/// The interface that marks a type as a union and exposes its boxed value.
	/// </summary>
	public static readonly TypeIdentity UnionInterface = new(
		PropertyLibrary.UnionInterfaceName,
		PropertyLibrary.CompilerServicesNamespace
	);

	/// <summary>
	/// The attribute that marks generated source, emitted by the polyfill and applied by the writer.
	/// </summary>
	public static readonly TypeIdentity CompilerGeneratedAttribute = new(
		"CompilerGeneratedAttribute",
		PropertyLibrary.CompilerServicesNamespace
	);

	/// <summary>
	/// The special attribute the compiler treats as implicitly compiler-generated and embedded.
	/// </summary>
	public static readonly TypeIdentity EmbeddedAttribute = new("EmbeddedAttribute", "Microsoft.CodeAnalysis");

	/// <summary>
	/// The system attribute base type, used by the embedded-attribute polyfill.
	/// </summary>
	public static readonly TypeIdentity Attribute = new("Attribute", "System");
}
