# Contributing

## Extension points — what to touch and where

### Add a new C# → TypeScript type mapping

File: `src/CodeGenerator/Mappers/TypeScriptTypeMapper.cs`

Add a case to the `Map` switch expression, above the generic fallbacks at the bottom. Order matters — more specific patterns must come before broader ones.

```csharp
// Example: map System.Uri to TypeScript string
{ Name: "Uri" } => "string",
```

Add a test in `tests/CodeGenerator.Tests/TypeScriptTypeMapperTests.cs`.

---

### Add a new HTTP verb

File: `src/CodeGenerator/Generators/ServiceFragmentGenerator.cs`

Add an entry to `VerbByAttributeName`:

```csharp
{ "HttpHeadAttribute", "HEAD" },
```

Add a test case to the `Generate_HttpVerb_IsEmittedInOptions` theory in
`tests/CodeGenerator.Tests/ServiceFragmentGeneratorTests.cs`.

---

### Skip a new ASP.NET Core framework-injected parameter type

File: `src/CodeGenerator/Generators/ServiceFragmentGenerator.cs`

Add the type name to `FrameworkParamTypes`:

```csharp
"ProblemDetails",
```

---

### Add support for a new .NET TFM

Three files to update:

1. **`src/SourceCodeGen.Runtime/SourceCodeGen.Runtime.csproj`**
   Add the new TFM to `<TargetFrameworks>`:
   ```xml
   <TargetFrameworks>netstandard2.0;net8.0;net10.0;net12.0</TargetFrameworks>
   ```

2. **`src/NetToReactModelMapper/NetToReactModelMapper.csproj`**
   Add the new `lib/<tfm>/` entry so NuGet delivers the right DLL to consumers:
   ```xml
   <None Include="..\SourceCodeGen.Runtime\bin\$(Configuration)\net12.0\SourceCodeGen.Runtime.dll"
         Pack="true" PackagePath="lib/net12.0/" Visible="false" />
   ```

3. **`tests/CodeGenerator.Tests/CodeGenerator.Tests.csproj`** and
   **`tests/TsxWriter.Tests/TsxWriter.Tests.csproj`**
   Add the new TFM to `<TargetFrameworks>` so CI validates the new runtime.

> The Roslyn generator itself (`CodeGenerator`) always targets `netstandard2.0` only —
> never change this (RS1041).

---

## Running locally

```bash
dotnet build          # build all projects
dotnet test           # run all tests (net8 + net10 locally; net9 in CI)
```

## Releasing a new version

```bash
git tag v1.2.3
git push origin v1.2.3
```

The GitHub Actions workflow (`.github/workflows/publish.yml`) will:
1. Run all tests
2. Pack the NuGet
3. Publish to NuGet.org

Semantic versioning rules for this package:

| Change | Version bump |
|---|---|
| Bug fix in generated TypeScript output | patch |
| New type mapping, new attribute option, new HTTP verb | minor |
| Rename of attribute, breaking change in generated output | major |

Once v1.0.0 is published, uncomment `<PackageValidationBaselineVersion>` in
`src/NetToReactModelMapper/NetToReactModelMapper.csproj` to automatically
catch accidental breaking changes during `dotnet pack`.
