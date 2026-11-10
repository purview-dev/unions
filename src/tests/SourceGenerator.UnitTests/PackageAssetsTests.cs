namespace Purview.Unions.SourceGenerator;

/// <summary>
/// Guards the package assets that make the generator's build switches reachable by <c>PackageReference</c>
/// consumers, and the packing arrangement that keeps the framework assembly out of the package.
/// </summary>
public sealed class PackageAssetsTests
{
	[Test]
	public async Task PackagedBuildProps_DeclaresTheDisableProperty(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var path = Path.Combine(
			RepositoryRoot(),
			"src",
			"src",
			"Unions",
			"Sdk",
			"buildTransitive",
			"Purview.Unions.props"
		);

		await Assert.That(File.Exists(path)).IsTrue();

		var content = await File.ReadAllTextAsync(path, cancellationToken);
		await Assert
			.That(content)
			.Contains(
				$"<CompilerVisibleProperty Include=\"{UnionsSourceGeneratorTestOptions.DisableGeneratorProperty}\">"
			);
	}

	[Test]
	public async Task GeneratorProject_DeclaresThePropertyItReads(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var project = await File.ReadAllTextAsync(
			Path.Combine(RepositoryRoot(), "src", "src", "SourceGenerator", "SourceGenerator.csproj"),
			cancellationToken
		);

		await Assert
			.That(project)
			.Contains(
				$"<UnionsSourceGeneratorDisableProperty>{UnionsSourceGeneratorTestOptions.DisableGeneratorProperty}</UnionsSourceGeneratorDisableProperty>"
			);
	}

	[Test]
	public async Task RuntimeProject_BundlesTheMergedGeneratorAndCodeFix(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var project = await File.ReadAllTextAsync(
			Path.Combine(RepositoryRoot(), "src", "src", "Unions", "Unions.csproj"),
			cancellationToken
		);

		await Assert.That(project).Contains("PackUnionsSourceGenerator");
		await Assert.That(project).Contains("GetPurviewMergedAnalyzerFile");
		await Assert.That(project).Contains("..\\SourceGenerator.CodeFixes\\SourceGenerator.CodeFixes.csproj");
		await Assert.That(project).Contains("analyzers/dotnet/cs/");
	}

	[Test]
	public async Task SourceGeneratorProject_IsNotPackable(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var project = await File.ReadAllTextAsync(
			Path.Combine(RepositoryRoot(), "src", "src", "SourceGenerator", "SourceGenerator.csproj"),
			cancellationToken
		);

		await Assert.That(project).DoesNotContain("<IsPackable>true</IsPackable>");
	}

	[Test]
	public async Task PackValidation_NamesTheRealSolutionAndPackageContents(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var config = await File.ReadAllTextAsync(
			Path.Combine(RepositoryRoot(), ".config", "purview-build.json"),
			cancellationToken
		);

		await Assert.That(config).Contains("\"Solution\": \"src/Unions.slnx\"");
		await Assert.That(config).Contains("analyzers/dotnet/cs/Purview.Unions.SourceGenerator.dll");
		await Assert.That(config).Contains("analyzers/dotnet/cs/Purview.Unions.SourceGenerator.CodeFixes.dll");
		await Assert.That(config).Contains("buildTransitive/Purview.Unions.props");
	}

	static string RepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (File.Exists(Path.Combine(directory.FullName, "src", "Unions.slnx")))
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"The repository root (the directory containing src/Unions.slnx) was not found above '{AppContext.BaseDirectory}'."
		);
	}
}
