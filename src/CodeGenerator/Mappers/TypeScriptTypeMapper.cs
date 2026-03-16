using System.Linq;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Mappers;

internal sealed class TypeScriptTypeMapper : ITypeMapper
{
    public string Map(ITypeSymbol type) => type switch
    {
        // Nullable<T> wrapper
        INamedTypeSymbol { IsGenericType: true, Name: "Nullable" } n
            => Map(n.TypeArguments[0]),

        // Arrays
        IArrayTypeSymbol arr => $"{Map(arr.ElementType)}[]",

        // Primitives
        { SpecialType: SpecialType.System_String }   => "string",
        { SpecialType: SpecialType.System_Boolean }  => "boolean",
        { SpecialType: SpecialType.System_DateTime } => "string",
        { SpecialType: var s } when IsNumeric(s)     => "number",

        // Date / time / id
        { Name: "DateTimeOffset" or "DateOnly" or "TimeOnly" or "Guid" } => "string",

        { SpecialType: SpecialType.System_Object } => "unknown",

        // Collections
        INamedTypeSymbol
        {
            IsGenericType: true,
            Name: "List" or "IList" or "IEnumerable" or "ICollection" or "IReadOnlyList"
        } col => $"{Map(col.TypeArguments[0])}[]",

        // Dictionaries
        INamedTypeSymbol
        {
            IsGenericType: true,
            Name: "Dictionary" or "IDictionary" or "IReadOnlyDictionary"
        } dict => $"Record<{Map(dict.TypeArguments[0])}, {Map(dict.TypeArguments[1])}>",

        // Generic fallback
        INamedTypeSymbol { IsGenericType: true } generic =>
            $"{generic.Name}<{string.Join(", ", generic.TypeArguments.Select(Map))}>",

        // Generic type parameter (T, TResult…)
        ITypeParameterSymbol tp => tp.Name,

        // Named type fallback
        INamedTypeSymbol other => other.Name,

        _ => "unknown"
    };

    public bool IsNullable(ITypeSymbol t) =>
        t.NullableAnnotation == NullableAnnotation.Annotated ||
        t is INamedTypeSymbol { IsGenericType: true, Name: "Nullable" };

    private static bool IsNumeric(SpecialType s) => s is
        SpecialType.System_Int16  or SpecialType.System_Int32  or SpecialType.System_Int64  or
        SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or
        SpecialType.System_Byte   or SpecialType.System_SByte;
}