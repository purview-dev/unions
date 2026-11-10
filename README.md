# Purview.Unions

[![NuGet version](https://img.shields.io/nuget/v/Purview.Unions.svg)](https://www.nuget.org/packages/Purview.Unions)
[![Release](https://github.com/purview-dev/unions/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/unions/actions/workflows/release.yml)

Dependency-free **C# 15 unions that do not box value-type cases**.

The `union` keyword always stores its content in a single `object?`, so every value-type case is boxed on
entry. `Purview.Unions` generates the language's **custom union type** shape instead: each case gets its own
typed field, a public constructor, and a non-boxing `TryGetValue`. The compiler still treats it as a union —
implicit conversions, exhaustive `switch`, and `is`/`null` patterns all work — but construction and pattern
matching never allocate.

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

## Package

| Package | Purpose |
| --- | --- |
| `Purview.Unions` | The `[GenerateUnion]` attribute, the source generator and code fixes, and the hand-written `Union<T1, T2>` |

```bash
dotnet add package Purview.Unions
```

The package targets `net10.0` and `net11.0` and carries the merged generator and its code fixes under
`analyzers/dotnet/cs`, so one `PackageReference` is all a consumer needs.

## The four surfaces

### A — a `typeof` list (non-generic)

```csharp
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;
```

The generator emits the constructors, storage and access members, and the
`[System.Runtime.CompilerServices.Union]` attribute the compiler recognises.

### B — partial factory signatures (generic-capable)

```csharp
[GenerateUnion]
public readonly partial struct Option<T>
{
    internal static partial Option<T> Some(Some<T> value);
    internal static partial Option<T> None(None value);
}
```

The case type is each factory's single parameter. Use this when a case names a type parameter, because
`typeof(T)` is not a legal attribute argument (`CS0416`). The generator supplies the bodies and derives the
public constructors from them.

### C — a nested `IUnionMembers` provider (generic-capable)

```csharp
[GenerateUnion]
public readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
    public interface IUnionMembers
    {
        static Outcome<T> Create(T? value) => Outcome<T>.CreateCase0(value);
        static Outcome<T> Create(Exception? error) => Outcome<T>.CreateCase1(error);

        object? Value { get; }
        bool HasValue { get; }
        bool TryGetValue(out T value);
        bool TryGetValue(out Exception value);
    }
}
```

When a union directly contains a public nested interface named `IUnionMembers`, the language finds every
union member there instead of on the union. The generator emits the storage and `CreateCaseN` methods; the
provider owns its one-line `Create` bodies, because a source generator cannot add bodies to an interface you
declared.

### D — hand-written `Union<T1, T2>`

```csharp
Purview.Unions.Union<int, string> value = 42;

if (value.TryGetValue(out int number))
    Console.WriteLine(number);       // 42
```

No generator, no attributes. On `net10.0` it is an ordinary struct with the same API; on `net11.0` it also
carries `[Union]`, so it is a compiler union there too.

## Boxing: `Value` boxes, patterns do not

Every union satisfies the language contract with `public object? Value { get; }`, and reading `Value`
boxes a value-type case. That is inherent to the contract and is documented rather than hidden.

What the compiler uses for patterns is the **non-boxing access pattern** the generator emits:

| Operation | Allocates? |
| --- | --- |
| `value = 42` (union conversion) | No |
| `value.TryGetValue(out int number)` | No |
| `value is int` | No |
| `value switch { int n => …, string s => … }` | No |
| `value is null` (`HasValue`) | No |
| `value.Value` | **Yes**, for a value-type case |

`tests/SourceGenerator.UnitTests/GeneratedUnionRuntimeTests` asserts exactly this with
`GC.GetAllocatedBytesForCurrentThread()`.

## Declaration kinds

`struct`, `readonly struct`, `class`, `record struct` and `record class` are all supported. A class union uses
a null reference as the empty union; a struct union uses `default`.

A nullable value-type case is normalised to its underlying type: `int?` becomes case `int`, the constructor
takes `int?`, and `TryGetValue(out int)` returns false for null — a null value leaves the union empty.

## Requirements

- A **C# 15 compiler**: the .NET 11 SDK (or later) with `LangVersion=preview` in the project that declares a
  union.
- Target `net10.0` or `net11.0`. On `net10.0` the generator emits internal `[Embedded]` polyfills for
  `UnionAttribute`/`IUnion`; on `net11.0` the BCL types are used.
- Do **not** set `EnablePreviewFeatures`. It emits `RequiresPreviewFeatures` assembly-wide and makes `CA2252`
  fire on every member a consumer touches. `LangVersion=preview` is the only switch needed.

The generated union is `CA1815`-clean: `Purview.BuildSdk`'s `PDS0006` suppresses the warning for a union
declaration, because a union is matched by its active case rather than compared by value. No `#pragma` is
needed.

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `UNI0001` | Error | The union declaration is not `partial` |
| `UNI0002` | Error | Unsupported declaration kind |
| `UNI0003` | Error | The union declares no cases |
| `UNI0004` | Error | Duplicate case type |
| `UNI0005` | Error | A case type is not public |
| `UNI0006` | Error | More cases than the tag can address |
| `UNI0007` | Error | `[Union]` is required but missing (fallback path) |
| `UNI0008` | Error | Two case types are implicitly convertible |
| `UNI0009` | Error | A case type cannot be a union case |
| `UNI0010` | Error | A nullable value-type case has no usable underlying form |
| `UNI0011` | Error | A generic union has no source-visible case list |
| `UNI0012` | Error | Existing constructors and the declared case list disagree |
| `UNI0013` | Error | The union is nested in a type the generator cannot name |
| `UNI0014` | Error | More than one case list is present and they conflict |
| `UNI0015` | Warning | A single-case union is degenerate |
| `UNI0016` | Error | The declaration collides with a required member |
| `UNI9000` | Error | The generator failed unexpectedly |

The package also ships code fixes that add the missing `partial` modifier (`UNI0001`) and, on the documented
fallback path, the `[Union]` attribute (`UNI0007`).

## Documentation

- [Getting Started](docs/wiki/Getting-Started.md)
- [Union Design](docs/wiki/Union-Design.md) — the compiler experiments and the polyfill decision
- [Diagnostics](docs/wiki/Diagnostics.md)
- [Interop](docs/wiki/Interop.md) — Results, Telemetry, ZodSharp and the follow-ups
- [Trimming and AOT](docs/wiki/Trimming-AOT.md)

## Agent skills

The `purview-unions-modelling`, `purview-unions-diagnostics` and `purview-unions-interop` skills cover this
package. Install them from the repository:

```bash
npx skills add purview-dev/unions
```

## Examples

`src/src/Examples` is a runnable console sample of all four surfaces:

```bash
dotnet run --project src/src/Examples
```

## License

MIT — see [LICENSE.md](LICENSE.md).
