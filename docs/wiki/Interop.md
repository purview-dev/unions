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

`[GenerateResult]` recognises a `[GenerateUnion]` target by metadata identity and reads the union's
**source-visible** case list instead of relying on generated output. A generated union therefore works with
Results without the author writing `[Union]` or constructors:

- surface A (`typeof` list) and surface B (partial factory signatures) are read from the source;
- surface C (`IUnionMembers` provider) is the portable source-visible route when another generator also needs
  the case list;
- a `[GenerateUnion]` target with no source-visible case list is reported as `RSG1001` by Results.

The Results tests use a stand-in `GenerateUnionAttribute` and source-visible conversions because
`Purview.Unions` is not on a CI-safe package feed yet, so the real two-generator integration test remains a
follow-up in `purview-dev/results`.

Do not hand-write the constructors to satisfy Results: the union generator would then treat the union as
hand-written and the declared and existing lists would disagree (`UNI0012`/`UNI0014`).

A hand-written `[Union]` union whose cases live on a nested `IUnionMembers` provider — the shape the Results
tests use — needs no `SuppressMessage` attribute for `CA1000`. The package's `UNI9002` suppression descriptor
answers the rule on the provider's static `Create` members, which are generic whenever the union is. See
[Diagnostics](Diagnostics.md).

### Follow-up

> `purview-dev/results`: add the two-generator integration test once `Purview.Unions` is on a CI-safe package
> feed.
>
> `purview-dev/results`: once a `Purview.Unions` version carrying `UNI9002` is on the feed, bump
> `PurviewUnionsVersion` in `Directory.Packages.props` and delete the `CA1000` `SuppressMessage` from
> `ProvidedOption<T>.IUnionMembers` in `src/tests/SourceGenerator.UnitTests/GeneratedTestUnions.cs`. Verify by
> raising `CA1000` to `warning` in a temporary project-local `.editorconfig` under that test project: the build
> must report no `CA1000` for the provider's static `Create` members.

## Purview.Telemetry

`Purview.Telemetry.SourceGenerator` 6.0.0 recognises a `[Purview.Unions.GenerateUnion]` target and reads the
source-visible case list in the same order as this package: the `typeof` list, partial factory signatures, a
nested `IUnionMembers` provider, then source-visible constructors. It emits the accessor against the
`HasValue`/`TryGetValue` members the unions generator emits, so a surface A/B/C union is unwrapped to its case
value without boxing the union.

`just test-telemetry-integration` proves the real two-generator path from the packages: it packs
`Purview.Unions`, references `Purview.Telemetry.SourceGenerator` 6.0.0 from `LOCAL_NUGET_FEED_PATH` (or
`TELEMETRY_PRERELEASE_FEED`), and asserts the generated accessor calls `TryGetValue` for each case on
`net10.0` and `net11.0`.

- Surface A/B/C work; surface C remains the portable source-visible route for another generator that does not
  read the `[GenerateUnion]` list.
- A `[GenerateUnion]` target with no source-visible case list is not treated as a union by Telemetry; the
  unions generator reports `UNI0003`.
- A telemetry parameter typed as `Purview.Unions.Union<T1, T2>` is visible as a union on `net11.0`, where the
  package assembly carries `[Union]`.

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
