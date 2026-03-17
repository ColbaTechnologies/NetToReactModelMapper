using CodeGenerator.Generators;
using CodeGenerator.Tests.Helpers;
using Xunit;

namespace CodeGenerator.Tests;

public class EnumFragmentGeneratorTests
{
    private readonly EnumFragmentGenerator _gen = new();

    [Fact]
    public void CanHandle_Enum_ReturnsTrue()
    {
        var type = CompilationHelper.GetType("public enum Status { Active, Inactive }", "Status");
        Assert.True(_gen.CanHandle(type));
    }

    [Fact]
    public void CanHandle_Class_ReturnsFalse()
    {
        var type = CompilationHelper.GetType("public class Foo { }", "Foo");
        Assert.False(_gen.CanHandle(type));
    }

    [Fact]
    public void Generate_EmptyEnum_ReturnsNull()
    {
        var type = CompilationHelper.GetType("public enum Empty { }", "Empty");
        Assert.Null(_gen.Generate(type, default));
    }

    [Fact]
    public void Generate_SingleMember_ProducesUnionType()
    {
        var type   = CompilationHelper.GetType("public enum Color { Red }", "Color");
        var result = _gen.Generate(type, default);
        Assert.NotNull(result);
        Assert.Contains("export type Color =", result);
        Assert.Contains("\"Red\"", result);
    }

    [Fact]
    public void Generate_MultipleMembers_ProducesPipeSeparated()
    {
        var type   = CompilationHelper.GetType("public enum Status { Active, Inactive, Pending }", "Status");
        var result = _gen.Generate(type, default);
        Assert.NotNull(result);
        Assert.Equal(
            "export type Status = \"Active\" | \"Inactive\" | \"Pending\";" + Environment.NewLine + Environment.NewLine,
            result);
    }

    [Fact]
    public void Generate_EndsWithSemicolonAndBlankLine()
    {
        var type   = CompilationHelper.GetType("public enum Foo { A, B }", "Foo");
        var result = _gen.Generate(type, default)!;
        Assert.EndsWith(";" + Environment.NewLine + Environment.NewLine, result);
    }
}
