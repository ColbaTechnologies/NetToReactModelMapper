using System;
using System.Collections.Generic;
using System.Reflection;

namespace TsWriter;

internal interface IAssemblyContentReader
{
    IReadOnlyDictionary<string, string> Read(string assemblyPath);
    string GetOutputPath(string assemblyPath, string fallback);
}

internal sealed class AssemblyContentReader : IAssemblyContentReader
{
    private const string ContentTypeName    = "GeneratedTypeScriptContent";
    private const string OutputPathTypeName = "SourceCodeGenOutputPath";

    public IReadOnlyDictionary<string, string> Read(string assemblyPath)
    {
        var asm = Assembly.LoadFrom(assemblyPath);

        var type = asm.GetType(ContentTypeName)
                   ?? throw new InvalidOperationException(
                       $"Not found: {ContentTypeName}. Is the assembly compiled with [FrontendModel] attributes?");

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        var result = new Dictionary<string, string>(fields.Length);

        foreach (var field in fields)
        {
            var content = (string)field.GetValue(null)!;

            content = content.Replace("\\n", "\n", StringComparison.Ordinal);

            result[field.Name] = content;
        }

        return result;
    }

    public string GetOutputPath(string assemblyPath, string fallback)
    {
        var asm  = Assembly.LoadFrom(assemblyPath);
        var type = asm.GetType(OutputPathTypeName);

        return type?.GetField("Value", BindingFlags.Public | BindingFlags.Static)
                   ?.GetRawConstantValue() as string
               ?? fallback;
    }
}