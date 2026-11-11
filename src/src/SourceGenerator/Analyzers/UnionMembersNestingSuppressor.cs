using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Unions.SourceGenerator.Common;
using Purview.Unions.SourceGenerator.Discovery;

namespace Purview.Unions.SourceGenerator.Analyzers;

/// <summary>
/// Suppresses <c>CA1034</c> (nested types should not be visible) and <c>CA1515</c> (public types should be
/// internal) on the <c>IUnionMembers</c> provider the C# union contract requires a union to nest.
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
/// The suppression is deliberately narrow. It applies only when the reported type is an interface named
/// <c>IUnionMembers</c> whose immediate containing type is a union (a type carrying
/// <c>[GenerateUnion]</c> or <c>[Union]</c>). A type of that name nested in an ordinary type, and any other
/// nested type in a union, keep the warnings.
/// </para>
/// <para>
/// These are suppression descriptors, not reported rules, so they are not release-tracked. <c>UNI0000</c>
/// turns the <c>CA1034</c> suppression off and <c>UNI9001</c> turns the <c>CA1515</c> suppression off.
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

	/// <inheritdoc />
	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
		[SuppressNestedUnionMembers, SuppressNestedUnionMembersVisibility];

	/// <inheritdoc />
	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach (var diagnostic in context.ReportedDiagnostics)
		{
			var descriptor = diagnostic.Id switch
			{
				"CA1034" => SuppressNestedUnionMembers,
				"CA1515" => SuppressNestedUnionMembersVisibility,
				_ => null,
			};

			if (descriptor is null || !IsUnionMembersProvider(context, diagnostic))
				continue;

			context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
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
