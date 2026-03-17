using System.Threading;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal interface IFragmentGenerator
{
    bool CanHandle(INamedTypeSymbol type);
    /// <returns>The generated TypeScript fragment, or <c>null</c> if the type should be skipped.</returns>
    string? Generate(INamedTypeSymbol type, CancellationToken ct);
}
