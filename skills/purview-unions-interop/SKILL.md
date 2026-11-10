---
name: purview-unions-interop
description: "Use when a Purview.Unions union meets another library — Purview.Results error unions, Purview.Telemetry parameters, ZodSharp, JSON serialisation, trimming and Native AOT — or when deciding which downstream generators can see a generated union's cases."
---

# Purview.Unions interoperability

A union is a plain type carrying `System.Runtime.CompilerServices.UnionAttribute`. Everything that
consumes it does so through ordinary metadata, so interop is mostly about **what is visible in source**.

## The source-visibility rule

Roslyn generators never see each other's output. A downstream generator sees:

- the declaration and the attributes written in the user's source;
- the constructors, `Value`, `HasValue` and `TryGetValue` members **only if the author wrote them**;
- never the members this generator emits.

So a downstream generator can discover a union's cases only when the case list is source-visible. That
is why surfaces B and C exist, and why surface A (`typeof` only) is invisible to them.

## Purview.Results

`[GenerateResult]` requires `ITypeSymbol.IsUnion` and reads the union's public single-parameter
constructors. For a union generated here:

- the `[Union]` attribute is emitted by this generator, so Results' generator cannot see it;
- the constructors are generated, so Results cannot see them either.

A Results error union therefore needs `[Union]` and the constructor set written in source. **This is a
known gap** and a follow-up in `purview-dev/results`: `[GenerateResult]` should read the source case list
(the `typeof` list, partial factories or a provider) instead of the generated constructors.

The integration test is marked `Skip` with the follow-up link; do not work around it by hand-writing the
constructors, because that is `UNI0012`/`UNI0014`.

## Purview.Telemetry

The telemetry generator's `UnionHelpers` prefers a nested `IUnionMembers` provider's `Create` methods,
then falls back to public constructors, and treats the non-boxing pattern as present when `HasValue` and
one `TryGetValue` per case exist.

- **Surface C works today**: the provider is in source, and the union implements the required
  `HasValue`/`TryGetValue` members.
- **Surface A does not**: the cases live only in the emitted constructors.
- A telemetry parameter typed as `Purview.Unions.Union<T1, T2>` is not detected as a union unless the
  `net11.0` asset's `[Union]` is visible — it is, because it is compiled into the package assembly.

## ZodSharp

`ZodSharp.Unions.Union<T1, T2>` and `NativeUnion<T1, T2>` predate this package. `Purview.Unions.Union<T1, T2>`
has the same two-case shape and a stable `IUnion` (`Value` + `Tag`). Migrating ZodSharp to it is a
follow-up in `purview-dev/zodsharp`; do not mix the two in one public contract meanwhile.

## JSON, EF Core and ASP.NET Core

Out of scope for v1. A union serialises as the runtime shape of whatever `Value` holds if you serialise
it directly, which is rarely what you want. Map a union to a DTO or a `ProblemDetails` in the host, and
keep the union as the domain contract. EF Core mapping and a `JsonConverter` are follow-ups.

## Trimming and Native AOT

Everything shipped is trim- and AOT-safe:

- the runtime is dependency-free and `IsAotCompatible`;
- `Union<T1, T2>` uses only typed fields, `EqualityComparer<T>.Default` and the static
  `typeof(T).IsValueType`/`Nullable.GetUnderlyingType` metadata checks — no dynamic code, no
  `Type.GetType`, no member reflection;
- generated unions use fields, constructors, a `switch` over a tag and `TryGetValue` overloads.

A trimmed or Native AOT application can use any surface. On .NET 10 the generator emits internal
`[Embedded]` polyfills for `UnionAttribute`/`IUnion`; they do not appear in the public surface.

## .NET 10 versus .NET 11

| | `net10.0` | `net11.0` |
| --- | --- | --- |
| Compiler | C# 15 (`LangVersion=preview`) | C# 15 |
| `UnionAttribute`/`IUnion` | internal polyfills emitted by the generator | BCL types |
| `Union<T1, T2>` | plain struct with the same API | also `[Union]`/`IUnion`; exhaustive and non-boxing patterns |
| Generated unions | compiler unions via the polyfill | compiler unions via the BCL |

`LangVersion=preview` is the only switch needed. Do not set `EnablePreviewFeatures`: it emits
`RequiresPreviewFeatures` assembly-wide and makes `CA2252` fire on every member a consumer touches.

## Checklist

1. If another generator must see the cases, use surface B or C, never surface A.
2. Do not hand-write constructors to satisfy a downstream generator; that is a conflict.
3. Keep unions out of serialisation contracts until a converter exists; map at the boundary.
4. Verify a trimmed/AOT consumer after upgrading the package.
