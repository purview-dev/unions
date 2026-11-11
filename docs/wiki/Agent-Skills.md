# Agent Skills

This repository authors **agent skills** so AI agents working with `Purview.Unions` get the guidance
automatically. They live at `skills/<name>/SKILL.md` and are installed from this git repository with the
skills CLI — they are not shipped inside the NuGet package. A skill can keep task-shaped detail in a
`references/` folder, which an agent loads only when it is needed.

## Skills published by this repository

| Skill | Covers |
| --- | --- |
| `skills/purview-unions-quickstart` | install, compiler requirements, a first union and the no-generator fallback |
| `skills/purview-unions-modelling` | choosing a declaration surface, shaping the cases, generics and nullable value types |
| `skills/purview-unions-diagnostics` | every `UNI0001`–`UNI0016`/`UNI9000` finding and the fix for it |
| `skills/purview-unions-interop` | Results, Telemetry, ZodSharp, JSON, trimming and Native AOT |

## When to load which skill

- Installing the package or writing your first union → `purview-unions-quickstart`
- Choosing between the `typeof` list, partial factories, an `IUnionMembers` provider or `Union<T1, T2>` →
  `purview-unions-modelling`
- A `UNI` diagnostic, a missing generated member, or a union conversion that will not compile →
  `purview-unions-diagnostics`
- A union crossing into Results, Telemetry, ZodSharp, JSON, a trimmed application or Native AOT →
  `purview-unions-interop`

## Install

```bash
npx skills add purview-dev/unions
```

The CLI clones this repository, so the skills always match the branch you install from.

## Upstream skills

The Purview engineering playbook, `Purview.BuildSdk` and the source-generator framework publish their own
skills (engineering conventions, SDK configuration and project layout, generator authoring and testing)
through the same CLI. Those are owned by the repository that publishes them — changes belong there, not
here.

This repository installs them into the universal `.agents/skills/` folder only (`-a universal`) and commits
that folder together with the CLI's `skills-lock.json`, so a clone has them without running anything.
Nothing is installed into `.claude/` or any other per-agent folder, and this repository's own skills are
never copied into `.agents/skills/` — two copies would drift apart.

## Related

- [Getting Started](Getting-Started.md) — install, declare and use a union.
- [Union Design](Union-Design.md) — the compiler contract and the emitted shape.
- [Release Flow](Release-Flow.md) — where the authored skill set is released from.
