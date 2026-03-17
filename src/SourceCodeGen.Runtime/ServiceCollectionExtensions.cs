using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SourceCodeGen.Runtime;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SourceCodeGen runtime service, which writes TypeScript files to
    /// <paramref name="outputPath"/> on application startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="outputPath">Output directory for generated <c>.tsx</c> files. Defaults to <c>src/generated</c>.</param>
    /// <param name="runInProduction">
    /// When <c>true</c>, also runs in Production environments.
    /// Defaults to <c>false</c> to prevent unintended writes in production.
    /// </param>
    public static IServiceCollection AddSourceCodeGen(
        this IServiceCollection services,
        string outputPath = "src/generated",
        bool runInProduction = false)
    {
        services.AddSingleton<IHostedService>(sp =>
            new SourceCodeGenHostedService(
                outputPath,
                sp.GetRequiredService<IHostEnvironment>(),
                sp.GetRequiredService<ILogger<SourceCodeGenHostedService>>(),
                runInProduction));

        return services;
    }
}
