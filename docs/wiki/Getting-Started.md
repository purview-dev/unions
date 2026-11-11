# Getting started

## Install

```bash
dotnet add package Purview.Unions
```

`Purview.Unions` targets `net10.0` and `net11.0` and carries the source generator and its code fixes under
`analyzers/dotnet/cs`. One `PackageReference` is enough.

## Declare a union

```csharp
using Purview.Unions;

namespace Tenancy;

public readonly record struct TenantNotFound(TenantId TenantId);
public readonly record struct TenantDisabled(TenantId TenantId);

[GenerateUnion(typeof(TenantNotFound), typeof(TenantDisabled))]
public readonly partial struct TenantError;
```

The declaration must be `partial` and a `struct`, `readonly struct`, `class` or record. The generator adds:

- one public constructor per case,
- a typed field per case and a tag,
- `public object? Value`,
- `public bool HasValue`,
- `public bool TryGetValue(out TCase value)` per case,
- the `[System.Runtime.CompilerServices.Union]` attribute.

## Use it

```csharp
TenantError error = new TenantNotFound(tenantId);   // implicit union conversion

var message = error switch
{
    TenantNotFound notFound => $"tenant {notFound.TenantId} was not found",
    TenantDisabled disabled => $"tenant {disabled.TenantId} is disabled",
    null => "no error",
};
```

`switch` is exhaustive over the case types, and the `null` arm handles an empty (default) struct union. The
compiler uses the generated `TryGetValue`/`HasValue` for those patterns, so no case is boxed.

## Generic unions

`typeof(T)` is `CS0416`, so a generic union names its cases in source:

```csharp
public readonly record struct Some<T>(T Value);
public readonly record struct None;

[GenerateUnion]
public readonly partial struct Option<T>
{
    internal static partial Option<T> Some(Some<T> value);
    internal static partial Option<T> None(None value);
}

Option<int> some = new Some<int>(42);
```

A nested `IUnionMembers` provider is the alternative when another generator must see the case list; the
provider is public, so the package's `UNI0000` and `UNI9001` suppressors handle the resulting `CA1034` and
`CA1515`. See [Union Design](Union-Design.md).

## No generator

```csharp
Purview.Unions.Union<int, string> value = 42;
if (value.TryGetValue(out int number)) { }
```

`Union<T1, T2>` is available on every target framework and is also a compiler union on `net11.0`.

## Requirements

- The .NET 11 SDK (C# 15, GA).
- Target `net10.0` or `net11.0`.
- A `net11.0` project gets C# 15 by default, so no `LangVersion` is needed. A `net10.0` project defaults to
  C# 14, so set `<LangVersion>latest</LangVersion>` in the project that declares a union.
- Do not set `EnablePreviewFeatures`; `LangVersion=latest` (and only on `net10.0`) is the only switch needed.

On `net10.0` the generator emits internal `[Embedded]` polyfills for `UnionAttribute`/`IUnion`. On `net11.0`
the BCL types are used and no polyfill is emitted.

## Turn the generator off

`DisableUnionsSourceGenerator=true` (MSBuild property) disables generation and the polyfills. It is useful
when inspecting the declared source in isolation; it is not a way to make a failing build pass.

## Where next

- [Union Design](Union-Design.md)
- [Diagnostics](Diagnostics.md)
- [Interop](Interop.md)
- [Trimming and AOT](Trimming-AOT.md)
