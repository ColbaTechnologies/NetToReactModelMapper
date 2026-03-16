using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CodeGenerator.Helpers;
using CodeGenerator.Mappers;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

internal sealed class InterfaceFragmentGenerator : IFragmentGenerator
{
    private readonly ITypeMapper _typeMapper;

    public InterfaceFragmentGenerator(ITypeMapper typeMapper) =>
        _typeMapper = typeMapper;

    public bool CanHandle(INamedTypeSymbol type) =>
        type.TypeKind != TypeKind.Enum;

    public string Generate(INamedTypeSymbol type, CancellationToken ct)
    {
        var typeParams = type.TypeParameters.IsEmpty
            ? ""
            : $"<{string.Join(", ", type.TypeParameters.Select(static t => t.Name))}>";

        var baseSymbol = type.TypeKind == TypeKind.Interface
            ? (INamedTypeSymbol?)type.Interfaces.FirstOrDefault()
            : type.BaseType is { SpecialType: not SpecialType.System_Object } bt ? bt : null;

        var baseClause = baseSymbol is not null ? $" extends {baseSymbol.Name}" : "";

        var props = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p =>
                p.DeclaredAccessibility == Accessibility.Public &&
                !p.IsStatic &&
                !p.IsIndexer)
            .ToList();

        // Collect referenced [FrontendModel] types for import statements
        var imports = new SortedSet<string>(StringComparer.Ordinal);

        if (baseSymbol is not null && HasFrontendModelAttribute(baseSymbol))
            imports.Add(baseSymbol.Name);

        foreach (var prop in props)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var refType in GetFrontendModelReferences(prop.Type))
                if (refType.Name != type.Name)
                    imports.Add(refType.Name);
        }

        var sb = new StringBuilder(256);

        foreach (var import in imports)
            sb.AppendLine($"import type {{ {import} }} from './{import}Model';");

        if (imports.Count > 0) sb.AppendLine();

        sb.Append("export interface ").Append(type.Name)
          .Append(typeParams).Append(baseClause).AppendLine(" {");

        foreach (var prop in props)
        {
            var tsType   = _typeMapper.Map(prop.Type);
            var optional = _typeMapper.IsNullable(prop.Type) ? "?" : "";

            sb.Append("  ")
              .Append(NamingHelper.ToCamelCase(prop.Name))
              .Append(optional).Append(": ")
              .Append(tsType).AppendLine(";");
        }

        sb.AppendLine("}").AppendLine();
        return sb.ToString();
    }

    private static bool HasFrontendModelAttribute(INamedTypeSymbol type) =>
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