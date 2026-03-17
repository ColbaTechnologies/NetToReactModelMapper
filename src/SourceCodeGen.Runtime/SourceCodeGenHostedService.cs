using System.Reflection;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SourceCodeGen.Runtime;

internal sealed class SourceCodeGenHostedService(
    string path,
    IHostEnvironment env,
    ILogger<SourceCodeGenHostedService> logger,
    bool runInProduction = false) : IHostedService
{
    private const string ExportKeyword = "export";

    public Task StartAsync(CancellationToken ct)
    {
        if (!runInProduction && env.IsProduction())
        {
            return Task.CompletedTask;
        }

        var entryAssembly = Assembly.GetEntryAssembly();

        var contentType = entryAssembly?.GetType("GeneratedTypeScriptContent");
        if (contentType is null)
        {
            return Task.CompletedTask;
        }

        var outputPath = entryAssembly!
                             .GetType("SourceCodeGenOutputPath")
                             ?.GetField("Value", BindingFlags.Public | BindingFlags.Static)
                             ?.GetRawConstantValue() as string
                         ?? path;

        Directory.CreateDirectory(outputPath);

        foreach (var field in contentType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetRawConstantValue() is not string content)
            {
                continue;
            }

            // string.Contains(string, StringComparison) requires netstandard2.1+; use IndexOf instead.
            if (content.IndexOf(ExportKeyword, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var filePath = Path.Combine(outputPath, $"{field.Name}.tsx");
            try
            {
                File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write TypeScript file {FilePath}", filePath);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) =>
        Task.CompletedTask;
}
