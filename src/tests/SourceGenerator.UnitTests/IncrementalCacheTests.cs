using Microsoft.CodeAnalysis;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Incremental-correctness tests: they assert which pipeline stages are recomputed and which are reused,
/// rather than only comparing generated text.
/// </summary>
public sealed class IncrementalCacheTests
	: TUnitSourceGeneratorTestBase<UnionsSourceGenerator, UnionsSourceGeneratorTestOptions>
{
	const string IntOrString = """
		using Purview.Unions;

		namespace Test;

		[GenerateUnion(typeof(int), typeof(string))]
		public readonly partial struct IntOrString;
		""";

	const string IntOrStringAndGuid = """
		using Purview.Unions;

		namespace Test;

		[GenerateUnion(typeof(int), typeof(string))]
		public readonly partial struct IntOrString;

		[GenerateUnion(typeof(int), typeof(string))]
		public readonly partial struct GuidOrString;
		""";

	[Test]
	public async Task FirstRun_AllStagesAreNew(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([IntOrString])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[0]).AllStepsNew();
	}

	[Test]
	public async Task IdenticalRerun_ReusesTheAttributeStage(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([IntOrString]), new IncrementalRunInput([IntOrString])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateUnionAttribute");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
		await Assert.That(result.Runs[1]).StepIsCached("GetUnionPolyfillRequest");
	}

	[Test]
	public async Task AddingATarget_InvalidatesOnlyTheAttributeStage(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([IntOrString]), new IncrementalRunInput([IntOrStringAndGuid])],
			cancellationToken: cancellationToken
		);

		await Assert
			.That(result.Runs[1])
			.HasStepReason("ForAttribute_GenerateUnionAttribute", IncrementalStepRunReason.New);
	}

	[Test]
	public async Task DisablingTheGenerator_ChangesOnlyTheConfigurationStage(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[
				new IncrementalRunInput([IntOrString]),
				new IncrementalRunInput(
					[IntOrString],
					[($"build_property.{UnionsSourceGeneratorTestOptions.DisableGeneratorProperty}", "true")]
				),
			],
			cancellationToken: cancellationToken
		);

		await Assert
			.That(result.Runs[1])
			.HasStepReason("GetGenerationContext_EmptyCapabilities", IncrementalStepRunReason.Modified);
	}
}
