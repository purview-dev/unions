using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Unions.SourceGenerator.Diagnostics;
using Purview.Unions.SourceGenerator.Discovery;

namespace Purview.Unions.SourceGenerator.Analyzers;

/// <summary>
/// Raises the per-target union diagnostics so consumers see them in the IDE and in build output.
/// </summary>
/// <remarks>
/// The analyzer and the source generator share one implementation of the rules
/// (<see cref="UnionAnalysis"/>, <see cref="DiagnosticLibrary"/>). The analyzer is the single reporting
/// source for the per-target rules; the generator uses the same findings only to decide whether generation
/// can continue, and reports only <c>UNI9000</c> for an unexpected failure. That split keeps the blocking
/// decision consistent between the two hosts and never reports a rule twice.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionsDiagnosticAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticLibrary.AllDescriptors;

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1062:Validate arguments of public methods",
		Justification = "The Roslyn contract states that AnalysisContext is never null."
	)]
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		if (context.Symbol is not INamedTypeSymbol symbol)
			return;

		// Cheap gate before the shared analysis: most types in a compilation do not opt in.
		if (!UnionAnalysis.HasGenerateUnionAttribute(symbol))
			return;

		var analysis = UnionAnalysis.Analyze(symbol, context.Compilation, context.CancellationToken);
		if (analysis.Diagnostics.IsEmpty)
			return;

		foreach (var finding in analysis.Diagnostics)
			context.ReportDiagnostic(finding.ToDiagnostic());
	}
}
