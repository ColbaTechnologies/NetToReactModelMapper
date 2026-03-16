using System.Reflection;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace SourceCodeGen;

internal sealed class SourceCodeGenHostedService(string path, IHostEnvironment env) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        if (env.IsProduction()) return Task.CompletedTask;

        var entryAssembly = Assembly.GetEntryAssembly();

        var contentType = entryAssembly?.GetType("GeneratedTypeScriptContent");
        if (contentType is null) return Task.CompletedTask;

        // Read the output path embedded by the Roslyn generator at compile time.
        // Falls back to the constructor parameter if the constant is not present.
        var outputPath = entryAssembly!
            .GetType("SourceCodeGenOutputPath")
            ?.GetField("Value", BindingFlags.Public | BindingFlags.Static)
            ?.GetRawConstantValue() as string
            ?? path;

        Directory.CreateDirectory(outputPath);

        foreach (var field in contentType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetRawConstantValue() is not string content) continue;
            if (!content.Contains("export", StringComparison.Ordinal)) continue;

            var filePath = Path.Combine(outputPath, $"{field.Name}Model.tsx");
            File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}