using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.Unions.SourceGenerator.CodeFixes;

/// <summary>
/// Adds <c>[System.Runtime.CompilerServices.Union]</c> to a union declaration reported as <c>UNI0007</c>.
/// </summary>
/// <remarks>
/// The shipped generator emits the attribute itself, so this rule is not reported by default. It exists for
/// the documented fallback path in which attribute emission is unavailable and the author must add the
/// attribute by hand. The attribute is written fully qualified so the fix needs no <c>using</c> directive.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddUnionAttributeCodeFixProvider))]
[Shared]
public sealed class AddUnionAttributeCodeFixProvider : CodeFixProvider
{
	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds => [UnionsDiagnosticIds.UnionAttributeRequired];

	/// <inheritdoc />
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc />
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var declaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
				.FirstAncestorOrSelf<TypeDeclarationSyntax>();

			if (declaration is null || HasUnionAttribute(declaration))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Add [Union] attribute",
					createChangedDocument: cancellationToken =>
						AddUnionAttributeAsync(context.Document, declaration, cancellationToken),
					equivalenceKey: nameof(AddUnionAttributeCodeFixProvider)
				),
				diagnostic
			);
		}
	}

	static async Task<Document> AddUnionAttributeAsync(
		Document document,
		TypeDeclarationSyntax declaration,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var attribute = SyntaxFactory
			.AttributeList(
				SyntaxFactory.SingletonSeparatedList(
					SyntaxFactory
						.Attribute(SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.Union"))
						.WithArgumentList(null)
				)
			)
			.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
		var updated = declaration.WithAttributeLists(declaration.AttributeLists.Insert(0, attribute));

		return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
	}

	static bool HasUnionAttribute(TypeDeclarationSyntax declaration)
	{
		foreach (var list in declaration.AttributeLists)
		{
			foreach (var attribute in list.Attributes)
			{
				var name = attribute.Name.ToString();
				var separator = name.LastIndexOf('.');
				var last = separator >= 0 ? name.Substring(separator + 1) : name;

				if (
					string.Equals(last, "Union", StringComparison.Ordinal)
					|| string.Equals(last, "UnionAttribute", StringComparison.Ordinal)
				)
					return true;
			}
		}

		return false;
	}
}
