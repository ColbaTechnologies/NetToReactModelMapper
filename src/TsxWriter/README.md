# TsxWriter

CLI tool that extracts the TypeScript content embedded by **CodeGenerator** from a compiled assembly and writes each fragment to its own `.tsx` file on disk.

## What it does

After the consuming application is built, its assembly contains a `GeneratedTypeScriptContent` class with one `public const string` field per annotated type. TsxWriter loads that assembly via reflection, reads every field, and writes each one to `{FieldName}.tsx` in the output directory.

It also reads the embedded `SourceCodeGenOutputPath.Value` constant (if present) to know where to write without needing it passed on the command line.

## Usage

```
dotnet TsxWriter.dll <assemblyPath> [outputDirectory]
```

| Argument | Required | Description |
|---|---|---|
| `assemblyPath` | Yes | Path to the compiled `YourApp.dll` |
| `outputDirectory` | No | Directory to write `.tsx` files into. See path resolution below. |

### Path resolution order

1. `outputDirectory` CLI argument (if provided)
2. `SourceCodeGenOutputPath.Value` constant embedded in the assembly at compile time (set automatically when `AddSourceCodeGen("path")` is called in the project)
3. `src/generated` (hard fallback)

## Output

- One `.tsx` file per model / service / utility (e.g. `UserDto.tsx`, `UserService.tsx`, `apiFetch.tsx`)
- Encoding: UTF-8 without BOM
- Files with no `export` statement are skipped silently
- The output directory is created automatically if it does not exist
- Exit code `0` on success, `1` if `assemblyPath` is not provided

## MSBuild integration — custom target

```xml
<Target Name="WriteTsFiles" AfterTargets="Build">
  <Exec Command="dotnet $(TsxWriterPath) $(OutputPath)$(AssemblyName).dll ClientApp/src/generated" />
</Target>
```

## MSBuild integration — via NuGet package (no custom target needed)

When the package is installed via NuGet, `SourceCodeGen.targets` is auto-imported. Add these two properties to your `.csproj` to enable automatic post-build writes:

```xml
<PropertyGroup>
  <SourceCodeGenRunTsxWriter>true</SourceCodeGenRunTsxWriter>
  <SourceCodeGenOutputPath>ClientApp/src/generated</SourceCodeGenOutputPath>
</PropertyGroup>
```

`<SourceCodeGenOutputPath>` defaults to `src/generated` if not set. This property is passed directly to TsxWriter as the `outputDirectory` argument — it is **not** read by the Roslyn generator.

## How it works internally

The assembly is loaded exactly once via `Assembly.LoadFrom`. Two types are inspected:

| Type in assembly | Purpose |
|---|---|
| `SourceCodeGenOutputPath` | `Value` field — compile-time output path (optional) |
| `GeneratedTypeScriptContent` | One field per TypeScript file |

`AssemblyContentReader.Read()` returns a tuple `(outputPath, content)`. `TypeScriptFileWriter.Write()` iterates the dictionary and writes each entry that contains an `export` statement.

## Project setup

- Target framework: `net8.0`
- No external runtime dependencies
