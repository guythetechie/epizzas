using System.Threading.Tasks;
using System;
using common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace api.integration.tests;

internal static class Program
{
    private static async Task Main(string[] args) =>
        await HostingModule.RunHost(args, "api.integration.tests", ConfigureRunApplication);

    private static void ConfigureRunApplication(IHostApplicationBuilder builder)
    {
        v1.Orders.TestModule.ConfigureRunTests(builder);
        builder.Services.TryAddSingleton(GetRunApplication);
    }

    private static RunApplication GetRunApplication(IServiceProvider provider)
    {
        var runTests = provider.GetRequiredService<v1.Orders.RunTests>();
        var logger = provider.GetRequiredService<ILogger>();

        return async cancellationToken =>
        {
            logger.LogInformation($"Running API integration tests...");

            await runTests(cancellationToken);

            logger.LogInformation($"Integration tests finished.");
        };
    }
}