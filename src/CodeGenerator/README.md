# CodeGenerator

Roslyn `IIncrementalGenerator` that converts annotated C# types and ASP.NET Core controllers into TypeScript at compile time — zero runtime overhead.

## What it does

At compile time, the generator scans the consuming project for types decorated with `[FrontendModel]` or `[FrontendService]`. For each one it produces a TypeScript fragment. All fragments are embedded as **individual `public const string` fields** inside the compiled assembly in a generated class called `GeneratedTypeScriptContent`.

No files are written to disk here — that is the job of **SourceCodeGen.Runtime** or **TsxWriter**.

## Attributes

### `[FrontendModel]`

Applies to `class`, `interface`, and `enum`.

- **class / interface** → TypeScript `interface` (via `InterfaceFragmentGenerator`)
- **enum** → TypeScript `export type` string union (via `EnumFragmentGenerator`)

### `[FrontendService]`

Applies to ASP.NET Core controllers. No ASP.NET Core package reference is required in the consuming project — attributes are detected by class name only.

Generates a typed TypeScript service object (`export const {Name}Service = { ... }`) plus the `apiFetch` utility (via `ServiceFragmentGenerator`).

## Generators

| Class | Handles | Output |
|---|---|---|
| `InterfaceFragmentGenerator` | `class`, `interface` | `export interface {Name} { ... }` |
| `EnumFragmentGenerator` | `enum` | `export type {Name} = "A" \| "B";` |
| `ServiceFragmentGenerator` | controller with `[FrontendService]` | `export const {Name}Service = { ... }` + `apiFetch.tsx` |

## Type mapping

| C# | TypeScript |
|---|---|
| `string` | `string` |
| `bool` | `boolean` |
| `int`, `long`, `short`, `byte`, `double`, `float`, `decimal` | `number` |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Guid` | `string` |
| `object` | `unknown` |
| `T?` / `Nullable<T>` | optional property (`field?: T`) |
| `T[]` | `T[]` |
| `List<T>`, `IList<T>`, `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` | `T[]` |
| `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` | `Record<K, V>` |
| Custom class / interface | type name (no namespace) |
| Enum | string union (`"A" \| "B"`) |
| Generic type `Wrapper<T>` | `Wrapper<T>` |
| Type parameter `T` | `T` |

## Generation rules

- Property names are converted to **camelCase** (`MyProp` → `myProp`).
- All-uppercase acronyms are fully lowercased (`ID` → `id`, `URL` → `url`).
- Only **public instance** properties are emitted. Static, private, and indexer members are ignored.
- Nullable properties become **optional** (`?`).
- Classes with a non-`object` base emit an `extends` clause.
- Interfaces emit `extends` from their first declared interface.
- Generic type parameters are preserved (`PagedResult<T>`).
- Properties whose type is also marked `[FrontendModel]` generate `import type` statements automatically (imports are de-duplicated and sorted).
- Empty enums produce no output and are skipped silently.

## Output format

Each type produces one `public const string` field in the generated class. The field name is the type name (controllers get `Service` appended after stripping the `Controller` suffix):

```csharp
internal static class GeneratedTypeScriptContent
{
    public const string UserDto     = @"export interface UserDto { ... }";
    public const string RoleDto     = @"export interface RoleDto { ... }";
    public const string OrderStatus = @"export type OrderStatus = ""Pending"" | ""Processing"";";
    public const string UserService = @"export const UserService = { ... };";
    public const string apiFetch    = @"export async function apiFetch<T = void>(...) { ... }";
}
```

`apiFetch` is emitted once whenever at least one `[FrontendService]` exists. Duplicate type names are de-duplicated via a `HashSet<string>` — the first occurrence wins.

## Output path detection

The generator also scans the **syntax tree** for any invocation of `AddSourceCodeGen(string)` and embeds the first literal it finds:

```csharp
// This call is detected at compile time and its argument embedded in the assembly
builder.Services.AddSourceCodeGen("ClientApp/src/generated");
```

Result (second generated file `SourceCodeGenOutputPath.g.cs`):

```csharp
internal static class SourceCodeGenOutputPath
{
    public const string Value = "ClientApp/src/generated";
}
```

If no `AddSourceCodeGen()` call is found, the embedded value defaults to `"src/generated"`.

> **Important:** This detection reads literal string values from the C# syntax tree — it does **not** read MSBuild properties. Setting `<SourceCodeGenOutputPath>` in your `.csproj` has no effect on the Roslyn generator; it only affects the TsxWriter MSBuild target.

## Incremental pipeline

The generator uses `IIncrementalGenerator` with `ForAttributeWithMetadataName` — it only reruns the transform for types whose syntax or attributes actually changed. This keeps IDE rebuild times minimal.

## Project setup

- Target framework: `netstandard2.0` (required by the Roslyn compiler host — RS1041 prohibits targeting `net8.0`)
- Referenced in the consuming project as an `Analyzer` with `ReferenceOutputAssembly=false`
- The `IsExternalInit` polyfill in `System.Runtime.CompilerServices` enables C# 9 records on `netstandard2.0`
