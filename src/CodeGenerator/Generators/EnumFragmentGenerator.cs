using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal sealed class EnumFragmentGenerator : IFragmentGenerator
{
    public bool CanHandle(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Enum;

    public string? Generate(INamedTypeSymbol type, CancellationToken ct)
    {
        var members = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static f => f.IsConst)
            .ToList();

        if (members.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder(256);
        sb.Append("export type ").Append(type.Name).Append(" = ");

        var first = true;
        foreach (var field in members)
        {
            ct.ThrowIfCancellationRequested();
            if (!first)
            {
                sb.Append(" | ");
            }

            sb.Append('"').Append(field.Name).Append('"');
            first = false;
        }

        sb.AppendLine(";").AppendLine();
        return sb.ToString();
    }
}
