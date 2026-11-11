# Release Flow

Releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the
GitHub Actions workflows in `.github/workflows/`. This repository owns configuration through
`.config/purview-build.json`, not pipeline source code.

## Versioning

`package.json` is the authoritative release and package version; `Purview.BuildSdk` applies it to `Version` and
`PackageVersion` for every project. Never diverge a project's version by hand. Read the current line with
`just version` rather than from this page; a prerelease uses a `MAJOR.MINOR.PATCH-prerelease.N` suffix.

## Releasing a version

A push to `main` publishes, so everything below has to be true **before** the merge.

1. **Give the release a dated changelog heading.** Move the entries under `## [Unreleased]` into a new
   `## [<version>] - YYYY-MM-DD` heading before the merge, so the record names the version that shipped.
   `package.json` stays the authoritative version; the heading is the human-facing history.
2. **Move new diagnostics into a shipped block.** New or changed rules stay in
   `src/src/SourceGenerator/AnalyzerReleases.Unshipped.md` until that version ships, then move into a
   `## Release <version>` block in `AnalyzerReleases.Shipped.md`. A rule stays recorded under the release that
   *introduced* it, so earlier blocks are never rewritten.
3. **Bump the package-validation baseline when a stable exists.** There is deliberately no baseline at
   `0.1.0-prerelease.1`: package validation proves internal consistency until the first stable ships. When it
   does, set `PackageValidationBaselineVersion` to that release and bump it with every stable.
4. **Run `just pipeline-pack-validate`**, which covers restore, build, lint, tests, pack and pack validation in
   one pass, then `just smoke-packed-consumers` and `just verify-pack-rules` when packaging, the generator or a
   dependency version changed.

## Workflows

| Workflow | Trigger | Gate |
| --- | --- | --- |
| `.github/workflows/pr.yml` | pull requests against `main` (opened, synchronize, reopened, ready_for_review) | `build`: shared `purview-build.yml` with the `[Category=Unit]` test filter, `run-pack: true` and `validate-pack: true`. `skills`: verifies `.agents/skills` against `skills-lock.json` without installing anything. `packed-consumers`: packs to an isolated feed, runs a `net10.0` and a `net11.0` consumer against the package only, leak-scans their output and proves every `ForbiddenContent` rule rejects a package. |
| `.github/workflows/release.yml` | push to `main` | the `purview-release` composite action with `release-mode: NuGet`, Trusted Publishing, and the GitHub Packages mirror controlled by repository variables. |

Both workflows get their SDK from `global.json` through `purview-dev/build`, which reads `.sdk.version` and
installs exactly that (`11.0.100`). The shared `purview-build.yml`/`purview-release` code does it, and the two
hand-rolled PR jobs call `purview-dev/build/actions/dotnet-sdk@main` directly. Keep the pin in `global.json`
alone: a workflow-level `dotnet-version` is ignored once `global.json` states one, and
`actions/setup-dotnet`'s `global-json-file` input falls back to the LTS channel when it cannot install the pin,
reports success, and leaves the first `dotnet` command to fail with "A compatible .NET SDK was not found". The
`packed-consumers` job additionally installs the .NET 10 runtime the `net10.0` consumer executes on;
`global.json` still selects the pinned SDK.

`release.yml` runs the composite action rather than the reusable workflow because nuget.org validates the OIDC
`job_workflow_ref` claim against the registered Trusted Publishing policy, and inside a called workflow that
claim names the called repository.

The release workflow packs, publishes to NuGet, and creates the `v<version>` GitHub release only when that tag
does not already exist. Do not create release tags or publish packages manually.

## `.config/purview-build.json`

| Key | Value |
| --- | --- |
| `Build:Solution` | `src/Unions.slnx` |
| `Build:TestRoot` | `src/tests` |
| `Build:TestPatterns` | `*Tests.csproj` |
| `Build:TestProjects` | `*Tests*` |
| `Build:TestFilter` | `/*/*/*/*[Category=Unit]` |
| `Build:RunPack` / `Build:ValidatePack` | schema default `true`; the PR workflow passes both explicitly |
| `PackValidation:RequiredCompilerFlags` | `optimization=release`, asserted for every assembly that ships a PDB |
| `PackValidation:RequireSymbolPackage` | `true` |
| `PackValidation:RequiredContent` | the exhaustive per-package content declaration |
| `PackValidation:ForbiddenContent` | compiler-host assemblies anywhere, and the generator outside `analyzers/` |
| `Release:Mode` | `None` (publishing is enabled only by the release workflow) |

`RequireExplicitContent` defaults to `true`, so `RequiredContent` is exhaustive: a package with no rule, or an
entry matching none of its globs, fails validation. Update it whenever package content changes — a new asset, a
removed PDB, a renamed analyzer.

## Packed shape that must not regress

- `Purview.Unions` ships the runtime library as `lib/<tfm>/Purview.Unions.dll` plus its XML documentation.
- The IL-merged source generator and the code-fix assembly ship under `analyzers/dotnet/cs/` only, never under
  `lib/`, `ref/` or `runtimes/`, where every application would load them.
- `buildTransitive/Purview.Unions.props`, `README.md` and `purview-logo-light.png` ship with the package.
- A symbol package ships alongside it.
- A loose `Purview.SourceGeneratorFramework.dll` beside the merged analyzer is a merge failure; `ForbiddenContent`
  rejects it.

## Analyzer release tracking

The generator ships `UNI0001`–`UNI0016` and `UNI9000`, so it maintains the Roslyn release-tracking files. The
catalogue is declared in `src/src/SourceGenerator/AnalyzerReleases.Unshipped.md`, which the compiler's `RS2008`
rule validates during the build. Suppression descriptors are not release-tracked.

## Commands

```bash
just pipeline-pr              # restore, build, lint, tests
just pipeline-build           # restore, build, lint (no tests, no release)
just pipeline-pack-validate   # restore, build, lint, tests, pack, validate contents
just pipeline-release         # pack, publish, GitHub release (NuGet mode)
just pipeline-local-release   # pack, publish to a local NuGet feed
just smoke-packed-consumers   # pack, then run net10.0 and net11.0 consumers of the package
just test-telemetry-integration # pack, then prove the Purview.Telemetry 6.0.0 integration (local feed)
just verify-pack-rules        # prove every ForbiddenContent rule rejects a package
just version                  # the version package.json declares
```

The pipeline recipes run `scripts/restore-dotnet-tools.ps1`, which installs the pinned `Purview.Build` from
`.config/dotnet-tools.json`.

## Testing the packed package locally

A `.nupkg` published under a **new** version always extracts fresh, so bump `package.json`, publish to the local
feed, then scrub the consuming repository:

```bash
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
```

In the consuming repository, run `just scrub` then `just build`. Use the `LOCAL_NUGET_FEED_PATH` environment
variable when the explicit switch is inconvenient. NuGet reuses an already-extracted package for the same id
**and** version, so prefer a new version over republishing one.
