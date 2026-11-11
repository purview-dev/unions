using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Purview.Unions.SourceGenerator;

namespace Purview.Unions.Benchmarks;

/// <summary>
/// Measures the incremental generator against an in-memory compilation containing N union declarations.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ColdRun"/> constructs a fresh driver for every operation, which is the first generation in an
/// IDE or build. <see cref="IncrementalRun"/> reuses the driver and compilation after a warm run, so it
/// measures the pipeline's cached path. The gap between the two is the value of the value-equatable
/// pipeline, not an accident of the benchmark.
/// </para>
/// <para>
/// This measures the unmerged generator assembly, which is the same code the package ships; IL merging only
/// internalizes the framework and does not change discovery or emission.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GeneratorBenchmarks
{
	// The pinned Roslyn 5.9.0 package's Latest is C# 14; parse under Preview so the C# 15 union syntax is
	// available. The SDK compiler that builds consumers accepts it with LangVersion=latest.
	readonly CSharpParseOptions _parseOptions = new(LanguageVersion.Preview);
	MetadataReference[] _references = [];
	SyntaxTree _syntaxTree = null!;
	CSharpCompilation _compilation = null!;
	GeneratorDriver _driver = null!;

	[Params(1, 10, 100)]
	public int UnionCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_references = BuildReferences();
		_syntaxTree = CSharpSyntaxTree.ParseText(BuildSource(UnionCount), _parseOptions);
		_compilation = CreateCompilation();
		_driver = CSharpGeneratorDriver.Create(
			[new UnionsSourceGenerator().AsSourceGenerator()],
			parseOptions: _parseOptions
		);

		// Warm the incremental pipeline so IncrementalRun measures the cached path, and fail the setup
		// (rather than silently benchmarking a broken generator) if the source does not compile.
		_driver = _driver.RunGeneratorsAndUpdateCompilation(_compilation, out _, out var diagnostics);
		ThrowOnError(diagnostics);
	}

	[Benchmark(Baseline = true)]
	public void ColdRun()
	{
		var compilation = CreateCompilation();
		var driver = CSharpGeneratorDriver.Create(
			[new UnionsSourceGenerator().AsSourceGenerator()],
			parseOptions: _parseOptions
		);
		driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
	}

	[Benchmark]
	public void IncrementalRun()
	{
		_driver = _driver.RunGeneratorsAndUpdateCompilation(_compilation, out _, out _);
	}

	CSharpCompilation CreateCompilation() =>
		CSharpCompilation.Create(
			"Purview.Unions.Benchmarks.Generated",
			[_syntaxTree],
			_references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

	static MetadataReference[] BuildReferences()
	{
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
		{
			foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
				paths.Add(path);
		}

		// The compilation must see [GenerateUnion], exactly as a consumer's compilation does.
		paths.Add(typeof(GenerateUnionAttribute).Assembly.Location);

		return [.. paths.Select(static path => MetadataReference.CreateFromFile(path))];
	}

	static string BuildSource(int unionCount)
	{
		var builder = new StringBuilder();
		builder.AppendLine("using Purview.Unions;");
		builder.AppendLine("namespace Benchmarks.Generated;");

		for (var index = 0; index < unionCount; index++)
		{
			builder.AppendLine("[GenerateUnion(typeof(int), typeof(string))]");
			builder.AppendLine(CultureInfo.InvariantCulture, $"public readonly partial struct BenchUnion{index};");
		}

		return builder.ToString();
	}

	static void ThrowOnError(IEnumerable<Diagnostic> diagnostics)
	{
		var errors = diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

		if (errors.Length > 0)
		{
			throw new InvalidOperationException(
				"The generator reported errors: "
					+ string.Join(Environment.NewLine, errors.Select(static error => error.ToString()))
			);
		}
	}
}
