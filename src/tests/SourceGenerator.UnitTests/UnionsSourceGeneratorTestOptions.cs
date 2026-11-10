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
		// A custom union and [Union] are C# 15 features.
		LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview;

		AdditionalAssemblyTypes = AdditionalAssemblyTypes.AddRange(
			typeof(global::Purview.Unions.GenerateUnionAttribute),
			typeof(global::Purview.Unions.Union<int, string>)
		);
		AdditionalNamespaces = AdditionalNamespaces.Add("Purview.Unions");

		DisableSourceGeneratorPropertyName = DisableGeneratorProperty;
		ExcludeGeneratedSourceHintNames = [.. InfrastructureHintNames];
	}
}
