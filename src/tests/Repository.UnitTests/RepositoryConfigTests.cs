namespace Purview.Unions.Repository;

/// <summary>
/// Guards the repository's build configuration and agent instructions.
/// </summary>
public sealed class RepositoryConfigTests
{
	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	[Test]
	public async Task BuildConfiguration_NamesTheRealSolution(CancellationToken cancellationToken)
	{
		var config = await ReadAsync(".config/purview-build.json", cancellationToken);
		var solution = await ReadAsync("src/Unions.slnx", cancellationToken);

		await Assert.That(config).Contains("\"Solution\": \"src/Unions.slnx\"");
		await Assert.That(solution).Contains("src/Unions/Unions.csproj");
		await Assert.That(solution).Contains("tests/Repository.UnitTests/Repository.UnitTests.csproj");
	}

	[Test]
	public async Task Solution_ListsTheBenchmarksProject(CancellationToken cancellationToken)
	{
		var solution = await ReadAsync("src/Unions.slnx", cancellationToken);

		await Assert.That(solution).Contains("src/Benchmarks/Benchmarks.csproj");
		await Assert
			.That(File.Exists(Path.Combine(RepositoryRoot.Value, "src", "src", "Benchmarks", "Benchmarks.csproj")))
			.IsTrue();
	}

	[Test]
	public async Task Solution_ReferencesTheRealBuildConfiguration(CancellationToken cancellationToken)
	{
		var solution = await ReadAsync("src/Unions.slnx", cancellationToken);

		await Assert.That(solution).Contains("../.config/purview-build.json");
		await Assert.That(File.Exists(Path.Combine(RepositoryRoot.Value, ".config", "purview-build.json"))).IsTrue();
	}

	[Test]
	public async Task PrWorkflow_TestFilterMatchesTheBuildConfiguration(CancellationToken cancellationToken)
	{
		var config = await ReadAsync(".config/purview-build.json", cancellationToken);
		var workflow = await ReadAsync(".github/workflows/pr.yml", cancellationToken);

		await Assert.That(config).Contains("/*/*/*/*[Category=Unit]");
		await Assert.That(workflow).Contains("/*/*/*/*[Category=Unit]");
	}

	[Test]
	public async Task PerformanceStructure_IsWiredIntoTheRepository(CancellationToken cancellationToken)
	{
		var packages = await ReadAsync("Directory.Packages.props", cancellationToken);
		var justfile = await ReadAsync("Justfile", cancellationToken);
		var sidebar = await ReadAsync("docs/wiki/_Sidebar.md", cancellationToken);
		var readme = await ReadAsync("README.md", cancellationToken);

		await Assert.That(packages).Contains("BenchmarkDotNet");
		await Assert.That(justfile).Contains("perf-tests");
		await Assert.That(justfile).Contains("src/src/Benchmarks/Benchmarks.csproj");
		await Assert.That(sidebar).Contains("(Performance.md)");
		await Assert.That(readme).Contains("docs/wiki/Performance.md");
	}

	[Test]
	public async Task SourceProjects_KeepTheDependencyDirectionOneWay(CancellationToken cancellationToken)
	{
		foreach (
			var project in new[]
			{
				"src/src/Unions/Unions.csproj",
				"src/src/SourceGenerator/SourceGenerator.csproj",
				"src/src/SourceGenerator.CodeFixes/SourceGenerator.CodeFixes.csproj",
			}
		)
		{
			var text = await ReadAsync(project, cancellationToken);

			await Assert
				.That(text)
				.DoesNotContain("<ProjectReference")
				.Because(
					$"{project} must not take a project dependency; the runtime, generator and code fixes are peers"
				);
		}
	}

	[Test]
	public async Task BuildConfiguration_NamesEveryPackageContent(CancellationToken cancellationToken)
	{
		var config = await ReadAsync(".config/purview-build.json", cancellationToken);

		foreach (
			var expected in new[]
			{
				"README.md",
				"analyzers/dotnet/cs/Purview.Unions.SourceGenerator.dll",
				"analyzers/dotnet/cs/Purview.Unions.SourceGenerator.CodeFixes.dll",
				"buildTransitive/Purview.Unions.props",
				"lib/$(TFM)/Purview.Unions.dll",
				"lib/$(TFM)/Purview.Unions.xml",
				"purview-logo-light.png",
			}
		)
			await Assert.That(config).Contains(expected);
	}

	[Test]
	public async Task BuildConfiguration_ForbidsCompilerHostAssembliesEverywhere(CancellationToken cancellationToken)
	{
		var config = await ReadAsync(".config/purview-build.json", cancellationToken);

		await Assert.That(config).Contains("analyzers/**/Purview.SourceGeneratorFramework*");
		await Assert.That(config).Contains("analyzers/**/Microsoft.CodeAnalysis*");
		await Assert.That(config).Contains("lib/**/Purview.Unions.SourceGenerator*");
		await Assert.That(config).Contains("ref/**/Purview.Unions.SourceGenerator*");
		await Assert.That(config).Contains("runtimes/**/Purview.Unions.SourceGenerator*");
	}

	[Test]
	public async Task AgentsMd_LinksEveryAuthoredSkill(CancellationToken cancellationToken)
	{
		var agents = await ReadAsync("AGENTS.md", cancellationToken);

		foreach (
			var skill in Directory
				.GetDirectories(Path.Combine(RepositoryRoot.Value, "skills"))
				.Select(static path => Path.GetFileName(path))
		)
		{
			await Assert
				.That(agents)
				.Contains(skill)
				.Because($"AGENTS.md must name the authored skill '{skill}' so an agent can find it");
		}
	}

	[Test]
	public async Task PackageJson_CarriesThePrereleaseVersion(CancellationToken cancellationToken)
	{
		var package = await ReadAsync("package.json", cancellationToken);

		await Assert.That(package).Contains("\"version\": \"0.1.0-prerelease.1\"");
		await Assert.That(package).Contains("\"name\": \"unions\"");
	}

	[Test]
	public async Task Generator_DoesNotShipAsItsOwnPackage(CancellationToken cancellationToken)
	{
		var project = await ReadAsync("src/src/SourceGenerator/SourceGenerator.csproj", cancellationToken);

		await Assert.That(project).DoesNotContain("<IsPackable>true</IsPackable>");
	}

	static async Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken) =>
		await File.ReadAllTextAsync(Path.Combine(RepositoryRoot.Value, relativePath), cancellationToken);

	static string FindRepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
				&& File.Exists(Path.Combine(directory.FullName, "skills-lock.json"))
				&& Directory.Exists(Path.Combine(directory.FullName, "src", "src"))
			)
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}' carries AGENTS.md, skills-lock.json and src/src."
		);
	}
}
