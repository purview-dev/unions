using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Unions.SourceGenerator.Common;
using Purview.Unions.SourceGenerator.Discovery;

namespace Purview.Unions.SourceGenerator.Analyzers;

/// <summary>
/// Suppresses <c>CA1034</c> (nested types should not be visible), <c>CA1515</c> (public types should be
/// internal) and <c>CA1000</c> (do not declare static members on generic types) on the <c>IUnionMembers</c>
/// provider the C# union contract requires a union to nest.
/// </summary>
/// <remarks>
/// <para>
/// The language finds a union's members on a <b>public nested interface</b> named <c>IUnionMembers</c>. A
/// public nested type is exactly what <c>CA1034</c> asks you not to write, and an internal union's public
/// nested provider is exactly what <c>CA1515</c> asks you to make internal. Under a strict analyzer set every
/// surface-C union fails the build for following the language contract. The advice cannot be followed:
/// making the interface internal removes the union semantics.
/// </para>
/// <para>
/// The provider's static <c>Create</c> methods are the contract's creation members. A generic union's
/// provider is necessarily generic (it inherits the union's type parameters), so those members are reported
/// as <c>CA1000</c>. The advice cannot be followed either: the members must be static and named <c>Create</c>
/// for the language to find them, so removing them removes the union semantics.
/// </para>
/// <para>
/// The suppressions are deliberately narrow. The nesting and visibility ones apply only when the reported
/// type is an interface named <c>IUnionMembers</c> whose immediate containing type is a union (a type carrying
/// <c>[GenerateUnion]</c> or <c>[Union]</c>). <c>CA1000</c> is reported at the member, not the interface, so
/// that suppression resolves the member symbol instead: only a public static <c>Create</c> returning its
/// containing union, on a public <c>IUnionMembers</c> nested in that union, is covered. A type of that name
/// nested in an ordinary type, any other nested type in a union, a static helper on the provider, and a
/// static member of the union itself keep the warnings.
/// </para>
/// <para>
/// These are suppression descriptors, not reported rules, so they are not release-tracked. <c>UNI0000</c>
/// turns the <c>CA1034</c> suppression off, <c>UNI9001</c> turns the <c>CA1515</c> suppression off and
/// <c>UNI9002</c> turns the <c>CA1000</c> suppression off.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionMembersNestingSuppressor : DiagnosticSuppressor
{
	/// <summary>
	/// The suppression of <c>CA1034</c> on a union's <c>IUnionMembers</c> provider.
	/// </summary>
	public static readonly SuppressionDescriptor SuppressNestedUnionMembers = new(
		"UNI0000",
		"CA1034",
		"IUnionMembers is required by the C# union contract to be a public nested interface, so CA1034 cannot be satisfied without removing the union semantics."
	);

	/// <summary>
	/// The suppression of <c>CA1515</c> on a union's <c>IUnionMembers</c> provider.
	/// </summary>
	public static readonly SuppressionDescriptor SuppressNestedUnionMembersVisibility = new(
		"UNI9001",
		"CA1515",
		"IUnionMembers is required by the C# union contract to be a public nested interface, so CA1515 cannot be satisfied without removing the union semantics."
	);

	/// <summary>
	/// The suppression of <c>CA1000</c> on a generic union's <c>IUnionMembers</c> provider's static
	/// <c>Create</c> members.
	/// </summary>
	public static readonly SuppressionDescriptor SuppressGenericUnionMembersStatic = new(
		"UNI9002",
		"CA1000",
		"IUnionMembers' static Create members are the C# union contract's creation members; a generic union's provider is necessarily generic, so CA1000 cannot be satisfied without removing the union semantics."
	);

	/// <inheritdoc />
	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
		[SuppressNestedUnionMembers, SuppressNestedUnionMembersVisibility, SuppressGenericUnionMembersStatic];

	/// <inheritdoc />
	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach (var diagnostic in context.ReportedDiagnostics)
		{
			var suppression = diagnostic.Id switch
			{
				"CA1034" when IsUnionMembersProvider(context, diagnostic) => SuppressNestedUnionMembers,
				"CA1515" when IsUnionMembersProvider(context, diagnostic) => SuppressNestedUnionMembersVisibility,
				"CA1000" when IsUnionMembersCreate(context, diagnostic) => SuppressGenericUnionMembersStatic,
				_ => null,
			};

			if (suppression is null)
				continue;

			context.ReportSuppression(Suppression.Create(suppression, diagnostic));
		}
	}

	/// <summary>
	/// Determines whether the reported diagnostic sits on a union's <c>IUnionMembers</c> provider.
	/// </summary>
	/// <remarks>
	/// <c>CA1034</c> is reported at the nested type's identifier, so the location is resolved to the nearest
	/// enclosing declaration that declares a type. The innermost one wins: a different nested type in a union
	/// does not become suppressible just because its containing type is a union.
	/// </remarks>
	static bool IsUnionMembersProvider(SuppressionAnalysisContext context, Diagnostic diagnostic)
	{
		var location = diagnostic.Location;
		if (!location.IsInSource || location.SourceTree is not { } tree)
			return false;

		var semanticModel = context.GetSemanticModel(tree);
		var root = tree.GetRoot(context.CancellationToken);
		var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

		for (var current = node; current is not null; current = current.Parent)
		{
			if (semanticModel.GetDeclaredSymbol(current, context.CancellationToken) is not INamedTypeSymbol type)
				continue;

			return
				type.TypeKind != TypeKind.Interface
				|| !string.Equals(type.Name, PropertyLibrary.UnionMembersInterfaceName, StringComparison.Ordinal)
				? false
				: type.ContainingType is { } containingType && IsUnion(containingType);
		}

		return false;
	}

	/// <summary>
	/// Determines whether the reported diagnostic sits on a static <c>Create</c> member of a union's
	/// <c>IUnionMembers</c> provider.
	/// </summary>
	/// <remarks>
	/// <c>CA1000</c> is reported at the static member rather than at the interface, so the member symbol is
	/// resolved instead of the enclosing type. The innermost declared method wins.
	/// </remarks>
	static bool IsUnionMembersCreate(SuppressionAnalysisContext context, Diagnostic diagnostic)
	{
		var location = diagnostic.Location;
		if (!location.IsInSource || location.SourceTree is not { } tree)
			return false;

		var semanticModel = context.GetSemanticModel(tree);
		var root = tree.GetRoot(context.CancellationToken);
		var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

		for (var current = node; current is not null; current = current.Parent)
		{
			if (semanticModel.GetDeclaredSymbol(current, context.CancellationToken) is not IMethodSymbol method)
				continue;

			return IsUnionMembersCreate(method);
		}

		return false;
	}

	/// <summary>
	/// Determines whether a method symbol is a creation member of a union's <c>IUnionMembers</c> provider.
	/// </summary>
	/// <remarks>
	/// The conditions mirror the provider contract the generator recognises: a public static method named
	/// <c>Create</c> with one by-value parameter, returning the union, declared on a public nested
	/// <c>IUnionMembers</c> interface inside that union.
	/// </remarks>
	static bool IsUnionMembersCreate(IMethodSymbol method)
	{
		if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public)
			return false;

		if (!string.Equals(method.Name, PropertyLibrary.UnionMembersCreateMethodName, StringComparison.Ordinal))
			return false;

		if (method.Parameters.Length != 1 || method.Parameters[0].RefKind != RefKind.None)
			return false;

		if (
			method.ContainingType is not { TypeKind: TypeKind.Interface } provider
			|| provider.DeclaredAccessibility != Accessibility.Public
			|| !string.Equals(provider.Name, PropertyLibrary.UnionMembersInterfaceName, StringComparison.Ordinal)
		)
			return false;

		if (provider.ContainingType is not { } union || !IsUnion(union))
			return false;

		// The provider's Create returns the union it creates; that is the contract the generator reads.
		return SymbolEqualityComparer.Default.Equals(method.ReturnType, union);
	}

	/// <summary>
	/// Determines whether a type is a union.
	/// </summary>
	/// <remarks>
	/// Both attributes are checked: <c>[GenerateUnion]</c> is what the consumer writes, and <c>[Union]</c> is
	/// what the compiler recognises (the generator emits it, or the BCL supplies it on <c>net11.0</c>). The
	/// source check keeps the suppressor correct when generation is disabled.
	/// </remarks>
	static bool IsUnion(INamedTypeSymbol type)
	{
		if (UnionAnalysis.HasGenerateUnionAttribute(type))
			return true;

		foreach (var attribute in type.GetAttributes())
		{
			if (UnionsTypeLibrary.UnionAttribute.Matches(attribute.AttributeClass))
				return true;
		}

		return false;
	}
}
