using System.Diagnostics;
using System.Text.Json;

namespace Purview.Unions.Repository;

/// <summary>
/// Guards the skills installed from other repositories.
/// </summary>
/// <remarks>
/// <para>
/// Skills are installed by the skills CLI into the universal <c>.agents/skills/</c> folder only. That
/// folder is a local install - gitignored, never tracked, reproducible from the committed
/// <c>skills-lock.json</c> - so a skill this repository installs but does not publish cannot be found
/// through <c>skills add purview-dev/unions</c>. This repository installs them with
/// <c>just skills-update</c> (the SDK's <c>UpdatePurviewSkills</c> target), so the tests that compare the
/// install with the record are vacuous until that has run, and strict afterwards.
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

	[Test]
	public async Task AgentsSkills_GivenEverythingTheCLIWrites_IsIgnoredAndNeverTracked(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the folder is a local install, so nothing under it may be tracked, and the rules that
		// keep it (and every per-agent folder) out of this repository must be in place. This is what stops
		// a skill the repository installs but does not publish being found by `skills add
		// purview-dev/unions`.
		var tracked = await TrackedFilesUnderAgentsSkillsAsync(cancellationToken);
		var ignores = await AgentsSkillsIgnoreRulesAsync(cancellationToken);

		// Assert
		await Assert
			.That(tracked)
			.IsEmpty()
			.Because(
				"nothing under .agents/skills/ may be tracked: it is a local install, reproducible from "
					+ $"skills-lock.json with `just skills-install`: {string.Join(", ", tracked)}"
			);
		await Assert
			.That(ignores.Agents)
			.IsTrue()
			.Because(".gitignore must keep .agents/skills/ out of this repository");
		await Assert.That(ignores.Claude).IsTrue().Because(".gitignore must keep .claude/ out of this repository");
	}

	/// <summary>What git tracks under <c>.agents/skills/</c>, asked of the repository itself.</summary>
	static async Task<string[]> TrackedFilesUnderAgentsSkillsAsync(CancellationToken cancellationToken)
	{
		using Process process = new()
		{
			StartInfo = new ProcessStartInfo("git", "ls-files .agents/skills")
			{
				WorkingDirectory = RepositoryRoot.Value,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		return
		[
			.. output
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Order(StringComparer.Ordinal),
		];
	}

	/// <summary>The ignore rules for the two agent folders, as <c>.gitignore</c> spells them.</summary>
	static async Task<(bool Agents, bool Claude)> AgentsSkillsIgnoreRulesAsync(CancellationToken cancellationToken)
	{
		var lines = await File.ReadAllLinesAsync(Path.Combine(RepositoryRoot.Value, ".gitignore"), cancellationToken);
		var rules = lines.Select(static line => line.Trim()).ToHashSet(StringComparer.Ordinal);
		return (
			rules.Overlaps([".agents/skills/", ".agents/skills", "/.agents/skills/", ".agents/"]),
			rules.Overlaps([".claude/", ".claude", "/.claude/", "/.claude"])
		);
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
