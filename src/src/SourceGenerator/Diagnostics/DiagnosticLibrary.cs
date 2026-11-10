using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.Unions.SourceGenerator.Diagnostics;

/// <summary>
/// The diagnostic catalogue shared by the analyzer (which reports the rules) and the source generator
/// (which uses them to decide whether generation can continue).
/// </summary>
/// <remarks>
/// The blocking policy lives next to the descriptors so both hosts agree: <see cref="IsBlocking"/> is the
/// only place that decides whether a finding stops generation.
/// </remarks>
static class DiagnosticLibrary
{
	/// <summary>
	/// The diagnostic category used by every rule in this component.
	/// </summary>
	public const string Category = "Purview.Unions.Usage";

	/// <summary>
	/// The union declaration is not <c>partial</c>, so no members can be added to it.
	/// </summary>
	public static readonly DiagnosticDescriptor UnionMustBePartial = new(
		id: "UNI0001",
		title: "Union declaration must be partial",
		messageFormat: "'{0}' must be declared 'partial' so the union generator can add its members",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The generator adds the union's constructors, storage and access members as a second partial declaration, so the union must be declared 'partial'."
	);

	/// <summary>
	/// The attributed type is not a struct or class the union pattern can be emitted for.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedDeclarationKind = new(
		id: "UNI0002",
		title: "Unsupported union declaration kind",
		messageFormat: "'{0}' is not a supported union declaration; use a struct, readonly struct, class, record struct or record class",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The C# union pattern applies to classes and structs. Interfaces, enums, delegates, static classes, abstract classes and ref structs cannot be generated as unions."
	);

	/// <summary>
	/// No case list could be found on the union.
	/// </summary>
	public static readonly DiagnosticDescriptor UnionHasNoCases = new(
		id: "UNI0003",
		title: "Union has no cases",
		messageFormat: "'{0}' declares no union cases, so no union members can be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A union declares its cases through the [GenerateUnion] typeof list, partial static factory signatures, a nested IUnionMembers provider, or existing public single-parameter constructors."
	);

	/// <summary>
	/// The same case type appears more than once.
	/// </summary>
	public static readonly DiagnosticDescriptor DuplicateCaseType = new(
		id: "UNI0004",
		title: "Duplicate union case type",
		messageFormat: "The case type '{0}' is declared more than once on '{1}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Each case type must appear once; a duplicate would declare two constructors and two TryGetValue overloads with the same signature. A nullable value type and its underlying type are the same case."
	);

	/// <summary>
	/// A case type is not accessible from the generated public constructor.
	/// </summary>
	public static readonly DiagnosticDescriptor InaccessibleCaseType = new(
		id: "UNI0005",
		title: "Union case type is not accessible",
		messageFormat: "The case type '{0}' is not public, so the public union constructor generated for '{1}' could not expose it; make the case type public",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Every union creation member must be public. A public constructor cannot take a less accessible parameter type, so every case type must be public."
	);

	/// <summary>
	/// The case count does not fit the emitted tag.
	/// </summary>
	public static readonly DiagnosticDescriptor TooManyCases = new(
		id: "UNI0006",
		title: "Too many union cases",
		messageFormat: "'{0}' declares {1} cases, which exceeds the maximum a generated tag can address",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The generator sizes the tag automatically (byte, then ushort, then uint). This rule is only reachable beyond uint.MaxValue cases."
	);

	/// <summary>
	/// The compiler's union attribute is required but was not emitted.
	/// </summary>
	/// <remarks>
	/// The shipped generator emits the attribute itself, so this rule is not reported by default. It is
	/// kept for the documented fallback path in which attribute emission is unavailable, where the author
	/// must add it by hand; <c>AddUnionAttributeCodeFixProvider</c> offers that edit.
	/// </remarks>
	public static readonly DiagnosticDescriptor UnionAttributeRequired = new(
		id: "UNI0007",
		title: "Union attribute is required",
		messageFormat: "'{0}' must be annotated with [System.Runtime.CompilerServices.Union] for the compiler to treat it as a union",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A custom union is recognised by [System.Runtime.CompilerServices.Union]. The generator normally emits it; when attribute emission is unavailable the author must add it."
	);

	/// <summary>
	/// Two case types are implicitly convertible, so creation is ambiguous.
	/// </summary>
	public static readonly DiagnosticDescriptor AmbiguousCaseConversion = new(
		id: "UNI0008",
		title: "Union case creation is ambiguous",
		messageFormat: "The case types '{0}' and '{1}' of '{2}' are implicitly convertible, so creating the union from such a value is ambiguous",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The compiler requires a single best union creation member. Two case types with an implicit conversion between them make a value of the narrower type convertible to both, which is ambiguous."
	);

	/// <summary>
	/// A case type cannot be a union case.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedCaseType = new(
		id: "UNI0009",
		title: "Unsupported union case type",
		messageFormat: "The case type '{0}' cannot be a union case: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Pointers, function pointers, ref structs, void, static classes, dynamic and open generic definitions cannot be stored in a union field or exposed by a public constructor."
	);

	/// <summary>
	/// A nullable value-type case has no usable underlying access form.
	/// </summary>
	public static readonly DiagnosticDescriptor UnusableNullableCase = new(
		id: "UNI0010",
		title: "Nullable value-type case has no usable access form",
		messageFormat: "The nullable value-type case '{0}' of '{1}' has no usable underlying TryGetValue form",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A nullable value type is exposed through its underlying type, so that type must itself be a usable case type."
	);

	/// <summary>
	/// A generic union relies on the attribute case list, which cannot name type parameters.
	/// </summary>
	public static readonly DiagnosticDescriptor GenericUnionNeedsSourceCaseList = new(
		id: "UNI0011",
		title: "Generic union needs a source-visible case list",
		messageFormat: "The generic union '{0}' needs a source-visible case list: declare partial static factory signatures or a nested IUnionMembers provider, because typeof(T) is not a legal attribute argument (CS0416)",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The [GenerateUnion] typeof list cannot name a type parameter. A generic union declares its cases with partial static factory signatures, a nested IUnionMembers provider, or its own public constructors."
	);

	/// <summary>
	/// Existing constructors disagree with the declared case list.
	/// </summary>
	public static readonly DiagnosticDescriptor ExistingConstructorsConflict = new(
		id: "UNI0012",
		title: "Existing constructors conflict with the case list",
		messageFormat: "The existing public single-parameter constructors of '{0}' do not match its declared case list",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A hand-written union owns its creation members. Declaring a different case list would duplicate or contradict them, so the two must agree or one must be removed."
	);

	/// <summary>
	/// A nested union's containing type cannot be named from generated code.
	/// </summary>
	public static readonly DiagnosticDescriptor ContainingTypeCannotBeNamed = new(
		id: "UNI0013",
		title: "Containing type cannot be named",
		messageFormat: "'{0}' is nested in a type the generator cannot name; every containing type must be non-generic and declared 'partial'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Generated members are emitted in a second partial declaration nested inside the same containing types, so every containing type must be partial and non-generic."
	);

	/// <summary>
	/// More than one case list is present and they disagree.
	/// </summary>
	public static readonly DiagnosticDescriptor ConflictingCaseSources = new(
		id: "UNI0014",
		title: "Union case lists conflict",
		messageFormat: "'{0}' declares more than one case list, and they disagree; keep exactly one of the typeof list, partial factories, IUnionMembers provider or constructors",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Cases are resolved from the first source present, and a second, different source means the declaration is ambiguous."
	);

	/// <summary>
	/// The union declaration collides with a member the generated shape must provide.
	/// </summary>
	public static readonly DiagnosticDescriptor ConflictingUnionMember = new(
		id: "UNI0016",
		title: "Union declaration conflicts with a required member",
		messageFormat: "'{0}' conflicts with a member the generated union must provide: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The generated union provides Value, HasValue and TryGetValue. A union cannot be named Value, and it cannot already declare those members unless it is hand-written with its own creation members."
	);

	/// <summary>
	/// The union has one case, which is usually not worth a union.
	/// </summary>
	public static readonly DiagnosticDescriptor SingleCaseUnion = new(
		id: "UNI0015",
		title: "Single-case union is degenerate",
		messageFormat: "'{0}' has a single case; prefer using the case type directly",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A union exists to choose between cases. With one case the union adds a layer without adding a choice."
	);

	/// <summary>
	/// The generator failed unexpectedly while processing a union.
	/// </summary>
	/// <remarks>
	/// Reported instead of letting an exception escape the generator. An escaping exception surfaces as
	/// <c>CS8785</c> and discards <em>all</em> generated output for the compilation, so one malformed union
	/// would remove every other union's members.
	/// </remarks>
	public static readonly DiagnosticDescriptor UnhandledException = new(
		id: "UNI9000",
		title: "Unhandled exception in the unions source generator",
		messageFormat: "The unions source generator failed while processing '{0}': {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "An unexpected failure is reported against the union being processed rather than allowed to escape, so the rest of the compilation's generated output survives and the cause is identifiable. Please report it."
	);

	/// <summary>
	/// Gets every rule in the catalogue, which is exactly the analyzer's supported diagnostics.
	/// </summary>
	public static readonly ImmutableArray<DiagnosticDescriptor> AllDescriptors =
	[
		UnionMustBePartial,
		UnsupportedDeclarationKind,
		UnionHasNoCases,
		DuplicateCaseType,
		InaccessibleCaseType,
		TooManyCases,
		UnionAttributeRequired,
		AmbiguousCaseConversion,
		UnsupportedCaseType,
		UnusableNullableCase,
		GenericUnionNeedsSourceCaseList,
		ExistingConstructorsConflict,
		ContainingTypeCannotBeNamed,
		ConflictingCaseSources,
		ConflictingUnionMember,
		SingleCaseUnion,
		UnhandledException,
	];

	/// <summary>
	/// Determines whether a finding stops generation.
	/// </summary>
	/// <param name="descriptor">The rule that was violated.</param>
	/// <returns><see langword="true"/> when generation must stop for the affected target.</returns>
	/// <remarks>
	/// Every error-severity rule blocks its target: the generated constructors and access members are a
	/// single coherent shape, so there is no partial result to emit. <c>UNI0015</c> is the only warning and
	/// does not block.
	/// </remarks>
	public static bool IsBlocking(DiagnosticDescriptor descriptor) =>
		descriptor.DefaultSeverity == DiagnosticSeverity.Error;
}
