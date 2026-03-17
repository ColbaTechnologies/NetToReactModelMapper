using CodeGenerator;
using CodeGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CodeGenerator.Tests;

/// <summary>
/// End-to-end tests that run the full Roslyn GeneratorDriver pipeline.
/// These verify the wiring in TypeScriptGenerator.Initialize() — things
/// the unit tests cannot catch: pipeline Combine(), de-duplication,
/// output path detection, and the apiFetch emission rule.
/// </summary>
public class TypeScriptGeneratorEndToEndTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new TypeScriptGenerator());
        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>Returns the text of the GeneratedTypeScriptContent source file.</summary>
    private static string GetContentClass(GeneratorDriverRunResult result)
    {
        var text = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(t => t.Contains("GeneratedTypeScriptContent"));

        Assert.NotNull(text);
        return text!;
    }

    // ── [FrontendModel] ───────────────────────────────────────────────────────

    [Fact]
    public void FrontendModel_Class_EmitsInterfaceField()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel]
            public class UserDto
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        var content = GetContentClass(result);
        Assert.Contains("public const string UserDto", content);
        Assert.Contains("export interface UserDto", content);
        Assert.Contains("id: number;", content);
        Assert.Contains("name: string;", content);
    }

    [Fact]
    public void FrontendModel_Enum_EmitsUnionTypeField()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel]
            public enum Status { Active, Inactive }
            """);

        var content = GetContentClass(result);
        Assert.Contains("public const string Status", content);
        Assert.Contains("export type Status", content);
        Assert.Contains("\"Active\"", content);
        Assert.Contains("\"Inactive\"", content);
    }

    [Fact]
    public void FrontendModel_DuplicateType_EmittedOnce()
    {
        // Two sources produce the same type name — only the first should appear.
        var source = """
            using SourceCodeGen;
            [FrontendModel] public class Foo { public int X { get; set; } }
            [FrontendModel] public class Bar { public int Y { get; set; } }
            """;

        var content = GetContentClass(RunGenerator(source));

        var fooCount = CountOccurrences(content, "const string Foo");
        var barCount = CountOccurrences(content, "const string Bar");
        Assert.Equal(1, fooCount);
        Assert.Equal(1, barCount);
    }

    // ── [FrontendService] ─────────────────────────────────────────────────────

    [Fact]
    public void FrontendService_Controller_EmitsServiceAndApiFetch()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendService]
            [System.Web.Http.RouteAttribute("api/[controller]")]
            public class UserController
            {
                [HttpGetAttribute]
                public System.Collections.Generic.IEnumerable<string> GetAll() => null!;
            }
            public class HttpGetAttribute : System.Attribute { }
            namespace System.Web.Http { public class RouteAttribute : System.Attribute { public RouteAttribute(string t) { } } }
            """);

        var content = GetContentClass(result);
        Assert.Contains("public const string UserService", content);
        Assert.Contains("public const string apiFetch", content);
        Assert.Contains("export const UserService", content);
    }

    [Fact]
    public void FrontendService_WithoutAnyService_DoesNotEmitApiFetch()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel]
            public class Dto { public int Id { get; set; } }
            """);

        var content = GetContentClass(result);
        Assert.DoesNotContain("apiFetch", content);
    }

    // ── Output path detection ─────────────────────────────────────────────────

    [Fact]
    public void OutputPath_AddSourceCodeGenLiteral_IsEmbedded()
    {
        // The generator detects MemberAccess calls: something.AddSourceCodeGen("path")
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel]
            public class Dto { public int Id { get; set; } }

            public static class Startup
            {
                public static void Configure(object services)
                {
                    services.AddSourceCodeGen("ClientApp/src/generated");
                }
            }
            public static class ServiceExtensions
            {
                public static object AddSourceCodeGen(this object s, string path) => s;
            }
            """);

        var pathSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(t => t.Contains("SourceCodeGenOutputPath"));

        Assert.NotNull(pathSource);
        Assert.Contains("ClientApp/src/generated", pathSource);
    }

    [Fact]
    public void OutputPath_NoCall_DefaultsToSrcGenerated()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel]
            public class Dto { public int Id { get; set; } }
            """);

        var pathSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(t => t.Contains("SourceCodeGenOutputPath"));

        Assert.NotNull(pathSource);
        Assert.Contains("src/generated", pathSource);
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────

    [Fact]
    public void Generator_ProducesNoDiagnosticErrors()
    {
        var result = RunGenerator("""
            using SourceCodeGen;
            [FrontendModel] public class Dto { public int Id { get; set; } }
            """);

        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
