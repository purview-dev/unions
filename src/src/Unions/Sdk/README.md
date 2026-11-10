# Purview.Unions

[![NuGet version](https://img.shields.io/nuget/v/Purview.Unions.svg)](https://www.nuget.org/packages/Purview.Unions)

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

- .NET 11 SDK (C# 15) with `LangVersion=preview` in the project that declares a union.
- `net10.0` or `net11.0`. On `net10.0` the generator emits internal `[Embedded]` polyfills; `net11.0` uses
  the BCL types and never receives a duplicate.
- Do not set `EnablePreviewFeatures`; `LangVersion=preview` is enough.

## Supported declarations

`struct`, `readonly struct`, `class`, `record struct` and `record class`, non-generic and generic, with
classes, structs, records, interfaces, type parameters, nullable value types and other unions as cases.

A nullable value-type case is exposed through its underlying type: `int?` becomes case `int`, the constructor
takes `int?`, and `TryGetValue(out int)` returns false for null.

## Diagnostics

`UNI0001`–`UNI0016` and `UNI9000`. See `docs/wiki/Diagnostics.md` in the repository. Code fixes add a missing
`partial` (`UNI0001`) and, on the fallback path, the `[Union]` attribute (`UNI0007`).

## Switches

| Property | Default | Purpose |
| --- | --- | --- |
| `DisableUnionsSourceGenerator` | `false` | Disables generation and the polyfills |

## Licence

MIT.
