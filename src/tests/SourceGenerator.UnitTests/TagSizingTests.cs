using Purview.Unions.SourceGenerator.Common;
using Purview.Unions.SourceGenerator.Diagnostics;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// The tag-sizing rule and the catalogue's unreachable-by-source rule.
/// </summary>
/// <remarks>
/// <c>UNI0006</c> is a defensive rule: the generator sizes the tag automatically, so the finding can only
/// occur beyond <see cref="PropertyLibrary.MaxSupportedCases"/> cases. A source fixture with that many
/// cases would be tens of thousands of lines, so the condition and the descriptor are asserted directly.
/// </remarks>
public sealed class TagSizingTests
{
	[Test]
	public async Task TagType_IsTheSmallestThatAddressesTheCases(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await Assert.That(PropertyLibrary.GetTagTypeName(1)).IsEqualTo("byte");
		await Assert.That(PropertyLibrary.GetTagTypeName(byte.MaxValue)).IsEqualTo("byte");
		await Assert.That(PropertyLibrary.GetTagTypeName(byte.MaxValue + 1)).IsEqualTo("ushort");
		await Assert.That(PropertyLibrary.GetTagTypeName(ushort.MaxValue)).IsEqualTo("ushort");
	}

	[Test]
	public async Task MaxSupportedCases_FitsTheWidestTag(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var maxSupportedCases = PropertyLibrary.MaxSupportedCases;

		await Assert.That(maxSupportedCases).IsEqualTo(ushort.MaxValue);
	}

	[Test]
	public async Task TooManyCases_IsAnErrorInTheCatalogue(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await Assert.That(DiagnosticLibrary.TooManyCases.Id).IsEqualTo("UNI0006");
		await Assert
			.That(DiagnosticLibrary.TooManyCases.DefaultSeverity)
			.IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
	}

	[Test]
	public async Task UnionAttributeRequired_IsInTheCatalogueForTheFallbackPath(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// The shipped generator emits [Union] itself, so this rule is not reported. It stays in the
		// catalogue, with its code fix, for the documented fallback.
		await Assert.That(DiagnosticLibrary.UnionAttributeRequired.Id).IsEqualTo("UNI0007");
		await Assert.That(DiagnosticLibrary.IsBlocking(DiagnosticLibrary.UnionAttributeRequired)).IsTrue();
	}

	[Test]
	public async Task SingleCaseUnion_IsTheOnlyNonBlockingRule(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		foreach (var descriptor in DiagnosticLibrary.AllDescriptors)
		{
			var expected = descriptor.Id == DiagnosticLibrary.SingleCaseUnion.Id;
			await Assert.That(DiagnosticLibrary.IsBlocking(descriptor)).IsEqualTo(!expected);
		}
	}
}
