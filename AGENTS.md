# Agent instructions

## Authority

This file is the source of truth for how work is done in `purview-dev/unions`. `README.md` explains the
package to a consumer; `docs/wiki/` explains the design and its evidence; this file explains the rules a
change must satisfy. Where they disagree, the more specific document wins for its subject, and a change
must update both rather than leave them inconsistent.

The repository builds `Purview.Unions`: a dependency-free .NET package that generates the C# 15
**custom union type** shape so value-type union cases are never boxed by construction, `TryGetValue`,
`is` or `switch`. It also ships a hand-written `Purview.Unions.Union<T1, T2>`.

## Source of truth layout

| Path | What it owns |
| --- | --- |
| `src/src/Unions/` | the packable runtime package, its `[GenerateUnion]` attribute, `IUnion` and `Union<T1, T2>` |
| `src/src/SourceGenerator/` | the `netstandard2.0` incremental generator and analyzer, merged into the package |
| `src/src/SourceGenerator.CodeFixes/` | the IDE code fixes; never referenced by the generator |
| `src/src/Examples/` | a runnable sample of surfaces A, B, C and D |
| `src/tests/Unions.UnitTests/` | `Union<T1, T2>` behaviour |
| `src/tests/SourceGenerator.UnitTests/` | generation, the compiler experiments, diagnostics, code fixes, cache tests |
| `src/tests/Repository.UnitTests/` | skills conformance, build configuration and agent instructions |
| `docs/wiki/` | design, evidence and follow-ups |
| `skills/` | the four skills published to consumers |

Do not add a second implementation of a union rule. Discovery, validation and the diagnostic catalogue
live once in `src/src/SourceGenerator`; the analyzer and the generator both consume them.

## Standard workflow

1. `just restore`
2. `just build`
3. `just test` (runs net10.0 and net11.0)
4. `just lint-check`
5. `just pipeline-pack-validate`
6. `just verify-pack-rules`

`just skills-update` installs the skills recorded in `skills-lock.json` via the SDK's
`UpdatePurviewSkills` target. It needs the skills CLI and network access; ordinary builds and tests never
run it.

## Union invariants

The compiler's contract is not ours to redefine. Every generated union must satisfy it:

- **[Union] and creation members.** The type carries
  `System.Runtime.CompilerServices.UnionAttribute` and has one public constructor per case (by value or
  `in`). With an `IUnionMembers` provider, the provider's static `Create` methods are the creation members
  and the union must not also declare public constructors.
- **`Value` boxes.** `public object? Value { get; }` is mandatory and boxes a value-type case when read.
  That is inherent; never claim otherwise in code or docs.
- **`HasValue` is `Value is not null`.** It is true iff `Value` is not null, evaluated per active case
  without boxing. Do not use `Tag != 0` when a reference or nullable case can be null.
- **`TryGetValue` semantics.** It returns true only for a non-null active case of that type. A nullable
  value-type case is exposed through its underlying type (`int?` → `TryGetValue(out int)`).
- **Soundness.** `Value` is null or a value of a case type, including for `default`.
- **Stability.** Creating from a case value yields that case or null; creating from null yields an empty
  union (`Tag == 0`).
- **Creation equivalence.** If a value converts to two cases, the declaration is ambiguous: report
  `UNI0008` rather than emit it.
- **Access consistency.** `HasValue`/`TryGetValue` must agree with checking `Value`.

## Source-generator rules

- Target `netstandard2.0`; `IsRoslynComponent=true`.
- Use `Purview.SourceGeneratorFramework`: `IncrementalPipeline`, `GenerationContext`, `GeneratorResult`,
  `CodeWriter`, `EquatableArray`.
- The pipeline stays value-equatable. No `ISymbol`, `Compilation`, `SemanticModel`, `SyntaxNode`,
  `SyntaxTree`, `Location` or `IOperation` in cached state; convert during discovery.
- One `RegisterSourceOutput` per union. Create the `CodeWriter` inside the callback. Report only `UNI9000`
  from the generator; the analyzer owns the per-target rules.
- Hint names and ordering are deterministic, derived from type identity. No timestamps, GUIDs or machine
  paths.
- Emitted code is trim- and AOT-safe: no reflection, no `Type.GetType`, no dynamic code.
- Never emit a `union` declaration. Use the full text "custom union type" when that distinction matters.
- Emit fully-qualified literal type names for case types (the framework rejects a `TypeReference` whose
  identity is empty, which is what a type parameter produces). This keeps generics, arrays and nullable
  types exact.
- Do not add `InternalsVisibleTo` between the generator and the code-fix project. Share only public
  identities (`UnionsDiagnosticIds`), and keep them cross-checked by a test.

## Diagnostics rules

- Prefix `UNI`. Every reported rule is in `AnalyzerReleases.Unshipped.md` (RS2008). Suppression
  descriptors are not release-tracked. `UNI0000` and `UNI9001` are suppression descriptors, not rules: they
  suppress `CA1034` and `CA1515` on a union's public nested `IUnionMembers` provider.
- The blocking policy lives beside the descriptors (`DiagnosticLibrary.IsBlocking`); there is one policy
  for both hosts.
- A rule is reported once. The analyzer reports per-target findings; the generator applies the same
  findings to `GeneratorResult.ShouldProcess` and reports only `UNI9000`.
- Do not suppress warnings with `NoWarn`, `#pragma warning disable` or `SuppressMessage` in product code.
  The only suppressions kept repo-wide are `Purview.BuildSdk`'s `PDS0006` for `CA1815` on a union
  declaration, this package's `UNI0000` `DiagnosticSuppressor` for `CA1034` and `UNI9001` for `CA1515` on a
  union's `IUnionMembers` provider, and the deliberate `RSEXPERIMENTAL006` at the `ITypeSymbol.IsUnion` call
  in a test.

## Packaging rules

- `Purview.Unions` is one package: runtime under `lib/<tfm>`, the merged generator and the code fix under
  `analyzers/dotnet/cs`, `buildTransitive/Purview.Unions.props`, the README and the logo.
- The generator is packed from `GetPurviewMergedAnalyzerFile`, never as a loose
  `Purview.SourceGeneratorFramework.dll`. The runtime project must not reference the generator as an
  analyzer (it would run on itself); the pack target builds it.
- `DisableUnionsSourceGenerator` is declared as both a `PurviewGeneratorVisibleProperty` and a
  `CompilerVisibleProperty`, and shipped in `Sdk/buildTransitive/Purview.Unions.props` (PSGF0003).
- Package validation is enabled with no baseline at `0.1.0-prerelease.1`; set the baseline when the first
  stable ships.

## Skills rules

- Authored skills live in `skills/<name>/SKILL.md` and carry the `purview-unions` prefix.
- Installed upstream skills live in `.agents/skills/` only and are recorded in `skills-lock.json`; the
  authored skills are never copied there. `Repository.UnitTests` enforces both. Never add `.claude/` or
  `CLAUDE.md`.
- `just skills-check` reports drift; `just skills-update` restores the recorded set.

The four published skills, authored for consumers of the package:

| Skill | Answers |
| --- | --- |
| `purview-unions-quickstart` | install, compiler requirements, a first union, and the no-generator fallback |
| `purview-unions-modelling` | which declaration surface to use, how to model cases, and why `Value` boxes while patterns do not |
| `purview-unions-diagnostics` | what a `UNI0001`–`UNI0016`/`UNI9000` finding means and how to fix it |
| `purview-unions-interop` | Results, Telemetry, ZodSharp, JSON, trimming and AOT, including the follow-ups |

## Testing rules

- TUnit and Microsoft.Testing.Platform. Filter with `--treenode-filter`; the default is `/*/*/*/*`.
- Cross-platform: no Windows paths or separators in code or tests. Locate the repository by walking up to
  a marker file, never by a hard-coded absolute path.
- Tests never invoke the network. Coordination with external processes goes through the repository's
  scripts, not a test.
- Every diagnostic needs a positive case; every fix needs a test. The compiler experiments in
  `CompilerBehaviourTests` are permanent — they are the evidence for the design.

## Performance rules

- The benchmark project is `src/src/Benchmarks`; run it with `just perf-tests` (Release, net10.0) or
  `just perf-tests-net11`. Never benchmark a Debug build.
- Benchmarks are evidence for the README's allocation claims, not a CI gate: shared-runner timings are
  noise. Add or change a suite when a hot path or a claim changes, and record the results in
  `docs/wiki/Performance.md`.
- `Value` boxes a value-type case by design; a suite that shows an allocation there is correct. An
  allocation on construction, `TryGetValue`, `is`, `switch` or `HasValue` is a regression.

## Canonical commands

| Command | Purpose |
| --- | --- |
| `just restore` | restore the solution |
| `just build` | build the solution |
| `just test` | run every test project on both target frameworks |
| `just perf-tests` | run the BenchmarkDotNet suite on .NET 10 (Release) |
| `just perf-tests-net11` | run the BenchmarkDotNet suite on .NET 11 (Release) |
| `just lint-check` / `just lint-fix` | CSharpier check / format |
| `just pipeline-pr` | restore, build, lint and test |
| `just pipeline-pack-validate` | pack and validate package contents |
| `just verify-pack-rules` | prove every forbidden-content rule rejects a package |
| `just smoke-packed-consumers` | pack, then build and run consumers of the packages |
| `just test-telemetry-integration` | prove `Purview.Telemetry` 6.0.0 reads a generated union (needs the local prerelease feed) |
| `just skills-update` | install/refresh the skills recorded in `skills-lock.json` |
| `just skills-check` | report drift between the installed skills and `skills-lock.json` |
| `just scrub` | remove build output and restart from a clean tree |

## Guardrails

- Do not modify another Purview repository; record cross-repository work as a follow-up in
  `docs/wiki/Interop.md`.
- Do not hardcode paths, platform separators or branch names.
- Do not disable the generator to make a test pass; fix the rule or the emitted shape.
- Do not silently change the design when an experiment fails. Use the documented fallback and record it in
  `docs/wiki/Union-Design.md`.
- Keep the merge, the package and the tests honest: if a claim is in the README, a test proves it.
