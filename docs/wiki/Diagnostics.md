# Diagnostics

Prefix: `UNI`. The analyzer reports every per-target rule; the generator applies the same findings to decide
whether it can emit and reports only `UNI9000`. A rule is reported once.

| ID | Severity | Meaning |
| --- | --- | --- |
| `UNI0001` | Error | The union declaration is not `partial` |
| `UNI0002` | Error | Unsupported declaration kind (interface, enum, static/abstract class, `ref struct`) |
| `UNI0003` | Error | No case list was found |
| `UNI0004` | Error | Duplicate case type |
| `UNI0005` | Error | A case type is less accessible than the union |
| `UNI0006` | Error | More cases than a `ushort` tag can address |
| `UNI0007` | Error | `[Union]` is required but missing (fallback path only) |
| `UNI0008` | Error | Two case types are implicitly convertible |
| `UNI0009` | Error | A case type cannot be a union case |
| `UNI0010` | Error | A nullable value-type case has no usable underlying form |
| `UNI0011` | Error | A generic union uses a `typeof` case list |
| `UNI0012` | Error | Existing constructors disagree with the declared case list |
| `UNI0013` | Error | A containing type cannot be named from generated code |
| `UNI0014` | Error | Two case lists are present and they conflict |
| `UNI0015` | Warning | A single-case union is degenerate |
| `UNI0016` | Error | The declaration collides with a required member |
| `UNI9000` | Error | The generator failed unexpectedly |

Every error blocks its union: the generated constructors, storage and access members are one coherent shape,
so there is no partial output. `UNI0015` is the only warning and does not block.

`UNI0000`, `UNI9001` and `UNI9002` are **suppression descriptors**, not reported rules. `UNI0000` suppresses
`CA1034` (nested types should not be visible) and `UNI9001` suppresses `CA1515` (public types should be
internal) on a union's public nested `IUnionMembers` provider, which the C# union contract requires.
`UNI9002` suppresses `CA1000` (do not declare static members on generic types) on that provider's static
`Create` members, which are the contract's creation members and are necessarily generic when the union is.
Add any of these ids to `NoWarn` to turn that suppression off; a consumer should not need to.

## Details

### `UNI0001` — not `partial`

The generator adds a second partial declaration. Add `partial`; the package's code fix does it for you.

### `UNI0002` — unsupported declaration kind

The union pattern applies to classes and structs. Interfaces, enums, delegates, static classes, abstract
classes and `ref struct`s cannot be generated as unions.

### `UNI0003` — no cases

Declare the cases with a `typeof` list, partial factories, a nested `IUnionMembers` provider, or existing
public single-parameter constructors.

### `UNI0004` — duplicate case

Each case type appears once. A nullable value type and its underlying type are the same case (`int?` and
`int`).

### `UNI0005` — case type less accessible than the union

A case type must be at least as accessible as the union. A public union's creation members are public API, so
its case types must be public all the way up their containers. An internal union's creation members are
effectively internal, so an internal case type is enough. The code fix adds `public` to the case type and its
containing types.

### `UNI0006` — too many cases

The tag is sized automatically (`byte`, then `ushort`). This rule is only reachable beyond 65,535 cases; split
the union.

### `UNI0007` — `[Union]` required

The shipped generator emits the attribute itself, so this rule is not reported by default. It exists for the
documented fallback path; the code fix adds the attribute fully qualified.

### `UNI0008` — ambiguous creation

Two case types with an implicit conversion between them make a value convertible to both. Wrap one in a
distinct record (`record struct Count(int Value)`) so the conversion sets are disjoint.

### `UNI0009` — unsupported case type

Pointers, function pointers, `ref struct`s, `void`, static classes, `dynamic` and open generic definitions
cannot be stored in a union field or exposed by a public constructor.

### `UNI0010` — unusable nullable case

A nullable value-type case is exposed through its underlying type, and that type must be usable. This is a
defensive rule: a symbol the compiler accepts today but cannot store is reported instead of emitting malformed
code.

### `UNI0011` — generic union with a `typeof` list

`typeof(T)` is not a legal attribute argument (`CS0416`). Use partial factory signatures or a nested provider.

### `UNI0012` — constructors disagree

A hand-written union owns its creation members. When its public constructors differ from a declared case list,
the two contradict each other. Keep one source of cases, or make them agree.

### `UNI0013` — containing type cannot be named

Generated members are emitted as a second partial declaration nested inside the same containing types. Every
containing type must be non-generic and `partial`.

### `UNI0014` — conflicting case lists

More than one non-constructor source is present and they differ. Keep exactly one of the `typeof` list,
partial factories, or provider.

### `UNI0015` — single case

A union with one case adds a layer without adding a choice. Use the case type directly unless you need the
union pattern for evolution.

### `UNI0016` — required member collides

The generated shape owns `Value`, `HasValue` and `TryGetValue`. A union cannot be named `Value` (`CS0542`), and
it cannot already declare one of those members unless it is hand-written with its own constructors.

### `UNI9000` — unexpected failure

The generator catches its own failures so one malformed union cannot discard every other union's output
(`CS8785`). The message names the union and the exception. Please report it with the declaration.

## The `[Union]` fallback path

The generator emits `[System.Runtime.CompilerServices.Union]` in the same partial declaration as the members.
The compiler recognises the attribute in generated trees, so the author never writes it. If a future compiler
change ever prevents that, the generator can report `UNI0007` and the author adds the attribute themselves —
which surface B and C already allow, because the attribute names no type parameters.
