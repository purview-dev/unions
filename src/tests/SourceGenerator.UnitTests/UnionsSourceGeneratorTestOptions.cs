namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Options shared by the unions generator, analyzer and code-fix tests.
/// </summary>
/// <remarks>
/// The options derive from <c>AnalyzerTestOptions</c> so the same type serves the generator, analyzer
/// and code-fix bases, keeping every host under test against identical compilation settings.
/// </remarks>
public record UnionsSourceGeneratorTestOptions : AnalyzerTestOptions
{
	/// <summary>
	/// The MSBuild/analyzer-config property that disables the generator.
	/// </summary>
	public const string DisableGeneratorProperty = "DisableUnionsSourceGenerator";

	/// <summary>
	/// The hint names of sources that are not the primary generated union files, excluded from the
	/// primary generated-source assertions.
	/// </summary>
	public static readonly string[] InfrastructureHintNames = ["EmbeddedAttribute.g.cs", "UnionPolyfills.g.cs"];

	/// <summary>
	/// Initializes the options used by the generator tests.
	/// </summary>
	public UnionsSourceGeneratorTestOptions()
	{
		// The pinned Roslyn 5.9.0 package's Latest is C# 14; the union syntax it must parse is exposed under
		// Preview there. This is a test-harness detail, not a consumer requirement: the .NET 11 SDK compiler
		// accepts C# 15 unions with LangVersion=latest, and a net11.0 project needs no switch at all.
		LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview;

		AdditionalAssemblyTypes = AdditionalAssemblyTypes.AddRange(
			typeof(GenerateUnionAttribute),
			typeof(Union<int, string>)
		);
		AdditionalNamespaces = AdditionalNamespaces.Add("Purview.Unions");

		DisableSourceGeneratorPropertyName = DisableGeneratorProperty;
		ExcludeGeneratedSourceHintNames = [.. InfrastructureHintNames];
	}
}
