namespace Purview.Unions;

/// <summary>
/// Behaviour of the hand-written <see cref="Union{T1,T2}"/>, the .NET 10 and no-generator story.
/// </summary>
public sealed class UnionTests
{
	[Test]
	public async Task ImplicitConversion_GivenFirstCase_HoldsIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = 42;

		await Assert.That(value.Tag).IsEqualTo(1);
		await Assert.That(value.HasValue).IsTrue();
		await Assert.That(value.TryGetValue(out int number)).IsTrue();
		await Assert.That(number).IsEqualTo(42);
		await Assert.That(value.TryGetValue(out string? _)).IsFalse();
	}

	[Test]
	public async Task ImplicitConversion_GivenSecondCase_HoldsIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = "hello";

		await Assert.That(value.Tag).IsEqualTo(2);
		await Assert.That(value.HasValue).IsTrue();
		await Assert.That(value.TryGetValue(out string? text)).IsTrue();
		await Assert.That(text!).IsEqualTo("hello");
		await Assert.That(value.TryGetValue(out int _)).IsFalse();
	}

	[Test]
	public async Task Create_GivenEitherCase_HoldsIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await Assert.That(Union<int, string>.Create(7).TryGetValue(out int number)).IsTrue();
		await Assert.That(number).IsEqualTo(7);

		await Assert.That(Union<int, string>.Create("text").TryGetValue(out string? text)).IsTrue();
		await Assert.That(text!).IsEqualTo("text");
	}

	[Test]
	public async Task Value_GivenEitherCase_ReturnsIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> number = 42;
		Union<int, string> text = "hello";
		Union<int, string> empty = default;

		await Assert.That(number.Value).IsEqualTo(42);
		await Assert.That(text.Value).IsEqualTo("hello");
		await Assert.That(empty.Value).IsNull();
	}

	[Test]
	public async Task Default_IsEmpty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = default;

		await Assert.That(value.Tag).IsEqualTo(0);
		await Assert.That(value.HasValue).IsFalse();
		await Assert.That(value.TryGetValue(out int _)).IsFalse();
		await Assert.That(value.TryGetValue(out string? _)).IsFalse();
		await Assert.That(value.ToString()).IsEqualTo("<empty>");
	}

	[Test]
	public async Task NullReference_LeavesTheUnionEmpty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = (string?)null;

		await Assert.That(value.HasValue).IsFalse();
		await Assert.That(value.Tag).IsEqualTo(0);
		await Assert.That(value.Value).IsNull();
	}

	[Test]
	public async Task NullableValueType_LeavesTheUnionEmptyWhenNull(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int?, string> value = (int?)null;

		await Assert.That(value.HasValue).IsFalse();
		await Assert.That(value.Tag).IsEqualTo(0);
	}

	[Test]
	public async Task Match_GivenEitherCase_FoldsIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> number = 42;
		Union<int, string> text = "hello";

		await Assert.That(number.Match(n => $"int {n}", s => $"string {s}")).IsEqualTo("int 42");
		await Assert.That(text.Match(n => $"int {n}", s => $"string {s}")).IsEqualTo("string hello");
	}

	[Test]
	public async Task Match_GivenEmpty_Throws(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = default;

		await Assert.That(() => value.Match(n => n, s => s.Length)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Switch_GivenEitherCase_InvokesTheMatchingAction(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = "hello";
		string? observed = null;

		value.Switch(n => observed = $"int {n}", s => observed = $"string {s}");

		await Assert.That(observed).IsEqualTo("string hello");
	}

	[Test]
	public async Task Equality_ComparesCaseAndValue(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> first = 42;
		Union<int, string> same = 42;
		Union<int, string> other = 43;
		Union<int, string> text = "42";
		Union<int, string> empty = default;

		await Assert.That(first == same).IsTrue();
		await Assert.That(first != other).IsTrue();
		await Assert.That(first != text).IsTrue();
		await Assert.That(empty == default).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(same.GetHashCode());
		await Assert.That(first.Equals((object)same)).IsTrue();
	}

	[Test]
	public async Task ToString_GivenActiveCase_ReturnsItsText(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = "hello";

		await Assert.That(value.ToString()).IsEqualTo("hello");
	}

	[Test]
	public async Task Interface_ExposesTagAndValue(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IUnion[] unions = [Union<int, string>.Create("hello")];

		await Assert.That(unions[0].Tag).IsEqualTo(2);
		await Assert.That(unions[0].Value).IsEqualTo("hello");
	}

	[Test]
	public async Task ReferenceAndValueCases_OnOneUnion_DoNotInterfere(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<Uri, int> uri = new Uri("https://purview.dev/");
		Union<Uri, int> number = 7;

		await Assert.That(uri.TryGetValue(out Uri? address)).IsTrue();
		await Assert.That(address!.Host).IsEqualTo("purview.dev");
		await Assert.That(uri.TryGetValue(out int _)).IsFalse();

		await Assert.That(number.TryGetValue(out int value)).IsTrue();
		await Assert.That(value).IsEqualTo(7);
		await Assert.That(number.TryGetValue(out Uri? _)).IsFalse();
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task Net11_IsACompilerUnion(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// The net11.0 asset carries [Union], so the compiler also exposes the BCL access interface.
		Union<int, string> value = 42;

		await Assert.That((System.Runtime.CompilerServices.IUnion)value).IsNotNull();
	}

	[Test]
	public async Task Net11_SupportsUnionMatching(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Union<int, string> value = 42;

		// `is int` on the union compiles only because the compiler treats the type as a union and calls
		// TryGetValue; a plain struct cannot be tested against an unrelated case type.
		await Assert.That(value is int).IsTrue();
	}
#endif
}
