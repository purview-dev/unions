PrintSurfaceA();
PrintSurfaceB();
PrintSurfaceC();
PrintSurfaceD();
PrintAllocations();

void PrintSurfaceA()
{
	IntOrString value = 42;

	if (value.TryGetValue(out int number))
		Console.WriteLine($"A: int {number}; has-value={value.HasValue}");

	IntOrString text = "hello";
	if (text.TryGetValue(out string message))
		Console.WriteLine($"A: string {message}; has-value={text.HasValue}");

	IntOrString empty = default;
	Console.WriteLine($"A: default has-value={empty.HasValue}; value={empty.Value ?? "<null>"}");
}

void PrintSurfaceB()
{
	Maybe<int> some = new Some<int>(7);
	Maybe<int> none = new None();

	Console.WriteLine(
		$"B: some={some.TryGetValue(out Some<int> value) && value.Value == 7}; none={none.TryGetValue(out None _)}"
	);
}

void PrintSurfaceC()
{
	Outcome<int> outcome = 9;

	if (outcome.TryGetValue(out int number))
		Console.WriteLine($"C: int {number}");

	Outcome<int> failure = new InvalidOperationException("boom");
	if (failure.TryGetValue(out Exception error))
		Console.WriteLine($"C: error {error.Message}");
}

void PrintSurfaceD()
{
	Union<int, string> value = 42;

	if (value.TryGetValue(out int number))
		Console.WriteLine($"D: int {number}; tag={value.Tag}");

	Union<int, string> text = "hello";
	Console.WriteLine($"D: {text.Match(n => $"int {n}", s => $"string {s}")}; text={text.Value}");
}

void PrintAllocations()
{
	IntOrString value = 42;

	// Warm the paths so JIT/tiering allocations are not attributed to the measurement.
	for (var index = 0; index < 100; index++)
	{
		_ = value.TryGetValue(out int _);
		_ = value is int;
	}

	var beforeIs = GC.GetAllocatedBytesForCurrentThread();
	_ = value is int;
	var afterIs = GC.GetAllocatedBytesForCurrentThread();

	var beforeTry = GC.GetAllocatedBytesForCurrentThread();
	_ = value.TryGetValue(out int _);
	var afterTry = GC.GetAllocatedBytesForCurrentThread();

	var beforeValue = GC.GetAllocatedBytesForCurrentThread();
	_ = value.Value;
	var afterValue = GC.GetAllocatedBytesForCurrentThread();

	Console.WriteLine($"alloc: is={afterIs - beforeIs} tryget={afterTry - beforeTry} value={afterValue - beforeValue}");
}
