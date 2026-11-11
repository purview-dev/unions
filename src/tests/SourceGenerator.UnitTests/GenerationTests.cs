namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Shape, determinism and declaration-kind coverage for the generated source.
/// </summary>
public sealed class GenerationTests
	: TUnitSourceGeneratorTestBase<UnionsSourceGenerator, UnionsSourceGeneratorTestOptions>
{
	[Test]
	public async Task SurfaceA_EmitsTheNonBoxingUnionShape(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.IntOrString.g.cs");
		await Assert.That(generated).IsNotNull();

		await Assert.That(generated!).ContainsGeneratedCode("[global::System.Runtime.CompilerServices.Union]");
		await Assert.That(generated!).ContainsGeneratedCode(": global::System.Runtime.CompilerServices.IUnion");
		await Assert.That(generated!).ContainsGeneratedCode("int _case0;");
		await Assert.That(generated!).ContainsGeneratedCode("string? _case1;");
		await Assert.That(generated!).ContainsGeneratedCode("public IntOrString(int value)");
		await Assert.That(generated!).ContainsGeneratedCode("public object? Value");
		await Assert.That(generated!).ContainsGeneratedCode("public bool HasValue");
		await Assert.That(generated!).ContainsGeneratedCode("public bool TryGetValue(out int value)");
	}

	[Test]
	public async Task SurfaceB_EmitsConstructorAndPartialFactoryImplementations(CancellationToken cancellationToken)
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
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.Option_1.g.cs");
		await Assert.That(generated).IsNotNull();

		await Assert
			.That(generated!)
			.ContainsGeneratedCode("internal static partial global::Test.Option<T> Some(global::Test.Some<T> value)");
		await Assert
			.That(generated!)
			.ContainsGeneratedCode("internal static partial global::Test.Option<T> None(global::Test.None value)");
		await Assert.That(generated!).ContainsGeneratedCode("public Option(global::Test.Some<T> value)");
	}

	[Test]
	public async Task SurfaceB_GivenInternalUnionAndInternalCases_GeneratesAndCompiles(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			internal readonly record struct Some<T>(T Value);
			internal readonly record struct None;

			[GenerateUnion]
			internal readonly partial struct Option<T>
			{
				internal static partial Option<T> Some(Some<T> value);
				internal static partial Option<T> None(None value);
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.Option_1.g.cs");
		await Assert.That(generated).IsNotNull();

		await Assert
			.That(generated!)
			.ContainsGeneratedCode("internal static partial global::Test.Option<T> Some(global::Test.Some<T> value)");
		await Assert.That(generated!).ContainsGeneratedCode("public Option(global::Test.Some<T> value)");
	}

	[Test]
	public async Task SurfaceC_EmitsProviderFactoryImplementations(CancellationToken cancellationToken)
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
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.Outcome_1.g.cs");
		await Assert.That(generated).IsNotNull();

		await Assert
			.That(generated!)
			.ContainsGeneratedCode("internal static global::Test.Outcome<T> CreateCase0(T? value)");
		await Assert
			.That(generated!)
			.ContainsGeneratedCode(
				"internal static global::Test.Outcome<T> CreateCase1(global::System.Exception? value)"
			);
	}

	[Test]
	public async Task Determinism_GivenTheSameInput_ProducesIdenticalSource(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		using var first = await GenerateAsync(source, cancellationToken);
		using var second = await GenerateAsync(source, cancellationToken);

		var firstSource = first.GetSource("Test.IntOrString.g.cs");
		var secondSource = second.GetSource("Test.IntOrString.g.cs");

		await Assert.That(secondSource).IsEqualTo(firstSource);
	}

	[Test]
	[Arguments("public partial struct Sample")]
	[Arguments("public readonly partial struct Sample")]
	[Arguments("public partial class Sample")]
	[Arguments("public partial record struct Sample")]
	[Arguments("public partial record class Sample")]
	public async Task DeclarationKind_GivenSupportedKind_Generates(
		string declaration,
		CancellationToken cancellationToken
	)
	{
		var source = $$"""
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			{{declaration}};

			public static class Usage
			{
				public static Sample FromInt() => 42;
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task NullableValueTypeCase_ExposesTheUnderlyingType(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int?), typeof(Guid?))]
			public readonly partial struct MaybeValue;

			public static class Usage
			{
				public static MaybeValue FromInt() => 42;
				public static MaybeValue Empty() => (int?)null;
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.MaybeValue.g.cs");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("public MaybeValue(int? value)");
		await Assert.That(generated!).ContainsGeneratedCode("public bool TryGetValue(out int value)");
	}

	[Test]
	public async Task NestedUnion_IsEmittedInsideItsContainingTypes(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public static partial class Outer
			{
				[GenerateUnion(typeof(int), typeof(string))]
				public readonly partial struct Inner;
			}

			public static class Usage
			{
				public static Outer.Inner FromInt() => 42;
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);
		result.AssertNoCompilationErrors();

		var generated = result.GetSource("Test.Outer.Inner.g.cs");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("partial class Outer");
		await Assert.That(generated!).ContainsGeneratedCode("partial struct Inner");
	}

	[Test]
	public async Task NestedUnionCase_IsAUnionCase(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct Inner;

			[GenerateUnion(typeof(int), typeof(Inner))]
			public readonly partial struct Outer;

			public static class Usage
			{
				public static Outer FromInt() => 42;
				public static Outer FromInner() => new Inner("text");
			}
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task GeneratedUnionSource_ReferencesNoCompilerHostAssemblies(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		using var result = await GenerateAsync(source, cancellationToken);

		var generated = result.GetSource("Test.IntOrString.g.cs");

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).DoesNotContain("Microsoft.CodeAnalysis");
		await Assert.That(generated!).DoesNotContain("Purview.SourceGeneratorFramework");
	}
}
