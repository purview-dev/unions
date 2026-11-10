using Purview.Unions.SourceGenerator.CodeFixes;
using Purview.Unions.SourceGenerator.Diagnostics;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Tests for the two shipped code fixes: <c>UNI0001</c> adds <c>partial</c>, and <c>UNI0007</c> adds the
/// compiler's <c>[Union]</c> attribute on the documented fallback path.
/// </summary>
public sealed class CodeFixTests
{
	[Test]
	public async Task DiagnosticIds_MatchTheGeneratorCatalogue()
	{
		await Assert.That(UnionsDiagnosticIds.UnionMustBePartial).IsEqualTo(DiagnosticLibrary.UnionMustBePartial.Id);
		await Assert
			.That(UnionsDiagnosticIds.UnionAttributeRequired)
			.IsEqualTo(DiagnosticLibrary.UnionAttributeRequired.Id);
	}

	[Test]
	public async Task UNI0001_AddsThePartialModifier(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly struct IntOrString;
			""";

		var fixedSource = await CodeFixHarness.ApplyAsync<AddPartialModifierCodeFixProvider>(
			source,
			DiagnosticLibrary.UnionMustBePartial,
			cancellationToken
		);

		await Assert.That(fixedSource).Contains("partial struct IntOrString");
	}

	[Test]
	public async Task UNI0007_AddsTheUnionAttribute(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var fixedSource = await CodeFixHarness.ApplyAsync<AddUnionAttributeCodeFixProvider>(
			source,
			DiagnosticLibrary.UnionAttributeRequired,
			cancellationToken
		);

		await Assert.That(fixedSource).Contains("global::System.Runtime.CompilerServices.Union");
	}
}
