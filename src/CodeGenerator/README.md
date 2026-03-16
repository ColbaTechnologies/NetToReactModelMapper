# CodeGenerator

Roslyn incremental source generator that reads C# types at compile time and produces TypeScript definitions from them.

## What it does

At compile time it scans the project for types decorated with `[FrontendModelAttribute]`. For each one it generates a TypeScript fragment. All fragments are joined and embedded as a string constant inside the compiled assembly under `GeneratedTypeScriptContent.Content`.

No files are written to disk here — that is the job of **TsxWriter**.

## Supported C# targets

`class` · `interface` · `enum`

## Type mapping

| C# | TypeScript |
|----|-----------|
| `string` | `string` |
| `int`, `long`, `short`, `byte`, `double`, `float`, `decimal` | `number` |
| `bool` | `boolean` |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Guid` | `string` |
| `object` | `unknown` |
| `List<T>`, `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyList<T>` | `T[]` |
| `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` | `Record<K, V>` |
| `T?` / `Nullable<T>` | optional property (`field?: T`) |
| Custom class / interface | type name (no namespace) |
| Enum | string union (`"A" \| "B"`) |

## Generation rules

- Property names are converted to camelCase (`MyProp` → `myProp`).
- Only public instance properties are emitted. Private, static members, indexers, and methods are ignored.
- Nullable properties become optional (`?`).
- Classes with a base class emit an `extends` clause.
- Generic type parameters are preserved (`Wrapper<T>`).

## Output

A single generated C# file `__ts_models.g.cs` containing:

```csharp
internal static class GeneratedTypeScriptContent
{
    public const string Content = @"// AUTO-GENERATED — do not edit
export interface MyModel {
  id: number;
}
";
}
```

## Project setup

- Target framework: `netstandard2.0` (required by Roslyn)
- Referenced in the consuming project as an `Analyzer` with `ReferenceOutputAssembly=false`