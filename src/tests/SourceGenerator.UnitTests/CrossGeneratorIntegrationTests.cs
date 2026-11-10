namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Cross-generator integration, recorded as skipped tests until the downstream repositories read a union's
/// source case list.
/// </summary>
/// <remarks>
/// Roslyn generators never see each other's output. A union generated here has its constructors and access
/// members in generated output, so <c>Purview.Results</c> and <c>Purview.Telemetry</c> cannot discover its
/// cases from the constructors. Surface C (a nested <c>IUnionMembers</c> provider) is source-visible and is
/// what those generators need; the follow-ups are recorded in <c>docs/wiki/Interop.md</c>.
/// </remarks>
public sealed class CrossGeneratorIntegrationTests
{
	[Test]
	[Skip(
		"Purview.Results' [GenerateResult] discovery reads generated constructors, which a generator cannot see. Follow-up: purview-dev/results should read the source case list. See docs/wiki/Interop.md."
	)]
	public async Task GeneratedUnion_AsAResultsErrorUnion_IsDiscovered(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await Task.CompletedTask;
	}

	[Test]
	[Skip(
		"A telemetry parameter typed as a surface-A union is not discovered because its cases live only in generated constructors. Surface C is source-visible and is the documented route; see docs/wiki/Interop.md."
	)]
	public async Task GeneratedUnion_AsATelemetryParameter_IsDiscovered(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await Task.CompletedTask;
	}
}
