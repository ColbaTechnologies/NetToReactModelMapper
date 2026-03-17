# SourceCodeGen

[![NuGet](https://img.shields.io/nuget/v/SourceCodeGen.svg)](https://www.nuget.org/packages/SourceCodeGen)
[![CI](https://github.com/Colba/NetToReactModelMapper/actions/workflows/ci.yml/badge.svg)](https://github.com/Colba/NetToReactModelMapper/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Roslyn Source Generator that converts annotated .NET types and ASP.NET Core controllers into TypeScript files automatically on every build — zero runtime overhead.

---

## What it does

SourceCodeGen provides two attributes:

| Attribute | Applies to | Output file |
|---|---|---|
| `[FrontendModel]` | `class`, `interface`, `enum` | `{TypeName}.tsx` — TypeScript `interface` or union `type` |
| `[FrontendService]` | ASP.NET Core controller | `{Name}Service.tsx` — typed service object using `apiFetch` |

All generation happens **at compile time** via `IIncrementalGenerator`. The TypeScript content is embedded as string constants inside the compiled assembly and written to disk either at startup (runtime service) or post-build (TsxWriter CLI).

---

## Projects

| Project | Role |
|---|---|
| `CodeGenerator` | Roslyn `IIncrementalGenerator` — all compile-time logic |
| `SourceCodeGen.Runtime` | `IHostedService` that writes `.tsx` files at app startup |
| `TsxWriter` | CLI tool that writes `.tsx` files from a built assembly |
| `NetToReactModelMapper` | NuGet packaging — bundles all of the above |

---

## TFM compatibility

| Consumer TFM | Generator | Runtime service DLL |
|---|---|---|
| `netstandard2.0` | Works | `lib/netstandard2.0/` |
| `net8.0` | Works | `lib/net8.0/` |
| `net9.0` | Works | `lib/net8.0/` (closest match) |
| `net10.0` | Works | `lib/net10.0/` |

The generator itself targets `netstandard2.0` (Roslyn host requirement) and works regardless of the consumer's TFM.

---

## Installation

```xml
<PackageReference Include="SourceCodeGen" Version="1.0.0" />
```

---

## [FrontendModel]

### Classes and interfaces → TypeScript interface

```csharp
[FrontendModel]
public class UserDto
{
    public int     Id    { get; set; }
    public string  Name  { get; set; } = "";
    public string? Email { get; set; }   // nullable → optional
    public RoleDto Role  { get; set; } = new();
}

[FrontendModel]
public class RoleDto
{
    public string Name { get; set; } = "";
}
```

Generated `UserDto.tsx`:
```typescript
import type { RoleDto } from './RoleDto';

export interface UserDto {
  id: number;
  name: string;
  email?: string;
  role: RoleDto;
}
```

Generated `RoleDto.tsx`:
```typescript
export interface RoleDto {
  name: string;
}
```

### Enums → TypeScript union type

```csharp
[FrontendModel]
public enum OrderStatus { Pending, Processing, Shipped, Delivered }
```

Generated `OrderStatus.tsx`:
```typescript
export type OrderStatus = "Pending" | "Processing" | "Shipped" | "Delivered";
```

### Type mapping

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

### Generation rules

- Property names are converted to **camelCase** (`MyProp` → `myProp`).
- All-uppercase names are fully lowercased (`ID` → `id`, `URL` → `url`).
- Only **public instance** properties are emitted. Static, private, and indexer members are ignored.
- Nullable properties become **optional** (`?`).
- Classes with a non-`object` base emit an `extends` clause.
- Interfaces emit `extends` from their first declared interface.
- Generic type parameters are preserved (`PagedResult<T>`).
- Properties whose type is also marked `[FrontendModel]` generate `import type` statements automatically.
- Empty enums produce no output and are skipped silently.

---

## [FrontendService]

Annotate an ASP.NET Core controller to generate a typed service object. No ASP.NET Core reference is required in the consuming project — attributes are detected by name.

```csharp
[FrontendService]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<UserDto>> GetAll() { ... }

    [HttpGet("{id}")]
    public ActionResult<UserDto> GetById(int id) { ... }

    [HttpPost]
    public ActionResult<UserDto> Create([FromBody] CreateUserRequest request) { ... }

    [HttpPut("{id}")]
    public ActionResult<UserDto> Update(int id, [FromBody] UpdateUserRequest request) { ... }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id) { ... }
}
```

Generated `UserService.tsx`:
```typescript
import { apiFetch } from '../apiFetch';
import type { UserDto } from './UserDto';
import type { CreateUserRequest } from './CreateUserRequest';
import type { UpdateUserRequest } from './UpdateUserRequest';

export const UserService = {
  getAll: (): Promise<UserDto[]> =>
    apiFetch('/api/user', { method: 'GET' }),

  getById: (id: number): Promise<UserDto> =>
    apiFetch(`/api/user/${id}`, { method: 'GET' }),

  create: (request: CreateUserRequest): Promise<UserDto> =>
    apiFetch('/api/user', { method: 'POST', body: request }),

  update: (id: number, request: UpdateUserRequest): Promise<UserDto> =>
    apiFetch(`/api/user/${id}`, { method: 'PUT', body: request }),

  delete: (id: number): Promise<void> =>
    apiFetch(`/api/user/${id}`, { method: 'DELETE' }),
};
```

### How routes are built

The full route is assembled from the class `[Route]` plus the method `[HttpXxx]` template:

| C# | TypeScript |
|---|---|
| `[Route("api/[controller]")]` on `UserController` | `/api/user` prefix |
| `[HttpGet("{id}")]` | `\`/api/user/${id}\`` (template literal) |
| `[HttpGet("featured")]` | `'/api/user/featured'` (regular string) |

`[controller]` is replaced with the controller class name without the `Controller` suffix, lowercased.

### Parameter mapping

| C# parameter | Mapped to |
|---|---|
| Name matches `{param}` in route template | Embedded in the URL template literal |
| `[FromBody]` attribute | `body:` option in `apiFetch` |
| `[FromQuery]` attribute | `params:` option in `apiFetch` |
| Complex type, no attribute (POST/PUT/PATCH) | Inferred as body |
| Simple type, no attribute, not in route | Inferred as query param |
| `CancellationToken`, `HttpContext`, `HttpRequest`, etc. | Skipped entirely |

### Return type unwrapping

| C# return | TypeScript `Promise<T>` |
|---|---|
| `Task<ActionResult<T>>` | `T` |
| `ActionResult<T>` | `T` |
| `Task<T>` | `T` |
| `IActionResult`, `ActionResult`, `void`, `Task` | `void` |

### apiFetch utility

When at least one `[FrontendService]` exists, an `apiFetch.tsx` file is generated alongside the service files:

```typescript
interface FetchOptions extends Omit<RequestInit, 'body'> {
  body?: unknown;
  params?: Record<string, string | number | boolean | null | undefined>;
}

export async function apiFetch<T = void>(url: string, options?: FetchOptions): Promise<T>
```

- Serializes `body` automatically via `JSON.stringify`.
- Builds query strings from `params`, filtering out `null` / `undefined`.
- Throws on non-2xx responses with `HTTP {status}: {statusText}`.
- Returns `undefined` as `T` on 204 No Content.

Place `apiFetch.tsx` one level above the generated models folder (e.g. `src/` if models live in `src/generated/`).

---

## Writing files to disk

Choose one of three integration paths:

### Option A — Runtime service (ASP.NET Core)

Registers an `IHostedService` that writes files at application startup.

```csharp
// Program.cs
builder.Services.AddSourceCodeGen("ClientApp/src/generated");
```

By default it **skips Production** to avoid unintended disk writes. To enable it everywhere:

```csharp
builder.Services.AddSourceCodeGen("ClientApp/src/generated", runInProduction: true);
```

### Option B — TsxWriter CLI (post-build)

Writes files by loading the compiled assembly via reflection. Does not require a running application.

```bash
dotnet TsxWriter.dll <path/to/YourApp.dll> [outputDirectory]
```

Path resolution order:
1. `outputDirectory` argument (if provided)
2. Path embedded in the assembly at compile time (from `AddSourceCodeGen("path")`)
3. `src/generated` (hard fallback)

Custom MSBuild target example:
```xml
<Target Name="WriteTsFiles" AfterTargets="Build">
  <Exec Command="dotnet $(TsxWriterPath) $(OutputPath)$(AssemblyName).dll ClientApp/src/generated" />
</Target>
```

### Option C — TsxWriter via NuGet MSBuild targets (post-build, no custom targets needed)

When installed as a NuGet package, the `SourceCodeGen.targets` file is auto-imported. Enable it with two properties in your `.csproj`:

```xml
<PropertyGroup>
  <SourceCodeGenRunTsxWriter>true</SourceCodeGenRunTsxWriter>
  <SourceCodeGenOutputPath>ClientApp/src/generated</SourceCodeGenOutputPath>
</PropertyGroup>
```

TsxWriter runs automatically after every build. No `<Target>` block required.

> **Note:** `<SourceCodeGenOutputPath>` is only read by the TsxWriter MSBuild target. The Roslyn generator embeds the path by scanning for a `AddSourceCodeGen("literal")` call in your source — it does not read MSBuild properties.

---

## Output path auto-detection

The Roslyn generator scans your source code at compile time for a literal string argument to `AddSourceCodeGen()` and embeds it in the assembly:

```csharp
// Program.cs — the "ClientApp/src/generated" literal is detected and embedded
builder.Services.AddSourceCodeGen("ClientApp/src/generated");
```

This produces in the compiled assembly:
```csharp
internal static class SourceCodeGenOutputPath
{
    public const string Value = "ClientApp/src/generated";
}
```

Both the runtime service (Option A) and TsxWriter (Options B/C) read this constant automatically, so you only specify the path once.

If no `AddSourceCodeGen()` call exists (e.g. you only use TsxWriter), the embedded path defaults to `src/generated`. In that case, pass the path explicitly via the CLI argument or `<SourceCodeGenOutputPath>`.

---

## How it works internally

```
[FrontendModel] / [FrontendService] types
         │ (compile time)
         ▼
  TypeScriptGenerator (IIncrementalGenerator)
         │
         ├─ [FrontendModel] class/interface ──▶ InterfaceFragmentGenerator
         ├─ [FrontendModel] enum            ──▶ EnumFragmentGenerator
         └─ [FrontendService] controller    ──▶ ServiceFragmentGenerator
                                                + apiFetch utility
         │
         ▼
  GeneratedTypeScriptContent  (embedded in compiled assembly)
  ┌──────────────────────────────────────────────────────────┐
  │ public const string UserDto     = @"export interface...";│
  │ public const string UserService = @"export const ...";   │
  │ public const string apiFetch    = @"export async ...";   │
  └──────────────────────────────────────────────────────────┘
         │
         ├── Option A: SourceCodeGenHostedService  (reflection at startup)
         ├── Option B: TsxWriter CLI               (reflection post-build)
         └── Option C: TsxWriter via MSBuild       (reflection post-build, auto)
                  │
                  ▼
         ClientApp/src/generated/
           UserDto.tsx
           UserService.tsx
           apiFetch.tsx
```

---

## Build, test, and pack

```bash
# Build full solution
dotnet build

# Run all 83 tests
dotnet test

# Pack the NuGet package (build first, then pack)
dotnet build -c Release
dotnet pack src/NetToReactModelMapper/NetToReactModelMapper.csproj -c Release --no-build
```

---

## Publishing to NuGet

A GitHub Actions workflow (`.github/workflows/publish.yml`) handles publishing automatically.

**Trigger:** push a version tag — the workflow runs tests, packs, and publishes:

```bash
git tag v1.0.0
git push origin v1.0.0
```

**Required secret:** add `NUGET_API_KEY` in your repository's *Settings → Secrets and variables → Actions* with an API key from [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys).

You can also trigger it manually from the *Actions* tab in GitHub with a custom version number.

---

## Repository structure

```
NetToReactModelMapper/
├── src/
│   ├── CodeGenerator/            # Roslyn IIncrementalGenerator
│   │   ├── Generators/           # IFragmentGenerator, Enum, Interface, Service
│   │   ├── Mappers/              # C# → TypeScript type mapping
│   │   └── Helpers/              # NamingHelper (camelCase)
│   ├── SourceCodeGen.Runtime/    # IHostedService + ServiceCollectionExtensions
│   ├── TsxWriter/                # CLI tool
│   └── NetToReactModelMapper/    # NuGet packaging
│       └── build/                # SourceCodeGen.props / .targets
├── tests/
│   ├── CodeGenerator.Tests/      # Unit tests (Roslyn symbol-based)
│   └── TsxWriter.Tests/          # Integration tests (real disk I/O)
├── LICENSE
└── README.md
```
