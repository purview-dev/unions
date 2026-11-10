# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
