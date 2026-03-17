using System.Reflection;

namespace TsWriter;

internal interface IAssemblyContentReader
{
    /// <summary>Loads the assembly at once and returns both the output path and all model content.</summary>
    (string outputPath, IReadOnlyDictionary<string, string> content) Read(
        string assemblyPath,
        string fallbackPath);
}

internal sealed class AssemblyContentReader : IAssemblyContentReader
{
    private const string ContentTypeName    = "GeneratedTypeScriptContent";
    private const string OutputPathTypeName = "SourceCodeGenOutputPath";

    public (string outputPath, IReadOnlyDictionary<string, string> content) Read(
        string assemblyPath,
        string fallbackPath)
    {
        var asm = Assembly.LoadFrom(assemblyPath);

        var outputPath = asm.GetType(OutputPathTypeName)
            ?.GetField("Value", BindingFlags.Public | BindingFlags.Static)
            ?.GetRawConstantValue() as string
            ?? fallbackPath;

        var contentType = asm.GetType(ContentTypeName)
            ?? throw new InvalidOperationException(
                $"Type '{ContentTypeName}' not found in '{assemblyPath}'. " +
                "Ensure the assembly was compiled with [FrontendModel] or [FrontendService] attributes.");

        var fields = contentType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var result = new Dictionary<string, string>(fields.Length);

        foreach (var field in fields)
        {
            var raw = (string)field.GetValue(null)!;
            result[field.Name] = raw.Replace("\\n", "\n", StringComparison.Ordinal);
        }

        return (outputPath, result);
    }
}
