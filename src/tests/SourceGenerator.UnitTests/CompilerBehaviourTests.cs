#pragma warning disable RSEXPERIMENTAL006 // ITypeSymbol.IsUnion is [Experimental] until C# unions ship.

using Microsoft.CodeAnalysis;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// The Phase 0 compiler experiments, kept as permanent tests.
/// </summary>
/// <remarks>
/// Each test compiles real C# rather than inferring the language's behaviour. Together they establish the
/// design recorded in <c>docs/wiki/Union-Design.md</c>: the compiler accepts a <c>[Union]</c> emitted by the
/// generator, an internal <c>UnionAttribute</c> polyfill is recognised on <c>net10.0</c>, and a
/// <c>net11.0</c> compilation never receives a duplicate.
/// </remarks>
public sealed class CompilerBehaviourTests
	: TUnitSourceGeneratorTestBase<UnionsSourceGenerator, UnionsSourceGeneratorTestOptions>
{
	const string SurfaceA = """
		using Purview.Unions;

		namespace Test;

		[GenerateUnion(typeof(int), typeof(string))]
		public readonly partial struct IntOrString;

		public static class Usage
		{
			public static IntOrString FromInt() => 42;

			public static string Describe(IntOrString value) =>
				value switch
				{
					int number => $"int {number}",
					string text => $"string {text}",
					null => "null",
				};

			public static bool IsInt(IntOrString value) => value is int;
		}
		""";

	[Test]
	public async Task Spike1_GeneratedUnionAttribute_ProducesCompilerUnion(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(SurfaceA, cancellationToken);

		// The union conversion, the exhaustive switch and the type/null patterns all compile, which they
		// only do when the compiler recognises the type as a union.
		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task Spike2_SourceUnionAttribute_WithGeneratedConstructors_ProducesCompilerUnion(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[System.Runtime.CompilerServices.Union]
			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;

			public static class Usage
			{
				public static IntOrString FromInt() => 42;

				public static string Describe(IntOrString value) =>
					value switch
					{
						int number => $"int {number}",
						string text => $"string {text}",
						null => "null",
					};
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task Spike3_PartialFactorySignatures_AreImplemented(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public readonly record struct Some<T>(T Value);
			public readonly record struct None;

			[GenerateUnion]
			public readonly partial struct Option<T>
			{
				internal static partial Option<T> Some(Some<T> value);
				internal static partial Option<T> None(None value);
			}

			public static class Usage
			{
				public static Option<int> Some(int value) => Option<int>.Some(new Some<int>(value));

				public static Option<int> None() => Option<int>.None(new None());
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task Spike4_ProviderBasedCreation_CompilesAndCreates(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
			{
				public interface IUnionMembers
				{
					static Outcome<T> Create(T? value) => Outcome<T>.CreateCase0(value);
					static Outcome<T> Create(Exception? value) => Outcome<T>.CreateCase1(value);

					object? Value { get; }
					bool HasValue { get; }
					bool TryGetValue(out T value);
					bool TryGetValue(out Exception value);
				}
			}

			public static class Usage
			{
				public static Outcome<int> FromValue() => 42;
				public static Outcome<int> FromError() => new InvalidOperationException("boom");
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task Spike5_IsUnion_IsTrueForAGeneratedUnion(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(SurfaceA, cancellationToken);

		result.AssertNoCompilationErrors();

		var union = (INamedTypeSymbol)result.GetTypeByMetadataName("Test.IntOrString")!;

		await Assert.That(union.IsUnion).IsTrue();
	}

#if !NET11_0_OR_GREATER
	[Test]
	public async Task Spike6_Net10_InternalPolyfill_IsRecognised(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(SurfaceA, cancellationToken);

		result.AssertNoCompilationErrors();

		await Assert.That(result.GetSource("UnionPolyfills.g.cs")).IsNotNull();

		var attribute = result.CompilationResult.Compilation.GetTypeByMetadataName(
			"System.Runtime.CompilerServices.UnionAttribute"
		);

		await Assert.That(attribute).IsNotNull();
		await Assert
			.That(
				SymbolEqualityComparer.Default.Equals(
					attribute!.ContainingAssembly,
					result.CompilationResult.Compilation.Assembly
				)
			)
			.IsTrue();
	}
#else
	[Test]
	public async Task Spike6_Net11_UsesTheBclTypes_AndEmitsNoDuplicate(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(SurfaceA, cancellationToken);

		result.AssertNoCompilationErrors();

		await Assert.That(result.GetSource("UnionPolyfills.g.cs")).IsNull();

		var attribute = result.CompilationResult.Compilation.GetTypeByMetadataName(
			"System.Runtime.CompilerServices.UnionAttribute"
		);

		await Assert.That(attribute).IsNotNull();
		await Assert
			.That(
				SymbolEqualityComparer.Default.Equals(
					attribute!.ContainingAssembly,
					result.CompilationResult.Compilation.Assembly
				)
			)
			.IsFalse();

		// No CS0433/CS0436: the compilation reports no error at all, and the type resolves from the BCL.
		await Assert
			.That(
				result
					.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
					.Select(static diagnostic => diagnostic.Id)
					.Where(static id => id is "CS0433" or "CS0436")
			)
			.IsEmpty();
	}
#endif
}
