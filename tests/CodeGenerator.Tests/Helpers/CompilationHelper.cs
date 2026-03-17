using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeGenerator.Tests.Helpers;

/// <summary>
/// Creates in-memory Roslyn compilations so tests can obtain real INamedTypeSymbol instances
/// without depending on installed .NET SDKs.
/// </summary>
internal static class CompilationHelper
{
    // ImmutableArray<PortableExecutableReference> is IEnumerable<MetadataReference>
    private static readonly System.Collections.Immutable.ImmutableArray<MetadataReference> References =
        Basic.Reference.Assemblies.Net80.References.All
            .CastArray<MetadataReference>();

    /// <summary>Compiles <paramref name="source"/> and returns the named type.</summary>
    public static INamedTypeSymbol GetType(string source, string metadataName)
    {
        var compilation = CreateCompilation(source);
        return compilation.GetTypeByMetadataName(metadataName)
               ?? throw new InvalidOperationException(
                   $"Type '{metadataName}' not found in:\n{source}");
    }

    /// <summary>Returns a compilation with all .NET 8 BCL references available.</summary>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Compiles <paramref name="source"/> and emits a real DLL to a temp file.
    /// Returns the path — caller is responsible for cleanup.</summary>
    public static string EmitToDisk(string source)
    {
        var compilation = CreateCompilation(source);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dll");
        var result = compilation.Emit(path);
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics);
            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }
        return path;
    }
}
