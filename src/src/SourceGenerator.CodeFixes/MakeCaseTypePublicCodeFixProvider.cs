using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.Unions.SourceGenerator.CodeFixes;

/// <summary>
/// Makes a union case type public for <c>UNI0005</c>, including any containing type that is not public.
/// </summary>
/// <remarks>
/// A public union's creation members are public API, so its case types must be public. The fix adds
/// <c>public</c> to the case type and every containing type in the declaration chain; an internal union
/// does not report <c>UNI0005</c> at all, so this fix is only offered where the union's effective
/// accessibility actually requires it.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeCaseTypePublicCodeFixProvider))]
[Shared]
public sealed class MakeCaseTypePublicCodeFixProvider : CodeFixProvider
{
	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds => [UnionsDiagnosticIds.UnionCaseTypeNotAccessible];

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
			if (!diagnostic.Location.IsInSource)
				continue;

			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
			var declaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
			if (declaration is null)
				continue;

			var chain = declaration.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().ToArray();
			if (chain.Any(static type => type.Modifiers.Any(SyntaxKind.FileKeyword)))
				continue;

			if (chain.All(static type => type.Modifiers.Any(SyntaxKind.PublicKeyword)))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Make '{declaration.Identifier.ValueText}' public",
					createChangedDocument: cancellationToken =>
						MakePublicAsync(context.Document, declaration, cancellationToken),
					equivalenceKey: nameof(MakeCaseTypePublicCodeFixProvider)
				),
				diagnostic
			);
		}
	}

	static async Task<Document> MakePublicAsync(
		Document document,
		TypeDeclarationSyntax declaration,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var declarations = declaration.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().ToArray();
		var updatedRoot = root.ReplaceNodes(declarations, static (_, rewritten) => AddPublicModifier(rewritten));

		return document.WithSyntaxRoot(updatedRoot);
	}

	static TypeDeclarationSyntax AddPublicModifier(TypeDeclarationSyntax declaration)
	{
		if (declaration.Modifiers.Any(SyntaxKind.PublicKeyword) || declaration.Modifiers.Any(SyntaxKind.FileKeyword))
		{
			return declaration;
		}

		var existing = declaration.Modifiers;
		var leading = existing.Count > 0 ? existing[0].LeadingTrivia : declaration.Keyword.LeadingTrivia;
		var publicToken = SyntaxFactory
			.Token(SyntaxKind.PublicKeyword)
			.WithLeadingTrivia(leading)
			.WithTrailingTrivia(SyntaxFactory.Space);

		var kept = existing
			.Where(static modifier => !IsAccessibilityModifier(modifier.Kind()))
			.Select(static modifier => modifier.WithLeadingTrivia(default(SyntaxTriviaList)));

		var modified = declaration.WithModifiers(SyntaxFactory.TokenList(publicToken).AddRange(kept));

		// With no existing modifiers the leading trivia lived on the declaration keyword, so move it to
		// the new first token instead.
		return existing.Count == 0
			? modified.WithKeyword(declaration.Keyword.WithLeadingTrivia(default(SyntaxTriviaList)))
			: modified;
	}

	static bool IsAccessibilityModifier(SyntaxKind kind) =>
		kind
			is SyntaxKind.PublicKeyword
				or SyntaxKind.ProtectedKeyword
				or SyntaxKind.InternalKeyword
				or SyntaxKind.PrivateKeyword
				or SyntaxKind.FileKeyword;
}
