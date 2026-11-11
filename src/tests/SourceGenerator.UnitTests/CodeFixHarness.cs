using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Applies a shipped code fix against a synthetic diagnostic at the union declaration.
/// </summary>
/// <remarks>
/// The framework's code-fix test base is driven by an analyzer, while these fixes are driven directly so both
/// can be proven without a second reporting host: <c>UNI0001</c> is reported by the analyzer, and
/// <c>UNI0007</c> is never reported by the shipped generator (it emits the attribute itself). Composing the
/// diagnostic here keeps both tests identical in shape and independent of a reporting host.
/// </remarks>
static class CodeFixHarness
{
	/// <summary>
	/// Applies the first fix offered by <typeparamref name="TProvider"/> to the first type declaration in the
	/// source.
	/// </summary>
	/// <typeparam name="TProvider">The code-fix provider to run.</typeparam>
	/// <param name="source">The source containing the union declaration.</param>
	/// <param name="descriptor">The descriptor whose id the synthetic diagnostic carries.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>The rewritten source, or the original source when no fix was offered.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "Test harness: composing a Roslyn compilation, an AdhocWorkspace and a code fix necessarily touches many Roslyn types."
	)]
	public static async Task<string> ApplyAsync<TProvider>(
		string source,
		DiagnosticDescriptor descriptor,
		CancellationToken cancellationToken
	)
		where TProvider : CodeFixProvider, new()
	{
		CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
		CSharpCompilationOptions compilationOptions = new(
			OutputKind.DynamicallyLinkedLibrary,
			nullableContextOptions: NullableContextOptions.Enable
		);
		var references = MetadataReferences();

		var tree = CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: cancellationToken);
		var compilation = CSharpCompilation.Create("CodeFixTest", [tree], references, compilationOptions);

		var declaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.First();
		var diagnostic = Diagnostic.Create(descriptor, declaration.Identifier.GetLocation());

		var projectId = ProjectId.CreateNewId();
		using AdhocWorkspace workspace = new();
		workspace.AddProject(
			ProjectInfo.Create(
				projectId,
				VersionStamp.Create(),
				compilation.AssemblyName ?? "CodeFixTest",
				compilation.AssemblyName ?? "CodeFixTest",
				LanguageNames.CSharp,
				parseOptions: parseOptions,
				compilationOptions: compilationOptions,
				metadataReferences: references
			)
		);

		var documentId = workspace.AddDocument(projectId, "Test.cs", SourceText.From(source, Encoding.UTF8)).Id;

		// Re-resolve from the workspace's current solution: a Document captured before the project was
		// fully added would resolve an empty compilation.
		var document = workspace.CurrentSolution.GetDocument(documentId)!;

		List<CodeAction> actions = [];
		await new TProvider().RegisterCodeFixesAsync(
			new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken)
		);

		if (actions.Count == 0)
			return source;

		var operations = await actions[0].GetOperationsAsync(cancellationToken);
		var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
		var changedDocument = changedSolution.GetDocument(documentId)!;

		return (await changedDocument.GetTextAsync(cancellationToken)).ToString();
	}

	static ImmutableArray<MetadataReference> MetadataReferences()
	{
		var builder = ImmutableArray.CreateBuilder<MetadataReference>();

		var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty).Split(
			Path.PathSeparator,
			StringSplitOptions.RemoveEmptyEntries
		);

		foreach (var path in trusted)
			builder.Add(MetadataReference.CreateFromFile(path));

		builder.Add(MetadataReference.CreateFromFile(typeof(GenerateUnionAttribute).Assembly.Location));

		return builder.ToImmutable();
	}
}
