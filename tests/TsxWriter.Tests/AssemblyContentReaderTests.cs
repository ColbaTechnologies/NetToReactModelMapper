using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TsWriter;
using Xunit;

namespace TsxWriter.Tests;

/// <summary>
/// Integration tests: compiles real assemblies in-memory, writes them to disk,
/// then verifies AssemblyContentReader can extract the embedded content.
/// </summary>
public class AssemblyContentReaderTests : IDisposable
{
    private readonly AssemblyContentReader _reader = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string CompileAndEmit(string source)
    {
        var tree        = CSharpSyntaxTree.ParseText(source);
        var refs        = Basic.Reference.Assemblies.Net80.References.All;
        var compilation = CSharpCompilation.Create(
            "TestAsm_" + Guid.NewGuid().ToString("N"),
            new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var path   = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dll");
        var result = compilation.Emit(path);
        if (!result.Success)
            throw new InvalidOperationException(
                string.Join("\n", result.Diagnostics));

        _tempFiles.Add(path);
        return path;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_AssemblyWithContentType_ReturnsFields()
    {
        var path = CompileAndEmit("""
            internal static class GeneratedTypeScriptContent
            {
                public const string User = "export interface User { id: number; }";
            }
            """);

        var (_, content) = _reader.Read(path, "src/generated");

        Assert.True(content.ContainsKey("User"));
        Assert.Contains("export interface User", content["User"]);
    }

    [Fact]
    public void Read_MissingContentType_Throws()
    {
        var path = CompileAndEmit("public class Dummy { }");
        Assert.Throws<InvalidOperationException>(() => _reader.Read(path, "src/generated"));
    }

    [Fact]
    public void Read_EmbeddedOutputPath_IsUsed()
    {
        var path = CompileAndEmit("""
            internal static class SourceCodeGenOutputPath
            {
                public const string Value = "frontend/models";
            }
            internal static class GeneratedTypeScriptContent
            {
                public const string X = "export type X = string;";
            }
            """);

        var (outputPath, _) = _reader.Read(path, "fallback/path");
        Assert.Equal("frontend/models", outputPath);
    }

    [Fact]
    public void Read_NoOutputPathType_UsesFallback()
    {
        var path = CompileAndEmit("""
            internal static class GeneratedTypeScriptContent
            {
                public const string Y = "export type Y = number;";
            }
            """);

        var (outputPath, _) = _reader.Read(path, "my/fallback");
        Assert.Equal("my/fallback", outputPath);
    }

    [Fact]
    public void Read_AssemblyLoadedOnce_BothOutputPathAndContentReturned()
    {
        var path = CompileAndEmit("""
            internal static class SourceCodeGenOutputPath { public const string Value = "out"; }
            internal static class GeneratedTypeScriptContent
            {
                public const string A = "export interface A { }";
                public const string B = "export type B = string;";
            }
            """);

        var (outputPath, content) = _reader.Read(path, "fallback");

        Assert.Equal("out", outputPath);
        Assert.Equal(2, content.Count);
        Assert.True(content.ContainsKey("A"));
        Assert.True(content.ContainsKey("B"));
    }
}
