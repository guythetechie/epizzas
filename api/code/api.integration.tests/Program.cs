﻿using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace api.integration.tests;

internal static class Program
{
    private static async Task Main(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);
        ConfigureBuilder(builder);

        using var host = builder.Build();
        await RunHost(host);
    }

    private static void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        ConfigureTelemetry(builder);
        OrdersModule.ConfigureRunOrderTests(builder);
    }

    private static void ConfigureTelemetry(IHostApplicationBuilder builder)
    {
        OpenTelemetryModule.ConfigureActivitySource(builder, "api.integration.tests");

        var telemetryBuilder = builder.Services.AddOpenTelemetry();
        OpenTelemetryModule.ConfigureDestination(telemetryBuilder, builder.Configuration);
        OpenTelemetryModule.ConfigureAspNetCoreInstrumentation(telemetryBuilder);
        OpenTelemetryModule.SetAlwaysOnSampler(telemetryBuilder);
        common.CosmosModule.ConfigureTelemetry(telemetryBuilder);
    }

    private static async ValueTask RunHost(IHost host)
    {
        try
        {
            var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var cancellationToken = applicationLifetime.ApplicationStopping;

            await host.StartAsync(cancellationToken);

            var runTests = host.Services.GetRequiredService<RunOrderTests>();
            await runTests(cancellationToken);
        }
        catch
        {
            Environment.ExitCode = -1;
            throw;
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
