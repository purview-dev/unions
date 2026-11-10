# Trimming and Native AOT

Everything `Purview.Unions` ships is trimming- and Native AOT-safe.

## The runtime

The runtime package is dependency-free, `IsAotCompatible` and `SupportedPlatform=browser`. It contains:

- `GenerateUnionAttribute` — an ordinary attribute with a `Type[]` payload;
- `Purview.Unions.IUnion` — a read-only interface;
- `Union<T1, T2>` — typed fields, `TryGetValue`, `Match`/`Switch`, equality and `ToString`.

`Union<T1, T2>` uses `EqualityComparer<T>.Default` and two static runtime metadata checks,
`typeof(T).IsValueType` and `Nullable.GetUnderlyingType(typeof(T))`, to decide whether a value needs the null
test. Neither is member reflection, dynamic code or `Type.GetType`; both are AOT-safe and evaluated once per
closed generic.

There is no `System.Reflection` member access, no `Activator`, no `dynamic` and no runtime type discovery.

## Generated unions

A generated union is:

- one typed field per case;
- one public constructor per case;
- a `switch` over a small integer tag for `Value` and `HasValue`;
- one `TryGetValue` overload per case.

No reflection, no serialisation hooks, no dynamic code. The same members the compiler uses for pattern
matching are the only ones the generator emits.

## The .NET 10 polyfills

On `net10.0` the generator emits internal `[Embedded]`/`[CompilerGenerated]`
`System.Runtime.CompilerServices.UnionAttribute` and `IUnion` into the consumer's compilation. They are
internal, marked embedded, and not part of the public surface; trimming removes them if unused.

On `net11.0` no polyfill is emitted and the BCL types are used.

## Verifying a consumer

The publisher gate packs the package to an isolated feed and builds and runs consumers that reference only the
package, then leak-scans their output for compiler-host assemblies:

```bash
just smoke-packed-consumers
```

For a trimmed or AOT application of your own:

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishAot>true</PublishAot>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

A union used as a type parameter, a field or a local is a value type with a fixed set of fields, so the
trimmer has nothing to guess at. If you expose a union in a public API, the case types are still ordinary
types and their own trimming annotations apply.

## Blazor WebAssembly

The package's `SupportedPlatform` includes `browser` and it uses no browser-unsupported API. A Blazor
WebAssembly client can use any surface, including a generated union; `Value` boxes only when read.

## What is deliberately absent

- **No reflection-based enumeration of cases.** The case list is a compile-time concept; expose it yourself if
  a runtime consumer needs it.
- **No JSON converter.** A converter would need a discriminator policy and a resolver; that belongs in a
  separate package.
- **No `ToString`/`Deconstruct` on generated unions.** They would invite treating a union as a tuple; match
  the active case instead.
