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
