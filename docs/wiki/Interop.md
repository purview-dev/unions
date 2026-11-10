# Interop

A union is a plain type carrying `System.Runtime.CompilerServices.UnionAttribute`. Consumers see it through
ordinary metadata, so interop depends on **what is visible in the user's source**: Roslyn generators never see
each other's output.

| Surface | Cases in source? | Visible to another generator? |
| --- | --- | --- |
| A — `typeof` list | Yes, on the attribute | Only the attribute list, not the constructors |
| B — partial factories | Yes, as method signatures | Yes |
| C — `IUnionMembers` provider | Yes, as provider methods | Yes |
| D — `Union<T1, T2>` | n/a | The type and its `[Union]` on `net11.0` |

## Purview.Results

`[GenerateResult]` requires `ITypeSymbol.IsUnion` and discovers cases from public single-parameter
constructors. For a union generated here:

- the `[Union]` attribute is emitted into generated output, which Results' generator cannot see;
- the constructors are generated, which Results' generator also cannot see.

A Results error union therefore needs its attribute and constructors written in source. That is a **known
gap**, and the integration test is skipped with the follow-up link rather than worked around.

### Follow-up

> `purview-dev/results`: `[GenerateResult]` case discovery should read the source case list — the
> `[GenerateUnion]` `typeof` list, the partial factory signatures, or the nested `IUnionMembers` provider —
> instead of relying on the generated constructors.

Do not hand-write the constructors to satisfy Results: the generator would then treat the union as
hand-written and the declared and existing lists would disagree (`UNI0012`/`UNI0014`).

## Purview.Telemetry

Telemetry's `UnionHelpers` prefers a nested `IUnionMembers` provider's static `Create` methods, then falls
back to public constructors, and treats the non-boxing pattern as present when `HasValue` and a `TryGetValue`
per case exist.

- **Surface C works today**, because the provider is in source and the generated union implements the
  required `HasValue`/`TryGetValue` members.
- **Surface A** is invisible: the cases live only in generated constructors.
- A telemetry parameter typed as `Purview.Unions.Union<T1, T2>` is visible as a union on `net11.0`, where the
  package assembly carries `[Union]`.

### Follow-up

> `purview-dev/telemetry-sourcegenerator`: when a parameter's type is a `[GenerateUnion]` union, read the
> source case list in the same way.

## ZodSharp

`ZodSharp.Unions.Union<T1, T2>` and `NativeUnion<T1, T2>` predate this package. `Purview.Unions.Union<T1, T2>`
has the same two-case shape and a stable `Purview.Unions.IUnion` (`Value` + `Tag`). Migrating ZodSharp is a
follow-up; do not mix the two types in one public contract meanwhile.

### Follow-up

> `purview-dev/zodsharp`: migrate `ZodSharp.Unions.NativeUnion<T1, T2>`/`Union<T1, T2>` to
> `Purview.Unions.Union<T1, T2>` and keep `ZodSharp.Unions.IUnion` as an alias or remove it in a major version.

## JSON, EF Core and ASP.NET Core

Out of scope for v1:

- **JSON**: serialise a union by converting it to a DTO at the boundary. A `JsonConverter` would have to
  choose a discriminator policy, which belongs in a separate package.
- **EF Core**: a union is not a scalar; map its active case to columns in the owning type.
- **ASP.NET Core**: map a union to `ProblemDetails` in the host, as `Purview.Results.AspNetCore` already does
  for result failures.

### Follow-ups

> Add `Purview.Unions.Serialization` (source-generated `System.Text.Json` converter) and
> `Purview.Unions.EntityFrameworkCore` only when a consumer needs them; keep the core package
> dependency-free.

## The union shape is a compiler contract

Anything that reflects over the emitted members must handle the shape the compiler specifies: a public
`Value`, an optional `HasValue`, and optional `TryGetValue` overloads. Do not depend on the private field
names (`_case0`, `_tag`) — they are an implementation detail of this generator.
