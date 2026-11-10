using Microsoft.CodeAnalysis;
using Purview.Unions.SourceGenerator.Analyzers;

namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Tests for <see cref="UnionsDiagnosticAnalyzer"/>, the reporting host for the per-target rules.
/// </summary>
public sealed class DiagnosticsTests
	: TUnitDiagnosticAnalyzerTestBase<UnionsDiagnosticAnalyzer, UnionsSourceGeneratorTestOptions>
{
	[Test]
	public async Task SupportedDiagnostics_IsTheWholeCatalogue()
	{
		UnionsDiagnosticAnalyzer analyzer = new();

		await Assert
			.That(
				analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).Order(StringComparer.Ordinal)
			)
			.IsEquivalentTo([
				"UNI0001",
				"UNI0002",
				"UNI0003",
				"UNI0004",
				"UNI0005",
				"UNI0006",
				"UNI0007",
				"UNI0008",
				"UNI0009",
				"UNI0010",
				"UNI0011",
				"UNI0012",
				"UNI0013",
				"UNI0014",
				"UNI0015",
				"UNI0016",
				"UNI9000",
			]);
	}

	[Test]
	public async Task UNI0001_GivenNotPartial_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0001");
	}

	[Test]
	public async Task UNI0001_GivenPartial_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0001");
	}

	[Test]
	public async Task UNI0002_GivenStaticClass_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int))]
			public static class NotAUnion;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0002");
	}

	[Test]
	public async Task UNI0002_GivenStruct_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0002");
	}

	[Test]
	public async Task UNI0003_GivenNoCases_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion]
			public readonly partial struct NoCases;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0003");
	}

	[Test]
	public async Task UNI0003_GivenCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0003");
	}

	[Test]
	public async Task UNI0004_GivenDuplicateCase_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(int))]
			public readonly partial struct IntOrInt;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0004");
	}

	[Test]
	public async Task UNI0004_GivenDistinctCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(long))]
			public readonly partial struct IntOrLong;
			""";

		// int and long are implicitly convertible, so UNI0008 is the finding here; the point is that
		// the cases are not duplicates.
		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0004");
	}

	[Test]
	public async Task UNI0005_GivenInternalCaseType_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			internal readonly record struct Hidden(int Value);

			[GenerateUnion(typeof(Hidden), typeof(string))]
			public readonly partial struct HiddenOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0005");
	}

	[Test]
	public async Task UNI0005_GivenPublicCaseType_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public readonly record struct Visible(int Value);

			[GenerateUnion(typeof(Visible), typeof(string))]
			public readonly partial struct VisibleOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0005");
	}

	[Test]
	public async Task UNI0008_GivenImplicitlyConvertibleCases_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(long))]
			public readonly partial struct IntOrLong;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0008");
	}

	[Test]
	public async Task UNI0008_GivenUnrelatedCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0008");
	}

	[Test]
	public async Task UNI0009_GivenRefStructCase_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(Span<int>), typeof(string))]
			public readonly partial struct SpanOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0009");
	}

	[Test]
	public async Task UNI0009_GivenOrdinaryCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0009");
	}

	[Test]
	public async Task UNI0010_GivenNullableValueTypeCase_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int?), typeof(Guid?))]
			public readonly partial struct MaybeValue;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0010");
	}

	[Test]
	public async Task UNI0011_GivenGenericUnionWithTypeofList_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int))]
			public readonly partial struct Box<T>;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0011");
	}

	[Test]
	public async Task UNI0011_GivenGenericUnionWithSourceCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public readonly record struct Some<T>(T Value);

			[GenerateUnion]
			public readonly partial struct Option<T>
			{
				internal static partial Option<T> Some(Some<T> value);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0011");
	}

	[Test]
	public async Task UNI0012_GivenConstructorsThatDisagree_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString
			{
				public IntOrString(int value) { }
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0012");
	}

	[Test]
	public async Task UNI0012_GivenNoExistingConstructors_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0012");
	}

	[Test]
	public async Task UNI0013_GivenGenericContainingType_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public partial class Outer<T>
			{
				[GenerateUnion(typeof(int), typeof(string))]
				public readonly partial struct Inner;
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0013");
	}

	[Test]
	public async Task UNI0013_GivenNonGenericPartialContainingType_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			public partial class Outer
			{
				[GenerateUnion(typeof(int), typeof(string))]
				public readonly partial struct Inner;
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0013");
	}

	[Test]
	public async Task UNI0014_GivenConflictingCaseLists_IsReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString
			{
				internal static partial IntOrString FromGuid(Guid value);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0014");
	}

	[Test]
	public async Task UNI0014_GivenOneCaseList_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0014");
	}

	[Test]
	public async Task UNI0015_GivenSingleCase_IsReportedAsAWarning(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int))]
			public readonly partial struct Single;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("UNI0015");

		var diagnostic = result.Diagnostics.Single(static candidate => candidate.Id == "UNI0015");
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
	}

	[Test]
	public async Task UNI0015_GivenTwoCases_IsNotReported(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.Unions;

			namespace Test;

			[GenerateUnion(typeof(int), typeof(string))]
			public readonly partial struct IntOrString;
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("UNI0015");
	}
}
