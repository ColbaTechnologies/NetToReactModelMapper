using System.Threading;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal interface IFragmentGenerator
{
    bool CanHandle(INamedTypeSymbol type);
    string Generate(INamedTypeSymbol type, CancellationToken ct);
}