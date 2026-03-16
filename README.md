# SourceCodeGen

> **Roslyn Source Generator** that reads your .NET models annotated with `[FrontendModel]` and automatically generates TypeScript/React (`.tsx`) files on every build.

---

## ✨ Features

- Zero-runtime overhead — all generation happens at **compile time**
- Annotate any C# class with `[FrontendModel]` and get a `.tsx` file
- Powered by `IIncrementalGenerator` for fast, incremental builds
- Includes `TsxWriter` CLI for post-processing generated files

---

## 📦 Projects

| Project | Purpose |
|---|---|
| `CodeGenerator` | Roslyn `IIncrementalGenerator` — the actual source generator |
| `SourceCodeGen.Runtime` | Runtime attributes and services consumed by the target project |
| `TsxWriter` | CLI tool that writes `.tsx` files from generator output |
| `NetToReactModelMapper` | NuGet packaging project — bundles all of the above |

---

## 🚀 Getting Started

### Requirements

- .NET SDK 10.0+ (for `TsxWriter`)
- Any project targeting `netstandard2.0` or later as the consumer

### Build

```bash
dotnet build SourceCodeGen.sln
```

### Pack (generate `.nupkg`)

```bash
dotnet pack src/NetToReactModelMapper/NetToReactModelMapper.csproj -c Release
```

---

## 🔧 Usage in a consumer project

1. Add a reference to the NuGet package (once published):

```xml
<PackageReference Include="SourceCodeGen" Version="1.0.0" />
```

2. Annotate your model:

```csharp
[FrontendModel]
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

3. Build your project — a `UserDtoModel.tsx` file will be generated automatically.

---

## 🏗️ Repository Structure

```
SourceCodeGen/
├── src/
│   ├── CodeGenerator/            # Roslyn IIncrementalGenerator
│   ├── SourceCodeGen.Runtime/    # Runtime attributes & services
│   ├── TsxWriter/                # .tsx file writer CLI
│   └── NetToReactModelMapper/    # NuGet packaging project
│       └── build/
│           ├── SourceCodeGen.props
│           └── SourceCodeGen.targets
├── .gitignore
├── LICENSE
└── SourceCodeGen.sln
```

---

