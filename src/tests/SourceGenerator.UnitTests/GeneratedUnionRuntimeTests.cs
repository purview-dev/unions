namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Executes the unions the generator produced for this test project itself, on both target frameworks.
/// </summary>
/// <remarks>
/// These declarations are compiled by the real generator against the real runtime, so the tests below
/// exercise exactly what a consumer receives: the emitted constructors, <c>Value</c>, <c>HasValue</c> and
/// <c>TryGetValue</c>, plus the compiler's union conversions. The in-memory compilation tests prove the
/// compiler contract; these prove the generated members behave.
/// </remarks>
public sealed class GeneratedUnionRuntimeTests
{
	[Test]
	public async Task SurfaceA_ImplicitConversion_HoldsTheCase(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IntOrString value = 42;

		await Assert.That(value.TryGetValue(out int number)).IsTrue();
		await Assert.That(number).IsEqualTo(42);
		await Assert.That(value.HasValue).IsTrue();
		await Assert.That(value.TryGetValue(out string _)).IsFalse();
	}

	[Test]
	public async Task SurfaceA_ReferenceCase_RoundTrips(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IntOrString value = "hello";

		await Assert.That(value.TryGetValue(out string text)).IsTrue();
		await Assert.That(text).IsEqualTo("hello");
		await Assert.That(value.TryGetValue(out int _)).IsFalse();
	}

	[Test]
	public async Task SurfaceA_Default_IsEmpty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IntOrString value = default;

		await Assert.That(value.HasValue).IsFalse();
		await Assert.That(value.Value).IsNull();
		await Assert.That(value.TryGetValue(out int _)).IsFalse();
	}

	[Test]
	public async Task SurfaceA_NullReference_LeavesTheUnionEmpty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IntOrString value = (string?)null;

		await Assert.That(value.HasValue).IsFalse();
		await Assert.That(value.TryGetValue(out string _)).IsFalse();
	}

	[Test]
	public async Task SurfaceB_GenericFactories_CreateTheCases(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Maybe<int> some = new Some<int>(7);
		Maybe<int> none = new None();

		await Assert.That(some.TryGetValue(out Some<int> value)).IsTrue();
		await Assert.That(value.Value).IsEqualTo(7);
		await Assert.That(none.TryGetValue(out None _)).IsTrue();
	}

	[Test]
	public async Task SurfaceB_InternalUnionAndInternalCases_CreateTheCases(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		InternalMaybe<int> some = new InternalSome<int>(7);
		InternalMaybe<int> none = new InternalNone();

		await Assert.That(some.TryGetValue(out InternalSome<int> value)).IsTrue();
		await Assert.That(value.Value).IsEqualTo(7);
		await Assert.That(none.TryGetValue(out InternalNone _)).IsTrue();
	}

	[Test]
	public async Task SurfaceC_ProviderFactories_CreateTheCases(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Outcome<int> outcome = 9;
		Outcome<int> failure = new InvalidOperationException("boom");

		await Assert.That(outcome.TryGetValue(out int number)).IsTrue();
		await Assert.That(number).IsEqualTo(9);
		await Assert.That(failure.TryGetValue(out Exception? error)).IsTrue();
		await Assert.That(error!.Message).IsEqualTo("boom");
	}

	[Test]
	public async Task ClassUnion_ConstructedInstance_HoldsTheCase(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		UriOrString value = new Uri("https://purview.dev/");

		await Assert.That(value.HasValue).IsTrue();
		await Assert.That(value.TryGetValue(out Uri? address)).IsTrue();
		await Assert.That(address!.Host).IsEqualTo("purview.dev");
	}

	[Test]
	public async Task ClassUnion_NullCase_LeavesTheUnionEmpty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		UriOrString value = (string?)null;

		await Assert.That(value.HasValue).IsFalse();
	}

	[Test]
	public async Task PatternsAndTryGetValue_DoNotAllocate_ButValueDoes(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IntOrString value = 42;

		// Warm the paths so JIT and tiering allocations are not attributed to the measurement.
		for (var index = 0; index < 100; index++)
		{
			_ = value is int;
			_ = value.TryGetValue(out int _);
			_ = Match(value);
		}

		var beforeIs = GC.GetAllocatedBytesForCurrentThread();
		_ = value is int;
		var afterIs = GC.GetAllocatedBytesForCurrentThread();

		var beforeTry = GC.GetAllocatedBytesForCurrentThread();
		_ = value.TryGetValue(out int _);
		var afterTry = GC.GetAllocatedBytesForCurrentThread();

		var beforeSwitch = GC.GetAllocatedBytesForCurrentThread();
		_ = Match(value);
		var afterSwitch = GC.GetAllocatedBytesForCurrentThread();

		var beforeValue = GC.GetAllocatedBytesForCurrentThread();
		_ = value.Value;
		var afterValue = GC.GetAllocatedBytesForCurrentThread();

		await Assert.That(afterIs - beforeIs).IsEqualTo(0);
		await Assert.That(afterTry - beforeTry).IsEqualTo(0);
		await Assert.That(afterSwitch - beforeSwitch).IsEqualTo(0);
		await Assert.That(afterValue - beforeValue).IsGreaterThan(0);
	}

	static int Match(IntOrString value)
	{
		var result = -1;

		switch (value)
		{
			case int number:
				result = number;
				break;
			case string text:
				result = text.Length;
				break;
			default:
				break;
		}

		return result;
	}
}
