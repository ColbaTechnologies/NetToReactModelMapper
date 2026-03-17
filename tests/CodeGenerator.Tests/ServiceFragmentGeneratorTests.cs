using CodeGenerator.Generators;
using CodeGenerator.Mappers;
using CodeGenerator.Tests.Helpers;
using Xunit;

namespace CodeGenerator.Tests;

/// <summary>
/// All tests compile inline C# with stub ASP.NET Core attributes so the test project
/// does not need an ASP.NET Core reference — ServiceFragmentGenerator detects attributes
/// by name only.
/// </summary>
public class ServiceFragmentGeneratorTests
{
    private readonly ServiceFragmentGenerator _gen = new(new TypeScriptTypeMapper());

    // ── Attribute stubs included in every test source ─────────────────────────
    private const string Stubs = """
        using System;
        [AttributeUsage(AttributeTargets.Class)]                      sealed class FrontendServiceAttribute : Attribute {}
        [AttributeUsage(AttributeTargets.Class)]                      sealed class RouteAttribute(string t) : Attribute {}
        [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)] sealed class HttpGetAttribute    : Attribute { public HttpGetAttribute() {} public HttpGetAttribute(string t) {} }
        [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)] sealed class HttpPostAttribute   : Attribute { public HttpPostAttribute() {} public HttpPostAttribute(string t) {} }
        [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)] sealed class HttpPutAttribute    : Attribute { public HttpPutAttribute() {} public HttpPutAttribute(string t) {} }
        [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)] sealed class HttpDeleteAttribute : Attribute { public HttpDeleteAttribute() {} public HttpDeleteAttribute(string t) {} }
        [AttributeUsage(AttributeTargets.Parameter)]                  sealed class FromBodyAttribute  : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)]                  sealed class FromQueryAttribute : Attribute {}
        [AttributeUsage(AttributeTargets.Class)]                      sealed class FrontendModelAttribute : Attribute {}
        """;

    // ── Controller with no action methods → null ──────────────────────────────

    [Fact]
    public void Generate_NoActions_ReturnsNull()
    {
        var src  = Stubs + "[FrontendService][Route(\"api/[controller]\")] public class EmptyController { }";
        var type = CompilationHelper.GetType(src, "EmptyController");
        Assert.Null(_gen.Generate(type, default));
    }

    // ── Service name derivation ───────────────────────────────────────────────

    [Fact]
    public void Generate_ControllerSuffix_IsStripped()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class UserController
            {
                [HttpGet] public string[] GetAll() => [];
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "UserController"), default)!;
        Assert.Contains("export const UserService =", result);
    }

    // ── Route building ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ControllerToken_IsReplacedWithLowercaseName()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class OrderController { [HttpGet] public string[] GetAll() => []; }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "OrderController"), default)!;
        Assert.Contains("'/api/order'", result);
    }

    [Fact]
    public void Generate_RouteParam_IsEmbeddedInTemplateLiteral()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class ItemController { [HttpGet("{id}")] public string GetById(int id) => ""; }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "ItemController"), default)!;
        Assert.Contains("`/api/item/${id}`", result);
    }

    [Fact]
    public void Generate_MethodRoute_IsCombinedWithClassRoute()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class ProductController { [HttpGet("featured")] public string[] GetFeatured() => []; }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "ProductController"), default)!;
        Assert.Contains("'/api/product/featured'", result);
    }

    // ── HTTP verbs ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HttpGet",    "GET")]
    [InlineData("HttpPost",   "POST")]
    [InlineData("HttpPut",    "PUT")]
    [InlineData("HttpDelete", "DELETE")]
    public void Generate_HttpVerb_IsEmittedInOptions(string attribute, string verb)
    {
        var src = Stubs + $"[FrontendService][Route(\"api/test\")] public class TestController {{ [{attribute}] public void Do() {{}} }}";
        var result = _gen.Generate(CompilationHelper.GetType(src, "TestController"), default)!;
        Assert.Contains($"method: '{verb}'", result);
    }

    // ── Parameter mapping ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_FromBodyParam_IsEmittedAsBodyOption()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class UserController
            {
                [HttpPost] public void Create([FromBody] string name) {}
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "UserController"), default)!;
        Assert.Contains("body: name", result);
    }

    [Fact]
    public void Generate_FromQueryParam_IsEmittedInParamsOption()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class SearchController
            {
                [HttpGet] public string[] Search([FromQuery] string query) => [];
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "SearchController"), default)!;
        Assert.Contains("params: { query }", result);
    }

    [Fact]
    public void Generate_CancellationToken_IsSkipped()
    {
        var src = Stubs + """
            using System.Threading;
            [FrontendService][Route("api/[controller]")]
            public class DataController
            {
                [HttpGet] public string[] GetAll(CancellationToken ct) => [];
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "DataController"), default)!;
        // CancellationToken must not appear in the function signature
        Assert.DoesNotContain("ct", result.Split("=>")[0]);
    }

    // ── Return types ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_VoidReturn_EmitsPromiseVoid()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class Ctrl { [HttpDelete("{id}")] public void Delete(int id) {} }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "Ctrl"), default)!;
        Assert.Contains("Promise<void>", result);
    }

    [Fact]
    public void Generate_StringArrayReturn_EmitsMappedType()
    {
        var src = Stubs + """
            [FrontendService][Route("api/[controller]")]
            public class Ctrl { [HttpGet] public string[] GetAll() => []; }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "Ctrl"), default)!;
        Assert.Contains("Promise<string[]>", result);
    }

    // ── apiFetch import ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_AlwaysImportsApiFetch()
    {
        var src = Stubs + """
            [FrontendService][Route("api/test")]
            public class TestController { [HttpGet] public void Ping() {} }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "TestController"), default)!;
        Assert.Contains("import { apiFetch } from '../apiFetch';", result);
    }

    // ── FrontendModel imports ─────────────────────────────────────────────────

    [Fact]
    public void Generate_FrontendModelReturnType_EmitsImport()
    {
        var src = Stubs + """
            [FrontendModel] public class UserDto { public int Id { get; set; } }
            [FrontendService][Route("api/[controller]")]
            public class UserController { [HttpGet] public UserDto[] GetAll() => []; }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "UserController"), default)!;
        Assert.Contains("import type { UserDto } from './UserDto';", result);
    }
}
