using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SourceCodeGen;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SourceCodeGen runtime service, which writes TypeScript model files
    /// to <paramref name="outputPath"/> on application startup (non-Production only).
    /// </summary>
    public static IServiceCollection AddSourceCodeGen(
        this IServiceCollection services,
        string outputPath = "src/generated")
    {
        services.AddSingleton<IHostedService>(sp =>
            new SourceCodeGenHostedService(
                outputPath,
                sp.GetRequiredService<IHostEnvironment>()));

        return services;
    }
}