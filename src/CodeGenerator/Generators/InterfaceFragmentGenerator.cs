using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CodeGenerator.Helpers;
using CodeGenerator.Mappers;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal sealed class InterfaceFragmentGenerator(ITypeMapper typeMapper) : IFragmentGenerator
{
    public bool CanHandle(INamedTypeSymbol type) =>
        type.TypeKind != TypeKind.Enum;

    public string Generate(INamedTypeSymbol type, CancellationToken ct)
    {
        var typeParams = type.TypeParameters.IsEmpty
            ? ""
            : $"<{string.Join(", ", type.TypeParameters.Select(static t => t.Name))}>";

        var baseSymbol = type.TypeKind == TypeKind.Interface
            ? type.Interfaces.FirstOrDefault()
            : type.BaseType is { SpecialType: not SpecialType.System_Object } bt ? bt : null;

        var baseClause = baseSymbol is not null ? $" extends {baseSymbol.Name}" : "";

        var props = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p =>
                p.DeclaredAccessibility == Accessibility.Public &&
                p is { IsStatic: false, IsIndexer: false })
            .ToList();

        // SortedSet allocated only when at least one import is needed.
        SortedSet<string>? imports = null;

        if (baseSymbol is not null && HasFrontendModelAttribute(baseSymbol))
            (imports ??= new SortedSet<string>(StringComparer.Ordinal)).Add(baseSymbol.Name);

        foreach (var prop in props)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var refType in GetFrontendModelReferences(prop.Type))
                if (refType.Name != type.Name)
                    (imports ??= new SortedSet<string>(StringComparer.Ordinal)).Add(refType.Name);
        }

        var sb = new StringBuilder(256);

        if (imports is not null)
        {
            foreach (var import in imports)
                sb.AppendLine($"import type {{ {import} }} from './{import}';");
            sb.AppendLine();
        }

        sb.Append("export interface ").Append(type.Name)
          .Append(typeParams).Append(baseClause).AppendLine(" {");

        foreach (var prop in props)
        {
            var tsType   = typeMapper.Map(prop.Type);
            var optional = typeMapper.IsNullable(prop.Type) ? "?" : "";

            sb.Append("  ")
              .Append(NamingHelper.ToCamelCase(prop.Name))
              .Append(optional).Append(": ")
              .Append(tsType).AppendLine(";");
        }

        sb.AppendLine("}").AppendLine();
        return sb.ToString();
    }

    internal static bool HasFrontendModelAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().Any(static a => a.AttributeClass?.Name == "FrontendModelAttribute");

    private static IEnumerable<INamedTypeSymbol> GetFrontendModelReferences(ITypeSymbol type)
    {
        switch (type)
        {
            case INamedTypeSymbol { IsGenericType: true, Name: "Nullable" } n:
                foreach (var t in GetFrontendModelReferences(n.TypeArguments[0]))
                    yield return t;
                break;

            case IArrayTypeSymbol arr:
                foreach (var t in GetFrontendModelReferences(arr.ElementType))
                    yield return t;
                break;

            case INamedTypeSymbol { IsGenericType: true } generic:
                if (HasFrontendModelAttribute(generic))
                    yield return generic;
                foreach (var arg in generic.TypeArguments)
                    foreach (var t in GetFrontendModelReferences(arg))
                        yield return t;
                break;

            case INamedTypeSymbol named when HasFrontendModelAttribute(named):
                yield return named;
                break;
        }
    }
}
