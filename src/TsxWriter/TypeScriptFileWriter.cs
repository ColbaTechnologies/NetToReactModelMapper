using System.Text;
namespace TsWriter;

internal interface ITypeScriptFileWriter
{
    void Write(IReadOnlyDictionary<string, string> models, string outputDirectory);
}

internal sealed class TypeScriptFileWriter : ITypeScriptFileWriter
{
    public void Write(IReadOnlyDictionary<string, string> models, string outputDirectory)
    {
        if (models.Count == 0)
        {
            Console.WriteLine("No models to write.");
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        foreach (var (name, content) in models)
        {
            if (string.IsNullOrWhiteSpace(content) ||
                !content.Contains("export", StringComparison.Ordinal))
                continue;

            var filePath = Path.Combine(outputDirectory, $"{name}Model.tsx");
            File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"Written: {filePath}");
        }
    }
}