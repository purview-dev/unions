using Microsoft.CodeAnalysis;
using Purview.Unions.SourceGenerator.Models;

namespace Purview.Unions.SourceGenerator.Discovery;

/// <summary>
/// Converts an opted-in attribute target into the immutable <see cref="UnionModel"/> the pipeline caches.
/// </summary>
/// <remarks>
/// The semantic decisions — declaration kind, case set, accessibility and every finding — are made by
/// <see cref="UnionAnalysis"/>, which the analyzer uses as well. This type only carries the result into the
/// pipeline, so there is no second implementation of the union rules.
/// </remarks>
static class UnionDiscovery
{
	/// <summary>
	/// Creates the union model for a single attribute target, carrying the findings that decide whether the
	/// target can be processed.
	/// </summary>
	/// <param name="context">The attribute syntax context supplied by the incremental pipeline.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A result carrying the model, or a result that only carries blocking findings.</returns>
	public static GeneratorResult<UnionModel> CreateTarget(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (context.TargetSymbol is not INamedTypeSymbol union)
			return GeneratorResult<UnionModel>.Empty;

		var analysis = UnionAnalysis.Analyze(union, context.SemanticModel.Compilation, cancellationToken);

		// The findings travel with the result so GeneratorResult.ShouldProcess applies the shared blocking
		// policy. They are not reported from here: the analyzer reports the per-target rules, and the
		// generator reports only UNI9000, so no rule is reported twice.
		return GeneratorResult<UnionModel>.Create(
			analysis.CanGenerate ? analysis.Model : UnionModel.Empty,
			analysis.Diagnostics.AsImmutableArray()
		);
	}
}
