using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Purview.Unions.SourceGenerator.Common;

namespace Purview.Unions.SourceGenerator.Models;

/// <summary>
/// A cache-safe description of a source location: the originating file path plus spans.
/// </summary>
/// <remarks>
/// Locations are captured during discovery so validation can report diagnostics later. A Roslyn
/// <see cref="Location"/> is deliberately not stored: it would root the syntax tree in the incremental
/// pipeline state.
/// </remarks>
/// <param name="FilePath">The path of the file that contains the location, or an empty string.</param>
/// <param name="TextSpan">The source span of the location.</param>
/// <param name="LinePositionSpan">The line/column span of the location.</param>
readonly record struct UnionSourceLocation(string FilePath, TextSpan TextSpan, LinePositionSpan LinePositionSpan)
{
	/// <summary>
	/// Gets an empty location, used when no source location is available.
	/// </summary>
	public static readonly UnionSourceLocation None;

	/// <summary>
	/// Creates the Roslyn <see cref="Location"/> represented by this value.
	/// </summary>
	/// <returns>The matching <see cref="Location"/>, or <see cref="Location.None"/>.</returns>
	public Location ToLocation() =>
		TextSpan == default && LinePositionSpan == default && string.IsNullOrEmpty(FilePath)
			? Location.None
			: Location.Create(FilePath ?? string.Empty, TextSpan, LinePositionSpan);
}

/// <summary>
/// Where a union's case list came from.
/// </summary>
enum UnionCaseSource
{
	/// <summary>No source was found.</summary>
	None,

	/// <summary>The <c>typeof</c> list on <c>[GenerateUnion]</c>.</summary>
	Attribute,

	/// <summary>Partial static factory signatures declared on the union.</summary>
	Factories,

	/// <summary>A nested <c>IUnionMembers</c> provider.</summary>
	Provider,

	/// <summary>Existing public single-parameter constructors (a hand-written union).</summary>
	Constructors,
}

/// <summary>
/// Describes one union case.
/// </summary>
/// <param name="CaseType">
/// The type the case is exposed and matched as: the underlying type of a nullable value-type case,
/// otherwise the case type with its nullable reference annotation removed.
/// </param>
/// <param name="ParameterType">
/// The type of the creation member's parameter: the declared case type, so a nullable value type keeps
/// its <c>Nullable&lt;T&gt;</c> form.
/// </param>
/// <param name="StorageType">The type of the field that stores the case.</param>
/// <param name="FullyQualifiedName">The display name, used for diagnostics.</param>
/// <param name="IsReferenceType">Whether the case is a reference type.</param>
/// <param name="IsNullableValueType">Whether the case is a nullable value type.</param>
/// <param name="IsTypeParameter">Whether the case is a generic type parameter.</param>
/// <param name="Tag">The tag value that marks this case active (<c>1</c>-based).</param>
readonly record struct UnionCaseModel(
	TypeReference CaseType,
	TypeReference ParameterType,
	TypeReference StorageType,
	string FullyQualifiedName,
	bool IsReferenceType,
	bool IsNullableValueType,
	bool IsTypeParameter,
	int Tag
)
{
	/// <summary>
	/// Gets whether the creation member must reject a null value, leaving the union empty.
	/// </summary>
	public bool RequiresNullCheck => IsReferenceType || IsNullableValueType;

	/// <summary>
	/// Gets the name of the storage field.
	/// </summary>
	public string FieldName => PropertyLibrary.CaseFieldName(Tag - 1);

	/// <summary>
	/// Gets an empty case model.
	/// </summary>
	public static readonly UnionCaseModel Empty;
}

/// <summary>
/// Describes one generic type parameter of the union or a containing type.
/// </summary>
/// <param name="Name">The type parameter name.</param>
/// <param name="Constraints">The constraint expressions written after <c>where T :</c>.</param>
readonly record struct TypeParameterModel(string Name, EquatableArray<string> Constraints)
{
	/// <summary>
	/// Gets an empty type parameter model.
	/// </summary>
	public static readonly TypeParameterModel Empty;
}

/// <summary>
/// Describes one containing type of a nested union.
/// </summary>
/// <param name="Name">The type's simple name.</param>
/// <param name="Kind">The declaration kind to reproduce.</param>
/// <param name="Accessibility">The declaration's accessibility, which a partial must repeat.</param>
/// <param name="IsReadOnly">Whether the declaration is a <c>readonly</c> struct.</param>
/// <param name="IsStatic">Whether the declaration is a static class.</param>
readonly record struct ContainingTypeModel(
	string Name,
	TypeDeclarationKind Kind,
	TypeDeclarationAccessibility Accessibility,
	bool IsReadOnly,
	bool IsStatic
)
{
	/// <summary>
	/// Gets an empty containing type model.
	/// </summary>
	public static readonly ContainingTypeModel Empty;
}

/// <summary>
/// Describes one partial static factory signature the generator must implement.
/// </summary>
/// <param name="Name">The factory method's name.</param>
/// <param name="Accessibility">The factory method's accessibility.</param>
/// <param name="ParameterType">The factory method's single parameter type.</param>
readonly record struct UnionFactoryModel(
	string Name,
	TypeDeclarationAccessibility Accessibility,
	TypeReference ParameterType
)
{
	/// <summary>
	/// Gets an empty factory model.
	/// </summary>
	public static readonly UnionFactoryModel Empty;
}

/// <summary>
/// An immutable, value-equatable description of one union to generate.
/// </summary>
/// <param name="Namespace">The union's containing namespace, or an empty string for the global namespace.</param>
/// <param name="IsGlobalNamespace">Whether the union is declared in the global namespace.</param>
/// <param name="UnionType">The identity of the union type itself.</param>
/// <param name="Name">The union's simple name, used for its constructors.</param>
/// <param name="FullyQualifiedName">The union's display name, used for diagnostics.</param>
/// <param name="Kind">The declaration kind to reproduce.</param>
/// <param name="Accessibility">The declaration's accessibility, which a partial must repeat.</param>
/// <param name="IsReadOnly">Whether the declaration is a <c>readonly</c> struct.</param>
/// <param name="TypeParameters">The union's own generic parameters.</param>
/// <param name="ContainingTypes">The containing types, outermost first.</param>
/// <param name="Cases">The union's cases, in declaration order.</param>
/// <param name="Factories">
/// The partial static factory signatures to implement, in declaration order. Empty unless the case list
/// came from those signatures.
/// </param>
/// <param name="CaseSource">Where the case list came from.</param>
/// <param name="EmitMembers">
/// Whether the generator emits constructors and access members. False for a hand-written union whose
/// case list came from its own constructors: the author owns the storage, so only the compiler attribute
/// is emitted.
/// </param>
/// <param name="EmitUnionAttribute">Whether the compiler's <c>[Union]</c> attribute must be emitted.</param>
/// <param name="TagTypeName">The smallest integer type that addresses every case.</param>
/// <param name="HintName">The deterministic hint name of the generated source file.</param>
/// <param name="Location">The declaration location of the union.</param>
readonly record struct UnionModel(
	string Namespace,
	bool IsGlobalNamespace,
	TypeIdentity UnionType,
	string Name,
	string FullyQualifiedName,
	TypeDeclarationKind Kind,
	TypeDeclarationAccessibility Accessibility,
	bool IsReadOnly,
	EquatableArray<TypeParameterModel> TypeParameters,
	EquatableArray<ContainingTypeModel> ContainingTypes,
	EquatableArray<UnionCaseModel> Cases,
	EquatableArray<UnionFactoryModel> Factories,
	UnionCaseSource CaseSource,
	bool EmitMembers,
	bool EmitUnionAttribute,
	string TagTypeName,
	string HintName,
	UnionSourceLocation Location
)
{
	/// <summary>
	/// Gets an empty union model.
	/// </summary>
	public static readonly UnionModel Empty;
}

/// <summary>
/// The complete per-target analysis: the model to generate (when generation can continue) and every
/// finding, in discovery order.
/// </summary>
/// <param name="Model">The union model, or an empty model when generation is blocked.</param>
/// <param name="Diagnostics">Every finding the target produced.</param>
readonly record struct UnionAnalysisResult(UnionModel Model, EquatableArray<ReportableDiagnostic> Diagnostics)
{
	/// <summary>
	/// Gets whether any finding blocks generation for the target.
	/// </summary>
	public bool HasBlockingDiagnostics =>
		!Diagnostics.IsEmpty && Diagnostics.Any(static diagnostic => diagnostic.IsBlocking);

	/// <summary>
	/// Gets whether the target can be generated.
	/// </summary>
	public bool CanGenerate => !Model.Equals(UnionModel.Empty) && !HasBlockingDiagnostics;
}
