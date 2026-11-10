# GitHub Copilot Instructions

## Primary instruction source

Use the repository root [`AGENTS.md`](../AGENTS.md) as the **primary** source of truth for behavior, architecture context, testing standards, and completion criteria.

If this file and `AGENTS.md` appear to conflict, prefer `AGENTS.md` unless this file explicitly states a GitHub Copilot-only exception.

## Copilot-specific guidance

This file should only contain **GitHub Copilot-specific** instruction details.
Keep product, architecture, and general engineering standards centralized in `AGENTS.md`.

## Operating expectations for Copilot

- Apply the `AGENTS.md` testing bar strictly (TUnit, `{Subject}_{Scenario}_{Expectation}` naming, the cancellation-token rule, and the permanent compiler experiments in `CompilerBehaviourTests`).
- Treat work as incomplete until the relevant tests pass. The union invariants in `AGENTS.md` are not negotiable.
- Consult the skills installed in `.agents/skills/` (recorded in `skills-lock.json`) for upstream guidance. They are installed by the skills CLI and read-only here; the repository's own skills live in `skills/` only.
- Benchmarks are evidence, not a CI gate. Run `just perf-tests` from a Release build before changing a performance claim, and update `docs/wiki/Performance.md`.
- Keep edits minimal, focused, and aligned with the existing SDK and repository conventions.
