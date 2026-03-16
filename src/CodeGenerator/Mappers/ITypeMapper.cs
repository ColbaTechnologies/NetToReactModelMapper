using Microsoft.CodeAnalysis;

namespace CodeGenerator.Mappers;

internal interface ITypeMapper
{
    string Map(ITypeSymbol type);
    bool IsNullable(ITypeSymbol type);
}