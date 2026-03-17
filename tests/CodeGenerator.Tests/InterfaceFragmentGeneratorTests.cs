using CodeGenerator.Generators;
using CodeGenerator.Mappers;
using CodeGenerator.Tests.Helpers;
using Xunit;

namespace CodeGenerator.Tests;

public class InterfaceFragmentGeneratorTests
{
    private readonly InterfaceFragmentGenerator _gen = new(new TypeScriptTypeMapper());

    [Fact]
    public void CanHandle_Class_ReturnsTrue()
    {
        var type = CompilationHelper.GetType("public class Foo { }", "Foo");
        Assert.True(_gen.CanHandle(type));
    }

    [Fact]
    public void CanHandle_Enum_ReturnsFalse()
    {
        var type = CompilationHelper.GetType("public enum E { A }", "E");
        Assert.False(_gen.CanHandle(type));
    }

    [Fact]
    public void Generate_EmptyClass_ProducesEmptyInterface()
    {
        var type   = CompilationHelper.GetType("public class Dto { }", "Dto");
        var result = _gen.Generate(type, default);
        Assert.NotNull(result);
        Assert.Contains("export interface Dto {", result);
        Assert.Contains("}", result);
    }

    [Fact]
    public void Generate_PrimitiveProperties_MapsCorrectly()
    {
        const string src = """
            public class UserDto
            {
                public int    Id   { get; set; }
                public string Name { get; set; } = "";
                public bool   Active { get; set; }
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "UserDto"), default)!;

        Assert.Contains("id: number;",   result);
        Assert.Contains("name: string;", result);
        Assert.Contains("active: boolean;", result);
    }

    [Fact]
    public void Generate_NullableProperty_EmitsOptionalMarker()
    {
        const string src = """
            #nullable enable
            public class Dto { public string? Name { get; set; } }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "Dto"), default)!;
        Assert.Contains("name?: string;", result);
    }

    [Fact]
    public void Generate_StaticAndPrivateProperties_AreOmitted()
    {
        const string src = """
            public class Dto
            {
                public static int Counter { get; set; }
                private string Secret { get; set; } = "";
                public int Visible { get; set; }
            }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "Dto"), default)!;
        Assert.DoesNotContain("counter", result);
        Assert.DoesNotContain("secret",  result);
        Assert.Contains("visible: number;", result);
    }

    [Fact]
    public void Generate_ListProperty_ProducesArrayType()
    {
        const string src = """
            using System.Collections.Generic;
            public class Dto { public List<int> Ids { get; set; } = new(); }
            """;
        var result = _gen.Generate(CompilationHelper.GetType(src, "Dto"), default)!;
        Assert.Contains("ids: number[];", result);
    }

    [Fact]
    public void Generate_BaseClass_EmitsExtendsClause()
    {
        const string src = """
            public class Base { }
            public class Child : Base { public int X { get; set; } }
            """;
        var type   = CompilationHelper.GetType(src, "Child");
        var result = _gen.Generate(type, default)!;
        Assert.Contains("extends Base", result);
    }

    [Fact]
    public void Generate_FrontendModelReference_EmitsImport()
    {
        const string src = """
            using System;
            [AttributeUsage(AttributeTargets.Class)] public sealed class FrontendModelAttribute : Attribute {}
            [FrontendModel] public class AddressDto { public string Street { get; set; } = ""; }
            public class UserDto { public AddressDto Address { get; set; } = new(); }
            """;
        var type   = CompilationHelper.GetType(src, "UserDto");
        var result = _gen.Generate(type, default)!;
        Assert.Contains("import type { AddressDto } from './AddressDto';", result);
    }

    [Fact]
    public void Generate_GenericClass_PreservesTypeParameter()
    {
        const string src = "public class PagedResult<T> { public T[] Items { get; set; } = []; public int Total { get; set; } }";
        var result = _gen.Generate(CompilationHelper.GetType(src, "PagedResult`1"), default)!;
        Assert.Contains("export interface PagedResult<T> {", result);
        Assert.Contains("items: T[];", result);
    }

    [Fact]
    public void Generate_Interface_EmitsExtendsFromFirstInterface()
    {
        const string src = """
            public interface IBase { }
            public interface IDerived : IBase { int X { get; } }
            """;
        var type   = CompilationHelper.GetType(src, "IDerived");
        var result = _gen.Generate(type, default)!;
        Assert.Contains("extends IBase", result);
    }
}
