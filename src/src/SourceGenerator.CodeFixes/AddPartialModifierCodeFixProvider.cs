using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.Unions.SourceGenerator.CodeFixes;

/// <summary>
/// Adds the missing <c>partial</c> modifier to a union declaration reported as <c>UNI0001</c>.
/// </summary>
/// <remarks>
/// The generator adds the union's constructors, storage and access members as a second partial
/// declaration, so a declaration that is not <c>partial</c> cannot be generated for. Adding the modifier is
/// the whole fix.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddPartialModifierCodeFixProvider))]
[Shared]
public sealed class AddPartialModifierCodeFixProvider : CodeFixProvider
{
	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds => [UnionsDiagnosticIds.UnionMustBePartial];

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

			if (declaration is null || declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Add 'partial' modifier",
					createChangedDocument: cancellationToken =>
						AddPartialAsync(context.Document, declaration, cancellationToken),
					equivalenceKey: nameof(AddPartialModifierCodeFixProvider)
				),
				diagnostic
			);
		}
	}

	static async Task<Document> AddPartialAsync(
		Document document,
		TypeDeclarationSyntax declaration,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);

		var updated = declaration.WithModifiers(declaration.Modifiers.Add(partialToken));

		return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
	}
}
