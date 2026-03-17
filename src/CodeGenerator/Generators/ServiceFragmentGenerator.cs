using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CodeGenerator.Helpers;
using CodeGenerator.Mappers;
using Microsoft.CodeAnalysis;

namespace CodeGenerator.Generators;

/// <summary>
/// Generates a TypeScript service object from a C# controller decorated with [FrontendService].
/// Each public action method with an HTTP attribute becomes an async function that calls apiFetch.
/// </summary>
internal sealed class ServiceFragmentGenerator(ITypeMapper typeMapper)
{
    // ── EXTENSION POINT: HTTP verbs ───────────────────────────────────────────
    // To support a new HTTP verb, add its attribute class name and verb string.
    // The attribute is matched by the class name only — no ASP.NET Core for reference needed.
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> _verbByAttributeName =
        new(StringComparer.Ordinal)
        {
            { "HttpGetAttribute",    "GET"    },
            { "HttpPostAttribute",   "POST"   },
            { "HttpPutAttribute",    "PUT"    },
            { "HttpDeleteAttribute", "DELETE" },
            { "HttpPatchAttribute",  "PATCH"  },
        };

    // ── EXTENSION POINT: framework-injected parameter types ──────────────────
    // Parameters whose type name is in this set are silently skipped and never
    // appear in the generated TypeScript signature.
    // Add any new ASP.NET Core infrastructure types introduced in future versions.
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly HashSet<string> _frameworkParamTypes = new(StringComparer.Ordinal)
    {
        "CancellationToken", "HttpContext", "HttpRequest", "HttpResponse",
        "ClaimsPrincipal", "IFormFile", "IFormFileCollection"
    };

    public string? Generate(INamedTypeSymbol controller, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var classRoute = ResolveClassRoute(controller);
        var methods    = new List<string>();
        var imports    = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var member in controller.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (method.IsStatic || method.MethodKind != MethodKind.Ordinary)
            {
                continue;
            }

            var httpAttr = method.GetAttributes()
                .FirstOrDefault(a => _verbByAttributeName.ContainsKey(a.AttributeClass?.Name ?? ""));
            if (httpAttr is null)
            {
                continue;
            }

            var verb = _verbByAttributeName[httpAttr.AttributeClass!.Name];

            var methodRoute = httpAttr.ConstructorArguments.Length > 0
                ? httpAttr.ConstructorArguments[0].Value as string ?? ""
                : "";

            var fullRoute      = CombineRoutes(classRoute, methodRoute);
            var routeParamNames = ExtractRouteParamNames(fullRoute);

            var (tsReturn, returnImports) = MapReturnType(method.ReturnType);
            foreach (var imp in returnImports)
            {
                imports.Add(imp);
            }

            var (paramDecls, paramImports, bodyParam, queryParams) =
                MapParameters(method, routeParamNames);
            foreach (var imp in paramImports)
            {
                imports.Add(imp);
            }

            var tsRoute = BuildTsRoute(fullRoute, routeParamNames);
            var fnName  = NamingHelper.ToCamelCase(method.Name);
            methods.Add(BuildMethodEntry(fnName, paramDecls, tsReturn, tsRoute, verb, bodyParam, queryParams));
        }

        if (methods.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder(512);
        sb.AppendLine("import { apiFetch } from '../apiFetch';");

        if (imports.Count > 0)
        {
            foreach (var imp in imports)
            {
                sb.AppendLine($"import type {{ {imp} }} from './{imp}';");
            }

            sb.AppendLine();
        }

        var serviceName = StripControllerSuffix(controller.Name);
        sb.AppendLine($"export const {serviceName}Service = {{");

        for (var i = 0; i < methods.Count; i++)
        {
            sb.Append(methods[i]);
            sb.AppendLine(i < methods.Count - 1 ? "," : "");
        }

        sb.AppendLine("};");
        return sb.ToString();
    }

    // ── Route helpers ─────────────────────────────────────────────────────────

    private static string ResolveClassRoute(INamedTypeSymbol controller)
    {
        var routeAttr = controller.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RouteAttribute");

        var template = routeAttr?.ConstructorArguments.Length > 0
            ? routeAttr.ConstructorArguments[0].Value as string ?? "api/[controller]"
            : "api/[controller]";

        var name = StripControllerSuffix(controller.Name).ToLowerInvariant();
        // Use case-insensitive replace compatible with netstandard2.0
        return ReplaceIgnoreCase(ReplaceIgnoreCase(template, "[controller]", name), "[action]", "")
            .Trim('/');
    }

    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        var idx = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return source;
        }

        return source.Substring(0, idx) + newValue + source.Substring(idx + oldValue.Length);
    }

    private static string CombineRoutes(string classRoute, string methodRoute)
    {
        if (string.IsNullOrEmpty(methodRoute))
        {
            return classRoute;
        }

        return classRoute.TrimEnd('/') + "/" + methodRoute.TrimStart('/');
    }

    private static List<string> ExtractRouteParamNames(string route)
    {
        var result = new List<string>();
        var i = 0;
        while (i < route.Length)
        {
            if (route[i] != '{') { i++; continue; }
            var end = route.IndexOf('}', i + 1);
            if (end < 0)
            {
                break;
            }

            var raw = route.Substring(i + 1, end - i - 1);
            var colon = raw.IndexOf(':');
            if (colon >= 0)
            {
                raw = raw.Substring(0, colon);
            }

            result.Add(raw.TrimEnd('?'));
            i = end + 1;
        }
        return result;
    }

    private static string BuildTsRoute(string route, List<string> routeParamNames)
    {
        if (routeParamNames.Count == 0)
        {
            return $"'/{route}'";
        }

        var ts = route;
        foreach (var p in routeParamNames)
        {
            ts = ts.Replace("{" + p + "}", "${" + p + "}");
        }

        return $"`/{ts}`";
    }

    // ── Return-type mapping ───────────────────────────────────────────────────

    private (string tsType, IEnumerable<string> imports) MapReturnType(ITypeSymbol returnType)
    {
        switch (returnType)
        {
            // Unwrap Task<T>
            case INamedTypeSymbol { IsGenericType: true, Name: "Task" } task:
                return MapReturnType(task.TypeArguments[0]);
            // Unwrap ActionResult<T>
            case INamedTypeSymbol { IsGenericType: true, Name: "ActionResult" } ar:
                return MapReturnType(ar.TypeArguments[0]);
        }

        // Void-like
        if (returnType.SpecialType == SpecialType.System_Void ||
            returnType is INamedTypeSymbol { Name: "Task" or "IActionResult" or "ActionResult", IsGenericType: false })
        {
            return ("void", Enumerable.Empty<string>());
        }

        var ts      = typeMapper.Map(returnType);
        var imports = CollectFrontendModelImports(returnType);
        return (ts, imports);
    }

    private static List<string> CollectFrontendModelImports(ITypeSymbol type)
    {
        var result = new List<string>();
        if (type is INamedTypeSymbol named && InterfaceFragmentGenerator.HasFrontendModelAttribute(named))
        {
            result.Add(named.Name);
        }

        switch (type)
        {
            case INamedTypeSymbol { IsGenericType: true } generic:
            {
                foreach (var arg in generic.TypeArguments)
                {
                    result.AddRange(CollectFrontendModelImports(arg));
                }

                break;
            }
            case IArrayTypeSymbol arr:
                result.AddRange(CollectFrontendModelImports(arr.ElementType));
                break;
        }

        return result;
    }

    // ── Parameter mapping ─────────────────────────────────────────────────────

    private (
        List<string> decls,
        List<string> imports,
        string?      bodyParam,
        List<string> queryParams
    ) MapParameters(IMethodSymbol method, List<string> routeParamNames)
    {
        var decls       = new List<string>();
        var imports     = new List<string>();
        string? body    = null;
        var queryParams = new List<string>();

        foreach (var param in method.Parameters)
        {
            if (_frameworkParamTypes.Contains(param.Type.Name))
            {
                continue;
            }

            var isRoute    = IsRouteParam(param.Name, routeParamNames);
            var hasFromBody  = HasAttribute(param, "FromBodyAttribute");
            var hasFromQuery = HasAttribute(param, "FromQueryAttribute");
            var isComplex    = IsComplexType(param.Type);
            var tsType       = typeMapper.Map(param.Type);
            var nullable     = typeMapper.IsNullable(param.Type) || param.IsOptional;
            var decl         = nullable ? $"{param.Name}?: {tsType}" : $"{param.Name}: {tsType}";

            decls.Add(decl);

            if (isRoute)
            {
                // embedded in URL template — no options entry needed
            }
            else if (hasFromBody || (!hasFromQuery && isComplex))
            {
                body = param.Name;
                imports.AddRange(CollectFrontendModelImports(param.Type));
            }
            else
            {
                queryParams.Add(param.Name);
            }
        }

        return (decls, imports, body, queryParams);
    }

    // ── Method entry builder ──────────────────────────────────────────────────

    private static string BuildMethodEntry(
        string       fnName,
        List<string> paramDecls,
        string       tsReturn,
        string       tsRoute,
        string       verb,
        string?      bodyParam,
        List<string> queryParams)
    {
        var signature = string.Join(", ", paramDecls);
        var options   = new List<string> { $"method: '{verb}'" };

        if (bodyParam is not null)
        {
            options.Add($"body: {bodyParam}");
        }

        if (queryParams.Count > 0)
        {
            options.Add($"params: {{ {string.Join(", ", queryParams)} }}");
        }

        return $"  {fnName}: ({signature}): Promise<{tsReturn}> =>\n    apiFetch({tsRoute}, {{ {string.Join(", ", options)} }})";
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string StripControllerSuffix(string name) =>
        name.EndsWith("Controller", StringComparison.Ordinal)
            ? name.Substring(0, name.Length - "Controller".Length)
            : name;

    private static bool IsRouteParam(string paramName, List<string> routeParamNames)
    {
        foreach (var rp in routeParamNames)
        {
            if (string.Equals(rp, paramName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(IParameterSymbol param, string attributeName)
    {
        foreach (var attr in param.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsComplexType(ITypeSymbol type) =>
        type is INamedTypeSymbol { SpecialType: SpecialType.None, TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Struct, Name: not (
            "String" or "Guid" or "DateTime" or "DateTimeOffset" or
            "DateOnly" or "TimeOnly" or "Uri")
        };
}
