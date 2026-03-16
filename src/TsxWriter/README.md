# TsxWriter

Console tool that extracts the TypeScript content from a compiled assembly and writes it to a `.tsx` file on disk.

## What it does

After `YourApplication` is built, its assembly contains the `GeneratedTypeScriptContent.Content` string constant produced by **CodeGenerator**. TsxWriter loads that assembly via reflection, reads the constant, and writes it to the specified output path.

It skips writing if the content is empty or contains no `export` statements.

## Arguments

```
TsxWriter <assemblyPath> <outputPath>
```

| Argument | Description                                 |
|----------|---------------------------------------------|
| `assemblyPath` | Path to the compiled `YourApplication.dll`  |
| `outputPath` | Full path of the `models.tsx` file to write |

## Output file

- Encoding: UTF-8 without BOM
- Line endings: normalized to `\n`
- Content: raw TypeScript from `GeneratedTypeScriptContent.Content`

## How it is invoked

TsxWriter is not meant to be run manually. It is called automatically by an MSBuild target in `YourApplication.csproj` after every build:

```xml
<Target Name="WriteTsModels" AfterTargets="Build">
  <Exec Command="dotnet run --no-build --project ../TsxWriter/TsxWriter.csproj
    -- YourApplication.dll Server/ClientApp/src/generated/models.tsx" />
</Target>
```

## Project setup

- Target framework: `net10.0`
- No external dependencies