# Union design

## The problem

The C# `union` keyword lowers to a struct with a single `object? Value`. That is compact, but every
value-type case is boxed on entry, and matching a case goes through `Value` and boxes again. The language
reference offers two escapes: **custom union types** and the **non-boxing access pattern**. This package
generates that shape.

A custom union is any class or struct carrying `System.Runtime.CompilerServices.UnionAttribute` and
following the language's union pattern. The compiler recognises it by metadata name, not by the assembly
that defines it, so an internal polyfill in the consumer's own compilation is enough on .NET 10.

## The compiler contract (verified, not assumed)

The following were read from the C# 15 union proposal and the compiler's own metadata, then proven by the
experiments kept in `src/tests/SourceGenerator.UnitTests/CompilerBehaviourTests.cs`.

| Fact | Evidence |
| --- | --- |
| `UnionAttribute` exists on `net11.0` with a parameterless constructor and `[AttributeUsage(Class \| Struct, AllowMultiple = false)]` | `Microsoft.NETCore.App.Ref/11.0.0/ref/net11.0/System.Runtime.dll` |
| `IUnion` has exactly one member, `object? Value { get; }` | same metadata |
| Case types are the parameter types of public single-parameter constructors (by value or `in`) | proposal |
| With a nested `IUnionMembers` provider, union members are found **only** on the provider | proposal |
| A nullable value-type case is normalised to its underlying type (`int?` → case `int`) | proposal (resolved) |
| The compiler does not synthesise `UnionAttribute`/`IUnion` | proposal (resolved) |
| An implicit user-defined conversion shadows a union conversion; only one user-defined conversion may participate in a sequence | proposal |
| `Value` is mandatory and boxes a value-type case when read | proposal + experiment 7 |

## The Phase 0 experiments

All seven are permanent tests. Their results are recorded here so a future change cannot silently switch
the design.

| # | Experiment | Result |
| --- | --- | --- |
| 1 | A `[Union]` emitted by the generator on the author's partial struct produces implicit conversions and exhaustive switches | **Passes.** The attribute is emitted in the same compilation as the user's code, and the compiler applies union semantics. |
| 2 | Source `[Union]` + generated constructors | **Passes.** This is the safe fallback if attribute emission ever regresses; the code fix for `UNI0007` supports it. |
| 3 | `static partial` factory signatures inside the union | **Passes.** The generated implementation part binds before the caller needs it. |
| 4 | A nested `IUnionMembers` provider whose `Create` bodies come from the source | **Passes, with the documented fallback.** A generator cannot add bodies to a source-declared interface, so the provider owns one-line `Create` bodies that delegate to the generated `CreateCaseN` methods. |
| 5 | `ITypeSymbol.IsUnion` for a generated union | **True.** This is why the analyzer and the `PDS0006` suppressor work; `Results` still cannot see generated constructors (see [Interop](Interop.md)). |
| 6 | Internal polyfills on `net10.0`, no duplicate on `net11.0` | **Passes.** On `net10.0` the compiler recognises an internal `UnionAttribute` defined in the compilation. On `net11.0` no polyfill is emitted and the BCL type is used. |
| 7 | No boxing in `is`/`switch`/`TryGetValue`, boxing in `Value` | **Passes.** Measured with `GC.GetAllocatedBytesForCurrentThread()`: 0 bytes for `is`, `switch` and `TryGetValue`, 24 bytes for `Value`. |

## The polyfill decision

The brief asked for the polyfills from `RegisterPostInitializationOutput`. That method cannot inspect the
compilation, so it would emit a second `UnionAttribute` on `net11.0` unless it were guarded by a
preprocessor symbol — and a custom compiler host that omits `NET11_0_OR_GREATER` would then get `CS0436`.

The shipped implementation instead emits the polyfills from a **normal source output** gated on
`compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute") is null`, with at
least one opted-in union and the generator enabled. That is precise:

- `net10.0` gets internal `[Embedded]`/`[CompilerGenerated]` `UnionAttribute` and `IUnion`;
- `net11.0` gets neither, so a duplicate is impossible;
- a compilation that never applies `[GenerateUnion]` gets no polyfills at all.

The deviation and its reason are deliberate, and this document is the record.

## Emitted shape

For `[GenerateUnion(typeof(int), typeof(string))] public readonly partial struct IntOrString`, the generator
emits (abridged):

```csharp
[global::System.Runtime.CompilerServices.Union]
public readonly partial struct IntOrString : global::System.Runtime.CompilerServices.IUnion
{
    readonly int _case0;
    readonly string? _case1;
    readonly byte _tag;

    public IntOrString(int value) { _case0 = value; _case1 = default; _tag = 1; }

    public IntOrString(string? value)
    {
        if (value is null) return;
        _case0 = default; _case1 = value; _tag = 2;
    }

    public object? Value => _tag switch { 1 => _case0, 2 => _case1, _ => null };
    public bool HasValue => _tag switch { 1 => true, 2 => _case1 is not null, _ => false };

    public bool TryGetValue(out int value) { value = _case0; return _tag == 1; }
    public bool TryGetValue(out string value) { value = _case1!; return _tag == 2 && _case1 is not null; }
}
```

Design points:

- **Tag sizing is automatic**: `byte` up to 255 cases, then `ushort`; beyond `ushort.MaxValue` is `UNI0006`.
- **`HasValue` is `Value is not null`**, per active case. A non-nullable value-type case is never null once
  its tag is set; a reference or nullable case is tested.
- **`TryGetValue` returns true only for a non-null active case.** A nullable value-type case is stored as its
  underlying value, so a null constructor argument leaves `Tag == 0`.
- **Reference-type parameters are nullable** (`string?`), so a consumer can pass `null` without a warning and
  get the empty union the language requires.
- **No equality, `ToString`, `Deconstruct`, `Match` or `Switch`** for generated unions in v1. A union is
  matched by its active case; `CA1815` is expected and `PDS0006` answers it. A `record` union gets the
  compiler's record equality over the tag and fields.
- **Accessibility is never widened.** The partial declaration repeats the union's accessibility, and every
  case type must be public because a public constructor cannot expose a less accessible type.
- **Fully-qualified literal type names** are emitted for case types, so type parameters, arrays and nullable
  types render exactly without relying on a `TypeReference` identity that a type parameter does not have.

## Case resolution

Cases come from the first source present, in this order:

1. the `[GenerateUnion]` `typeof` list;
2. the union's own partial static factory signatures;
3. a nested `IUnionMembers` provider;
4. existing public single-parameter constructors.

A second, differing non-constructor source is `UNI0014`; existing constructors that differ are `UNI0012`.
When an existing constructor set agrees with a declared list, the author owns the creation members and the
generator emits only the attribute and, where it can, the access members — it never duplicates constructors.

## Why not the `union` keyword

`union` declarations are useful and compact, but they always box. This package exists for the cases where
that matters. It never emits a `union` declaration.
