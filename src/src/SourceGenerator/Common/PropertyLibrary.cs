using System.Globalization;

namespace Purview.Unions.SourceGenerator.Common;

/// <summary>
/// Names, suffixes and build-property names shared across the generator.
/// </summary>
static class PropertyLibrary
{
	/// <summary>
	/// The MSBuild property that disables generation for a consumer.
	/// </summary>
	public const string DisableGeneratorProperty = "DisableUnionsSourceGenerator";

	/// <summary>
	/// The suffix of a generated union source file.
	/// </summary>
	public const string GeneratedFileSuffix = ".g.cs";

	/// <summary>
	/// The name of the union attribute the compiler recognises.
	/// </summary>
	public const string UnionAttributeName = "UnionAttribute";

	/// <summary>
	/// The name of the union access interface the compiler recognises.
	/// </summary>
	public const string UnionInterfaceName = "IUnion";

	/// <summary>
	/// The namespace that holds the compiler's union types.
	/// </summary>
	public const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

	/// <summary>
	/// The name of the attribute that opts a declaration into generation.
	/// </summary>
	public const string GenerateUnionAttributeName = "GenerateUnionAttribute";

	/// <summary>
	/// The namespace that holds <see cref="GenerateUnionAttributeName"/>.
	/// </summary>
	public const string UnionsNamespace = "Purview.Unions";

	/// <summary>
	/// The name of the nested interface that turns a union into a union member provider.
	/// </summary>
	public const string UnionMembersInterfaceName = "IUnionMembers";

	/// <summary>
	/// The name of the provider's union factory method.
	/// </summary>
	public const string UnionMembersCreateMethodName = "Create";

	/// <summary>
	/// The name of the provider's non-boxing access property.
	/// </summary>
	public const string HasValueMemberName = "HasValue";

	/// <summary>
	/// The name of a union's boxed access property.
	/// </summary>
	public const string ValueMemberName = "Value";

	/// <summary>
	/// The name of a union's non-boxing access method.
	/// </summary>
	public const string TryGetValueMemberName = "TryGetValue";

	/// <summary>
	/// The prefix for a generated per-case storage field.
	/// </summary>
	public const string CaseFieldPrefix = "_case";

	/// <summary>
	/// The name of the generated tag field.
	/// </summary>
	public const string TagFieldName = "_tag";

	/// <summary>
	/// The name of the generated constructor and access-method parameter.
	/// </summary>
	public const string ValueParameterName = "value";

	/// <summary>
	/// The name of the generated static helper on a union member provider's backing implementation,
	/// which the provider's <c>Create</c> method delegates to.
	/// </summary>
	public const string CreateCaseMethodPrefix = "CreateCase";

	/// <summary>
	/// The maximum number of cases the generator addresses. A <c>byte</c> tag covers the ordinary range;
	/// a union beyond it is widened to <c>ushort</c> automatically, and beyond that the declaration is
	/// rejected as unmanageable.
	/// </summary>
	public const int MaxSupportedCases = ushort.MaxValue;

	/// <summary>
	/// Creates the storage field name for a zero-based case index.
	/// </summary>
	/// <param name="index">The zero-based case index.</param>
	/// <returns>The field name.</returns>
	public static string CaseFieldName(int index) => CaseFieldPrefix + index.ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// Gets the smallest integral type name that can address <paramref name="caseCount"/> cases.
	/// </summary>
	/// <param name="caseCount">The number of cases, excluding the empty tag.</param>
	/// <returns><c>byte</c> or <c>ushort</c>.</returns>
	public static string GetTagTypeName(int caseCount) => caseCount <= byte.MaxValue ? "byte" : "ushort";
}
