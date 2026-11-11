# Purview.Unions

[![NuGet version](https://img.shields.io/nuget/v/Purview.Unions.svg)](https://www.nuget.org/packages/Purview.Unions)
[![Release](https://github.com/purview-dev/unions/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/unions/actions/workflows/release.yml)

Dependency-free **C# 15 unions that do not box value-type cases**. The `union` keyword stores one
`object?`, so every value-type case is boxed. This package generates the language's **custom union type**
instead: a typed field per case, a public constructor per case, and a non-boxing `TryGetValue`. The compiler
still applies union conversions, exhaustive `switch` and `is`/`null` patterns.

## Install

```bash
dotnet add package Purview.Unions
```

## Quick start

```csharp
using Purview.Unions;

[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;

IntOrString value = 42;

Console.WriteLine(value switch
{
    int number => $"int {number}",
    string text => $"string {text}",
    null => "empty",
});
```

The package carries the merged generator and code fixes under `analyzers/dotnet/cs`, so no second
`PackageReference` is needed.

## Declaration surfaces

| Surface | Shape |
| --- | --- |
| A | `[GenerateUnion(typeof(int), typeof(string))] public readonly partial struct IntOrString;` |
| B | `[GenerateUnion] public readonly partial struct Option<T> { internal static partial Option<T> Some(Some<T> value); }` |
| C | a `[GenerateUnion]` union implementing its own nested `IUnionMembers` provider |
| D | `Purview.Unions.Union<int, string> value = 42;` |

Use B or C for a generic union: `typeof(T)` is not a legal attribute argument (`CS0416`).

## Boxing

| Operation | Allocates? |
| --- | --- |
| union conversion, `TryGetValue`, `is`, `switch`, `is null` | No |
| reading `Value` | **Yes**, for a value-type case |

`Value` is part of the language's union contract and boxes. That is inherent to the contract; pattern
matching does not use it.

## Requirements

- .NET 11 SDK (C# 15, GA).
- `net10.0` or `net11.0`. A `net11.0` project gets C# 15 by default and needs no `LangVersion`; a `net10.0`
  project sets `<LangVersion>latest</LangVersion>` because its default is C# 14. On `net10.0` the generator
  emits internal `[Embedded]` polyfills; `net11.0` uses the BCL types and never receives a duplicate.
- Do not set `EnablePreviewFeatures`; `LangVersion=latest` (only on `net10.0`) is enough.

## Supported declarations

`struct`, `readonly struct`, `class`, `record struct` and `record class`, non-generic and generic, with
classes, structs, records, interfaces, type parameters, nullable value types and other unions as cases.

A nullable value-type case is exposed through its underlying type: `int?` becomes case `int`, the constructor
takes `int?`, and `TryGetValue(out int)` returns false for null.

## Diagnostics

`UNI0001`–`UNI0016` and `UNI9000`. See the
[Diagnostics](https://github.com/purview-dev/unions/blob/main/docs/wiki/Diagnostics.md) page. Code fixes add a
missing `partial` (`UNI0001`), make an inaccessible case type public (`UNI0005`) and, on the fallback path, the
`[Union]` attribute (`UNI0007`). `UNI0000` and `UNI9001` are suppression descriptors, not rules: they suppress
`CA1034` and `CA1515` on a union's nested `IUnionMembers` provider.

## Switches

| Property | Default | Purpose |
| --- | --- | --- |
| `DisableUnionsSourceGenerator` | `false` | Disables generation and the polyfills |

## Agent skills

The `purview-unions-quickstart`, `purview-unions-modelling`, `purview-unions-diagnostics` and
`purview-unions-interop` agent skills cover this package. No package ships skills: install them from the git
repository with the skills CLI:

```bash
npx skills add purview-dev/unions
```

## Licence

MIT.
