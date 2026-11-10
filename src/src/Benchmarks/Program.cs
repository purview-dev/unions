using BenchmarkDotNet.Running;

Console.WriteLine("=== Purview.Unions performance benchmarks ===\n");

// Preserve the run-everything behaviour for `just perf-tests`, but honour BenchmarkDotNet
// arguments such as `--filter` when they are provided.
if (args.Length == 0)
	BenchmarkRunner.Run(typeof(Program).Assembly);
else
	BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

Console.WriteLine("\nResults are in BenchmarkDotNet.Artifacts/results/.");
