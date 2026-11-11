namespace Purview.Unions.SourceGenerator.CodeFixes;

/// <summary>
/// The diagnostic ids the code fixes answer.
/// </summary>
/// <remarks>
/// The ids are public identities shared with the generator by name only. The code-fix project deliberately
/// does not reference the generator — the shipped analyzer is merged and every <c>InternalsVisibleTo</c> is
/// stripped — so the values are repeated here and a test asserts they match the generator's descriptors.
/// </remarks>
public static class UnionsDiagnosticIds
{
	/// <summary>
	/// The union declaration is not <c>partial</c>.
	/// </summary>
	public const string UnionMustBePartial = "UNI0001";

	/// <summary>
	/// The compiler's union attribute is required but missing.
	/// </summary>
	public const string UnionAttributeRequired = "UNI0007";

	/// <summary>
	/// A union case type is less accessible than the union.
	/// </summary>
	public const string UnionCaseTypeNotAccessible = "UNI0005";
}
