using Microsoft.CodeAnalysis;
using Purview.Unions.SourceGenerator.Common;
using Purview.Unions.SourceGenerator.Diagnostics;
using Purview.Unions.SourceGenerator.Discovery;
using Purview.Unions.SourceGenerator.Emit;
using Purview.Unions.SourceGenerator.Models;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Generates the C# 15 non-boxing custom-union shape for declarations opted in with
/// <c>[GenerateUnion]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator is explicit opt-in and does no work for compilations that never apply the attribute:
/// discovery runs through <c>ForAttributeWithMetadataName</c>.
/// </para>
/// <para>
/// The pipeline converts Roslyn symbols into immutable, value-equatable models during discovery, so no
/// syntax nodes, symbols, semantic models or compilations are held in cached pipeline state. Each union is
/// emitted from its own source output, and the <c>CodeWriter</c> is created inside that callback.
/// </para>
/// </remarks>
[Generator]
public sealed class UnionsSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// The metadata name of the compiler's union attribute, used to decide whether the .NET 10 polyfills
	/// are needed.
	/// </summary>
	const string UnionAttributeMetadataName = "System.Runtime.CompilerServices.UnionAttribute";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterEmbeddedAttribute<UnionsSourceGenerator>();

		var generationContext = IncrementalPipeline.DefaultGenerationContextValueProvider<UnionsSourceGenerator>(
			context,
			PropertyLibrary.DisableGeneratorProperty
		);

		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			UnionsTypeLibrary.GenerateUnionAttribute,
			static (attributeContext, cancellationToken) =>
				UnionDiscovery.CreateTarget(attributeContext, cancellationToken)
		);

		// One source output per union. The analyzer reports the per-target diagnostics; this callback
		// reports only an unexpected generator failure.
		var withContext = targets.Combine(generationContext);
		context.RegisterSourceOutput(
			withContext,
			static (sourceProductionContext, item) => ExecuteSafely(sourceProductionContext, item.Left, item.Right)
		);

		// The .NET 10 polyfills are emitted only when the compiler's union types are absent, at least one
		// union opted in, and generation is enabled. A net11.0 compilation already has the real types, so
		// it can never receive a second definition.
		var hasUnionAttribute = context
			.CompilationProvider.Select(
				static (compilation, _) => compilation.GetTypeByMetadataName(UnionAttributeMetadataName) is not null
			)
			.WithTrackingName("GetHasUnionAttribute");

		var polyfill = hasUnionAttribute
			.Combine(generationContext)
			.Combine(targets.Collect())
			.Select(
				static (item, _) =>
					new UnionPolyfillRequest(
						Emit: !item.Left.Right.Settings.IsSourceGeneratorDisabled
							&& item.Right.Length > 0
							&& !item.Left.Left,
						Context: item.Left.Right
					)
			)
			.WithTrackingName("GetUnionPolyfillRequest");

		context.RegisterSourceOutput(
			polyfill,
			static (sourceProductionContext, request) =>
			{
				if (request.Emit)
					EmitPolyfillsSafely(sourceProductionContext, request.Context);
			}
		);
	}

	/// <summary>
	/// Runs the emitter, reporting an unexpected failure instead of letting it escape.
	/// </summary>
	/// <remarks>
	/// An exception escaping a generator surfaces as <c>CS8785</c> and discards <em>all</em> generated output
	/// for the compilation: one malformed union removes every other union's members and produces a cascade of
	/// unrelated errors with no indication of the cause. <see cref="OperationCanceledException"/> is
	/// deliberately not caught: the IDE cancels generation on every keystroke.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "This is the generator's last line of defence. Any exception that escapes discards "
			+ "every generated source in the compilation (CS8785) and stops IDE analysis (AD0001), so the "
			+ "catch is deliberately broad. Cancellation is rethrown."
	)]
	static void ExecuteSafely(
		SourceProductionContext context,
		GeneratorResult<UnionModel> result,
		GeneratorContext generationContext
	)
	{
		if (!result.ShouldProcess || generationContext.Settings.IsSourceGeneratorDisabled)
			return;

		try
		{
			UnionEmitter.Execute(context, result.Value, generationContext);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.UnhandledException,
					location: null,
					result.Value.FullyQualifiedName ?? "a union",
					exception.Message
				)
			);
		}
	}

	/// <summary>
	/// Runs the polyfill emitter, reporting an unexpected failure instead of letting it escape.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "The same last line of defence as the union emitter: an escaping exception discards "
			+ "every generated source in the compilation. Cancellation is rethrown."
	)]
	static void EmitPolyfillsSafely(SourceProductionContext context, GeneratorContext generationContext)
	{
		try
		{
			PolyfillEmitter.Emit(context, generationContext);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.UnhandledException,
					location: null,
					"the .NET 10 union polyfills",
					exception.Message
				)
			);
		}
	}

	/// <summary>
	/// The decision to emit the .NET 10 polyfills, carried through the pipeline with the generation context.
	/// </summary>
	readonly record struct UnionPolyfillRequest(bool Emit, GeneratorContext Context);
}
