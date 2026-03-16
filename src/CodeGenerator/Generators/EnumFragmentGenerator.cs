using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal sealed class EnumFragmentGenerator : IFragmentGenerator
{
    public bool CanHandle(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Enum;

    public string Generate(INamedTypeSymbol type, CancellationToken ct)
    {
        var sb = new StringBuilder(256);
        sb.Append("export type ").Append(type.Name).Append(" = ");

        var first = true;
        foreach (var member in type.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is not IFieldSymbol { IsConst: true } field) continue;

            if (!first) sb.Append(" | ");
            sb.Append('"').Append(field.Name).Append('"');
            first = false;
        }

        sb.AppendLine(";").AppendLine();
        return sb.ToString();
    }
}