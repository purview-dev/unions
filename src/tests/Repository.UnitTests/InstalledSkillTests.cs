using System.Text.Json;

namespace Purview.Unions.Repository;

/// <summary>
/// Guards the skills installed from other repositories.
/// </summary>
/// <remarks>
/// <para>
/// Skills are installed by the skills CLI into the universal <c>.agents/skills/</c> folder only, and that
/// folder and the CLI's <c>skills-lock.json</c> are both committed. This repository ships the lockfile and
/// installs the skills with <c>just skills-update</c> (the SDK's <c>UpdatePurviewSkills</c> target), so the
/// tests are vacuous until that has run and become strict afterwards.
/// </para>
/// <para>
/// <c>SkillTests</c> validates this repository's own skills in <c>skills/</c>; these are someone else's, so
/// their content is not checked here, only that the installed set is the recorded set.
/// </para>
/// </remarks>
public sealed class InstalledSkillTests
{
	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	[Test]
	public async Task Lockfile_IsAValidRecordOfTheInstalledSources(CancellationToken cancellationToken)
	{
		var recorded = await ReadLockfileSkillsAsync(cancellationToken);

		await Assert.That(recorded).IsNotEmpty();

		await using var stream = File.OpenRead(Path.Combine(RepositoryRoot.Value, "skills-lock.json"));
		using var lockfile = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var sources = lockfile
			.RootElement.GetProperty("skills")
			.EnumerateObject()
			.Select(static property => property.Value.GetProperty("source").GetString())
			.Where(static source => !string.IsNullOrEmpty(source))
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		await Assert.That(sources).IsNotEmpty();
	}

	[Test]
	public async Task AgentsSkills_GivenInstalledSkills_MatchesTheLockfile(CancellationToken cancellationToken)
	{
		var agentsSkills = Path.Combine(RepositoryRoot.Value, ".agents", "skills");
		if (!Directory.Exists(agentsSkills))
		{
			// The skills are installed by `just skills-update`, which needs the skills CLI and network
			// access. Until it has run there is nothing to compare, and the authored skills in skills/ are
			// still validated by SkillTests.
			await Assert.That(AgentsSkillsExists(agentsSkills)).IsFalse();

			return;
		}

		var recorded = await ReadLockfileSkillsAsync(cancellationToken);
		var installed = Directory.GetDirectories(agentsSkills).Select(static path => Path.GetFileName(path)).ToArray();

		await Assert
			.That(installed.Where(name => !recorded.Contains(name, StringComparer.Ordinal)).ToArray())
			.IsEmpty()
			.Because(
				"every folder in .agents/skills/ must be written by the skills CLI and recorded in skills-lock.json"
			);

		await Assert
			.That(recorded.Where(name => !installed.Contains(name, StringComparer.Ordinal)).ToArray())
			.IsEmpty()
			.Because("skills-lock.json records skills that are not installed; run `just skills-update`");
	}

	[Test]
	public async Task AgentsSkills_GivenTheAuthoredSkills_HoldsNoCopyOfThem(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var agentsSkills = Path.Combine(RepositoryRoot.Value, ".agents", "skills");
		if (!Directory.Exists(agentsSkills))
			return;

		var authored = Directory
			.GetDirectories(Path.Combine(RepositoryRoot.Value, "skills"))
			.Select(static path => Path.GetFileName(path))
			.ToArray();

		var copied = authored.Where(name => Directory.Exists(Path.Combine(agentsSkills, name))).ToArray();

		await Assert
			.That(copied)
			.IsEmpty()
			.Because(
				"this repository's own skills live in skills/ only; a copy in .agents/skills/ would drift from "
					+ $"the authored one: {string.Join(", ", copied)}"
			);
	}

	[Test]
	public async Task ClaudeFolder_GivenTheInstalledSkills_HoldsNoCopy(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var claudeSkills = Path.Combine(RepositoryRoot.Value, ".claude", "skills");

		await Assert
			.That(Directory.Exists(claudeSkills))
			.IsFalse()
			.Because(
				"skills are installed into .agents/skills/ only. A .claude/skills/ folder means the CLI ran without "
					+ "`-a universal`; delete it and reinstall with `-a universal`"
			);
	}

	[Test]
	public async Task Repository_GivenAgentInstructions_HasNoClaudeMd(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		string[] candidates =
		[
			Path.Combine(RepositoryRoot.Value, "CLAUDE.md"),
			Path.Combine(RepositoryRoot.Value, ".github", "CLAUDE.md"),
		];

		await Assert
			.That(candidates.Where(File.Exists).ToArray())
			.IsEmpty()
			.Because("this repository is not agent-specific: AGENTS.md is its one instruction file");
	}

	static bool AgentsSkillsExists(string path) => Directory.Exists(path);

	static async Task<string[]> ReadLockfileSkillsAsync(CancellationToken cancellationToken)
	{
		await using var stream = File.OpenRead(Path.Combine(RepositoryRoot.Value, "skills-lock.json"));
		using var lockfile = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		return
		[
			.. lockfile
				.RootElement.GetProperty("skills")
				.EnumerateObject()
				.Select(static property => property.Name)
				.Order(StringComparer.Ordinal),
		];
	}

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
