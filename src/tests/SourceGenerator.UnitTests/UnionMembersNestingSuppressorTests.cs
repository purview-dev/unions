using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Unions.SourceGenerator.Analyzers;

// The stand-in analyzers below report the real CA1034, CA1515 and CA1000 ids and live in a test assembly.
// These are analyzer-authoring rules for a shipped component; the stand-ins are never packed, and the test
// assembly already references Workspaces for the code-fix tests.
#pragma warning disable RS1029 // CA1034, CA1515 and CA1000 are the real rules under test.
#pragma warning disable RS1036 // This is a test assembly, not an analyzer project.
#pragma warning disable RS1038 // The test assembly references Workspaces for the code-fix tests.
#pragma warning disable RS1041 // The test assembly targets net10.0 and net11.0.
#pragma warning disable RS2008 // The stand-in is not shipped and has no release tracking.

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Tests for <see cref="UnionMembersNestingSuppressor"/>.
/// </summary>
/// <remarks>
/// CA1034, CA1515 and CA1000 are reported by stand-in analyzers rather than by the real CodeQuality rules: a
/// suppressor only sees a diagnostic id and a location, so what is under test is which declarations the
/// suppression lands on, not the real rules' implementation. The CA1034 and CA1515 stand-ins report on every
/// nested type, and the CA1000 stand-in reports on every static member of a generic type, which is what the
/// real rules do.
/// </remarks>
public sealed class UnionMembersNestingSuppressorTests
{
	[Test]
	public async Task CA1034_GivenUnionMembersProvider_IsSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T>
			{
				public interface IUnionMembers
				{
					object? Value { get; }

					bool HasValue { get; }

					bool TryGetValue(out T value);
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1034");
		await Assert.That(diagnostic.IsSuppressed).IsTrue();
	}

	[Test]
	public async Task CA1034_GivenIUnionMembersOutsideAUnion_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public readonly partial struct NotAUnion
			{
				public interface IUnionMembers { }
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1034");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1034_GivenAnotherNestedTypeInAUnion_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int))]
			public readonly partial struct IntOrString
			{
				public interface OtherNested { }
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1034");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1515_GivenUnionMembersProvider_IsSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T>
			{
				public interface IUnionMembers
				{
					object? Value { get; }

					bool HasValue { get; }

					bool TryGetValue(out T value);
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1515");
		await Assert.That(diagnostic.IsSuppressed).IsTrue();
	}

	[Test]
	public async Task CA1515_GivenIUnionMembersOutsideAUnion_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public readonly partial struct NotAUnion
			{
				public interface IUnionMembers { }
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1515");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1000_GivenGenericGenerateUnionMembersProvider_IsSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T>
			{
				public interface IUnionMembers
				{
					static Outcome<T> Create(T value) => default;
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1000");
		await Assert.That(diagnostic.IsSuppressed).IsTrue();
	}

	[Test]
	public async Task CA1000_GivenHandWrittenUnionMembersProvider_IsSuppressed(CancellationToken cancellationToken)
	{
		// The downstream results test declares its generic union by hand with [Union] rather than
		// [GenerateUnion], so the shape is reproduced here: a self-contained UnionAttribute stands in for
		// the polyfill the generator emits on net10.0 and the BCL supplies on net11.0.
		const string source = """
			namespace System.Runtime.CompilerServices
			{
				[global::System.AttributeUsage(global::System.AttributeTargets.Struct)]
				internal sealed class UnionAttribute : global::System.Attribute { }
			}

			namespace Test
			{
				public readonly record struct ProvidedSome<T>(T Value);

				public readonly record struct ProvidedNone;

				[System.Runtime.CompilerServices.Union]
				public readonly struct ProvidedOption<T> : ProvidedOption<T>.IUnionMembers
				{
					public interface IUnionMembers
					{
						static ProvidedOption<T> Create(ProvidedSome<T> value) => default;

						static ProvidedOption<T> Create(ProvidedNone value) => default;
					}
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);
		var ca1000 = diagnostics.Where(static entry => entry.Id == "CA1000").ToArray();

		await Assert.That(ca1000.Length).IsEqualTo(2);
		await Assert.That(ca1000.All(static entry => entry.IsSuppressed)).IsTrue();
	}

	[Test]
	public async Task CA1000_GivenIUnionMembersOutsideAUnion_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public sealed class Container<T>
			{
				public interface IUnionMembers
				{
					static Container<T> Create(int value) => new();
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1000");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1000_GivenNonCreateStaticMemberInAUnion_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T>
			{
				public interface IUnionMembers
				{
					static Outcome<T> Describe(T value) => default;
				}
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1000");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1000_GivenStaticMemberInANonUnionGenericType_IsNotSuppressed(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Test;

			public sealed class Holder<T>
			{
				public static Holder<T> Of(int value) => new();
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1000");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[Test]
	public async Task CA1000_GivenStaticMemberOnTheUnionItself_IsNotSuppressed(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T>
			{
				public static Outcome<T> Empty() => default;
			}
			""";

		var diagnostics = await AnalyzeAsync(source, cancellationToken);

		var diagnostic = diagnostics.Single(static entry => entry.Id == "CA1000");
		await Assert.That(diagnostic.IsSuppressed).IsFalse();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "Test harness: composing a Roslyn compilation, analyzers and a suppressor necessarily touches many Roslyn types."
	)]
	static async Task<Diagnostic[]> AnalyzeAsync(string source, CancellationToken cancellationToken)
	{
		var tree = CSharpSyntaxTree.ParseText(
			source,
			new CSharpParseOptions(LanguageVersion.Preview),
			cancellationToken: cancellationToken
		);
		var compilation = CSharpCompilation.Create(
			"UnionMembersNestingSuppressorTests",
			[tree],
			References(),
			new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				nullableContextOptions: NullableContextOptions.Enable
			)
		);

		var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
			new NestedTypeCa1034Analyzer(),
			new NestedTypeCa1515Analyzer(),
			new StaticMemberOnGenericTypeCa1000Analyzer(),
			new UnionMembersNestingSuppressor()
		);

		// reportSuppressedDiagnostics keeps a suppressed diagnostic in the results, carrying IsSuppressed.
		// Without it a suppressed diagnostic is filtered out, and "suppressed" would be indistinguishable
		// from "the stand-in never reported it".
		CompilationWithAnalyzersOptions analysisOptions = new(
			new AnalyzerOptions([]),
			onAnalyzerException: null,
			concurrentAnalysis: false,
			logAnalyzerExecutionTime: false,
			reportSuppressedDiagnostics: true
		);

		var diagnostics = await compilation
			.WithAnalyzers(analyzers, analysisOptions)
			.GetAllDiagnosticsAsync(cancellationToken);

		return [.. diagnostics.Where(static diagnostic => diagnostic.Id is "CA1034" or "CA1515" or "CA1000")];
	}

	static ImmutableArray<MetadataReference> References()
	{
		var builder = ImmutableArray.CreateBuilder<MetadataReference>();

		if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
		{
			foreach (var path in trusted.Split(Path.PathSeparator))
				builder.Add(MetadataReference.CreateFromFile(path));
		}

		builder.Add(MetadataReference.CreateFromFile(typeof(GenerateUnionAttribute).Assembly.Location));

		return builder.ToImmutable();
	}

	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NestedTypeCa1034Analyzer : DiagnosticAnalyzer
	{
		static readonly DiagnosticDescriptor Rule = new(
			"CA1034",
			"Nested types should not be visible",
			"Do not nest type {0}",
			"Design",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSymbolAction(
				static symbolContext =>
				{
					if (symbolContext.Symbol is INamedTypeSymbol { ContainingType: not null } type)
					{
						symbolContext.ReportDiagnostic(
							Diagnostic.Create(Rule, type.Locations.FirstOrDefault(), type.Name)
						);
					}
				},
				SymbolKind.NamedType
			);
		}
	}

	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NestedTypeCa1515Analyzer : DiagnosticAnalyzer
	{
		static readonly DiagnosticDescriptor Rule = new(
			"CA1515",
			"Consider making public types internal",
			"Because an application's API isn't typically referenced from outside the assembly, types can be made internal",
			"Design",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSymbolAction(
				static symbolContext =>
				{
					if (symbolContext.Symbol is INamedTypeSymbol { ContainingType: not null } type)
					{
						symbolContext.ReportDiagnostic(
							Diagnostic.Create(Rule, type.Locations.FirstOrDefault(), type.Name)
						);
					}
				},
				SymbolKind.NamedType
			);
		}
	}

	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class StaticMemberOnGenericTypeCa1000Analyzer : DiagnosticAnalyzer
	{
		static readonly DiagnosticDescriptor Rule = new(
			"CA1000",
			"Do not declare static members on generic types",
			"Do not declare static members on generic types",
			"Design",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSymbolAction(
				static symbolContext =>
				{
					var symbol = symbolContext.Symbol;

					if (
						!symbol.IsStatic
						|| symbol
							is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor }
						|| symbol.ContainingType is not { IsGenericType: true }
					)
						return;

					symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations.FirstOrDefault()));
				},
				SymbolKind.Method,
				SymbolKind.Property,
				SymbolKind.Field,
				SymbolKind.Event
			);
		}
	}
}
