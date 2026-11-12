# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A `just test-telemetry-integration` gate that packs `Purview.Unions` and runs a `net10.0` and a `net11.0`
  consumer against `Purview.Telemetry.SourceGenerator` 6.0.0, asserting the generated telemetry accessor uses
  `TryGetValue` for every case.
- A consumer `purview-unions-quickstart` skill covering install, compiler requirements, a first union and
  the no-generator fallback.
- An [Agent Skills](docs/wiki/Agent-Skills.md) wiki page that maps each published skill to the task it
  answers.
- A `UNI0000` `DiagnosticSuppressor` that suppresses `CA1034` and a `UNI9001` suppressor that suppresses
  `CA1515` on the public nested `IUnionMembers` provider the C# union contract requires. Both ship with the
  package's analyzer, so a surface-C union needs no suppression attribute.
- A `UNI9002` `DiagnosticSuppressor` that suppresses `CA1000` (do not declare static members on generic
  types) on a generic union's `IUnionMembers` provider static `Create` members. The members are the
  contract's creation members and the provider is necessarily generic when the union is, so `CA1000` cannot
  be satisfied without removing the union semantics. This lets the `purview-dev/results`
  `ProvidedOption<T>.IUnionMembers` provider drop its `SuppressMessage` workaround.
- A `UNI0005` code fix that makes the case type and every containing type public.

### Fixed

- The Example app's internal unions build again: `UNI0005` now allows a case type that is at least as
  accessible as the union, so an internal union can use internal case types.

### Changed

- **`Purview.Telemetry` v6 supports `[GenerateUnion]`.** The telemetry generator reads the source-visible case
  list and emits the accessor against the generated `HasValue`/`TryGetValue` pattern. `just
  test-telemetry-integration` proves both generators in one compilation on `net10.0` and `net11.0`.
- **`Purview.Results` now supports `[GenerateUnion]` unions.** Its `[GenerateResult]` discovery recognises the
  attribute by metadata identity and reads the source-visible case list, so a generated union no longer needs
  hand-written `[Union]` or constructors for Results. The Interop documentation and cross-generator test surface
  reflect the closed Results gap; the Telemetry surface-A gap remains.
- **`UNI0005` is relative to the union's accessibility.** A public union still needs public case types, but an
  internal union can use internal case types; the diagnostic message and documentation now say “at least as
  accessible as the union”.
- **.NET 11 GA / C# 15 GA.** The repository compiles with `LangVersion=latest` instead of `preview`. A
  `net11.0` consumer needs no language-version switch; a `net10.0` consumer sets `latest` because its default
  is C# 14. The packed-consumer gate now proves both.
- The README, `AGENTS.md` and wiki now name all four published skills, and the documentation links and
  changelog guidance match the repository.

## [0.1.0-prerelease.1] - 2026-11-10

### Added

- `Purview.Unions`, a dependency-free package that generates the C# 15 **custom union type** shape so
  value-type cases are not boxed by construction, `TryGetValue`, `is` or `switch`.
- The `[GenerateUnion]` attribute and the four declaration surfaces:
  - a `typeof` case list for non-generic unions;
  - partial static factory signatures for generic unions;
  - a nested `IUnionMembers` provider for generic unions and source-visible case lists;
  - the hand-written `Purview.Unions.Union<T1, T2>` (surface D).
- Support for `struct`, `readonly struct`, `class`, `record struct` and `record class`, generic unions,
  nullable value-type cases, reference cases and nested unions.
- Internal `[Embedded]` polyfills for `System.Runtime.CompilerServices.UnionAttribute` and `IUnion` when a
  compilation targets `net10.0`, so generated unions are compiler unions there too; `net11.0` uses the BCL
  types and never receives a duplicate.
- A source generator that emits one output per union, with a value-equatable incremental pipeline, plus a
  bundled analyzer and the `UNI0001`–`UNI0016` / `UNI9000` diagnostics.
- IDE code fixes for the missing `partial` modifier (`UNI0001`) and, on the fallback path, the missing
  `[Union]` attribute (`UNI0007`).
- `Union<T1, T2>` carries `[Union]` on `net11.0`, so it is a compiler union there as well as a hand-written
  two-case union on `net10.0`.
- Documentation: README, `docs/wiki/`, `AGENTS.md`, and the `purview-unions-modelling`,
  `purview-unions-diagnostics` and `purview-unions-interop` skills.
- Tests covering all four surfaces, the compiler experiments, incremental caching, determinism, diagnostics,
  code fixes, both target frameworks and the no-boxing assertion.
