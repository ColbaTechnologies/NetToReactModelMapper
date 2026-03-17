using CodeGenerator.Mappers;
using CodeGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeGenerator.Tests;

public class TypeScriptTypeMapperTests
{
    private readonly TypeScriptTypeMapper _mapper = new();
    private readonly Microsoft.CodeAnalysis.CSharp.CSharpCompilation _compilation =
        CompilationHelper.CreateCompilation("class _Dummy {}");

    // ── Primitive mappings ────────────────────────────────────────────────────

    [Theory]
    [InlineData(SpecialType.System_String,  "string")]
    [InlineData(SpecialType.System_Boolean, "boolean")]
    [InlineData(SpecialType.System_Int32,   "number")]
    [InlineData(SpecialType.System_Int64,   "number")]
    [InlineData(SpecialType.System_Int16,   "number")]
    [InlineData(SpecialType.System_Byte,    "number")]
    [InlineData(SpecialType.System_Double,  "number")]
    [InlineData(SpecialType.System_Single,  "number")]
    [InlineData(SpecialType.System_Decimal, "number")]
    [InlineData(SpecialType.System_Object,  "unknown")]
    [InlineData(SpecialType.System_DateTime,"string")]
    public void Map_Primitives(SpecialType specialType, string expected)
    {
        var type = _compilation.GetSpecialType(specialType);
        Assert.Equal(expected, _mapper.Map(type));
    }

    [Theory]
    [InlineData("System.Guid",           "string")]
    [InlineData("System.DateTimeOffset", "string")]
    [InlineData("System.DateOnly",       "string")]
    [InlineData("System.TimeOnly",       "string")]
    public void Map_DateAndIdTypes(string typeName, string expected)
    {
        var type = _compilation.GetTypeByMetadataName(typeName)!;
        Assert.Equal(expected, _mapper.Map(type));
    }

    // ── Collection mappings ───────────────────────────────────────────────────

    [Theory]
    [InlineData("System.Collections.Generic.List`1",            "number[]")]
    [InlineData("System.Collections.Generic.IList`1",           "number[]")]
    [InlineData("System.Collections.Generic.IEnumerable`1",     "number[]")]
    [InlineData("System.Collections.Generic.ICollection`1",     "number[]")]
    [InlineData("System.Collections.Generic.IReadOnlyList`1",   "number[]")]
    public void Map_Collections_ProduceArray(string genericTypeName, string expected)
    {
        var intType  = _compilation.GetSpecialType(SpecialType.System_Int32);
        var openType = _compilation.GetTypeByMetadataName(genericTypeName)!;
        var closed   = openType.Construct(intType);
        Assert.Equal(expected, _mapper.Map(closed));
    }

    [Theory]
    [InlineData("System.Collections.Generic.Dictionary`2",           "Record<string, number>")]
    [InlineData("System.Collections.Generic.IDictionary`2",          "Record<string, number>")]
    [InlineData("System.Collections.Generic.IReadOnlyDictionary`2",  "Record<string, number>")]
    public void Map_Dictionaries(string genericTypeName, string expected)
    {
        var strType  = _compilation.GetSpecialType(SpecialType.System_String);
        var intType  = _compilation.GetSpecialType(SpecialType.System_Int32);
        var openType = _compilation.GetTypeByMetadataName(genericTypeName)!;
        var closed   = openType.Construct(strType, intType);
        Assert.Equal(expected, _mapper.Map(closed));
    }

    // ── Array mapping ─────────────────────────────────────────────────────────

    [Fact]
    public void Map_Array_ProducesElementTypeArray()
    {
        var intType   = _compilation.GetSpecialType(SpecialType.System_Int32);
        var arrayType = _compilation.CreateArrayTypeSymbol(intType);
        Assert.Equal("number[]", _mapper.Map(arrayType));
    }

    // ── Nullable mapping ──────────────────────────────────────────────────────

    [Fact]
    public void Map_NullableInt_ProducesNumber()
    {
        var intType      = _compilation.GetSpecialType(SpecialType.System_Int32);
        var nullableType = _compilation.GetTypeByMetadataName("System.Nullable`1")!.Construct(intType);
        Assert.Equal("number", _mapper.Map(nullableType));
    }

    [Fact]
    public void IsNullable_NullableInt_ReturnsTrue()
    {
        var intType      = _compilation.GetSpecialType(SpecialType.System_Int32);
        var nullableType = _compilation.GetTypeByMetadataName("System.Nullable`1")!.Construct(intType);
        Assert.True(_mapper.IsNullable(nullableType));
    }

    [Fact]
    public void IsNullable_PlainInt_ReturnsFalse()
    {
        var intType = _compilation.GetSpecialType(SpecialType.System_Int32);
        Assert.False(_mapper.IsNullable(intType));
    }

    // ── Named type fallback ───────────────────────────────────────────────────

    [Fact]
    public void Map_CustomNamedType_ReturnsTypeName()
    {
        var source = "public class MyModel { }";
        var type   = CompilationHelper.GetType(source, "MyModel");
        Assert.Equal("MyModel", _mapper.Map(type));
    }

    // ── Generic fallback ──────────────────────────────────────────────────────

    [Fact]
    public void Map_CustomGenericType_PreservesTypeParameters()
    {
        var source = "public class Wrapper<T> { }";
        var type   = CompilationHelper.GetType(source, "Wrapper`1");
        // Unbound generic — type parameter is a type parameter symbol
        var intType = _compilation.GetSpecialType(SpecialType.System_Int32);
        var closed  = type.Construct(intType);
        Assert.Equal("Wrapper<number>", _mapper.Map(closed));
    }
}
