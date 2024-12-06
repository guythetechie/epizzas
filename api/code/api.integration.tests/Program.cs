using common;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace api.integration.tests;

internal static class Program
{
    private static async Task Main(string[] arguments)
    {
        using var host = GetHost(arguments);
        await HostingModule.RunHost(host, RunHost);
    }

    private static IHost GetHost(string[] arguments)
    {
        var builder = GetBuilder(arguments);
        return builder.Build();
    }

    private static HostApplicationBuilder GetBuilder(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);
        ConfigureBuilder(builder);
        return builder;
    }

    private static void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        ConfigureTelemetry(builder);
        ConfigureEnv(builder);
    }

    private static void ConfigureTelemetry(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(provider => new ActivitySource("api.integration.tests"));
        var telemetryBuilder = builder.Services.AddOpenTelemetry();

        OpenTelemetryModule.ConfigureDestination(telemetryBuilder, builder.Configuration);
        OpenTelemetryModule.SetAlwaysOnSampler(telemetryBuilder);
        HttpModule.ConfigureTelemetry(telemetryBuilder);
        common.CosmosModule.ConfigureTelemetry(telemetryBuilder);
    }

    private static async ValueTask RunHost(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var env = provider.GetRequiredService<Env>();

        await OrdersModule.RunTests()
                          .RunUnsafe((env.ActivitySource,
                                      env.OrdersContainer,
                                      env.GetApiClient),
                                     cancellationToken);
    }

    private sealed record Env
    {
        public required ActivitySource ActivitySource { get; init; }
        public required OrdersContainer OrdersContainer { get; init; }
        public required GetApiClient GetApiClient { get; init; }
        public required ILogger ILogger { get; init; }
    }

    private static void ConfigureEnv(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);
        ApiModule.ConfigureGetApiClient(builder);
        ConfigureLogger(builder);

        builder.Services.TryAddSingleton(GetEnv);
    }

    private static void ConfigureLogger(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(GetLogger);
    }

    private static ILogger GetLogger(IServiceProvider provider)
    {
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        var activitySource = provider.GetService<ActivitySource>();

        return loggerFactory.CreateLogger(activitySource?.Name ?? "Program");
    }

    private static Env GetEnv(IServiceProvider provider)
    {
        var ordersContainer = provider.GetRequiredService<OrdersContainer>();
        var getApiClient = provider.GetRequiredService<GetApiClient>();
        var logger = provider.GetRequiredService<ILogger>();

        return new Env
        {
            ActivitySource = provider.GetService<ActivitySource>() ?? new ActivitySource("Program"),
            OrdersContainer = ordersContainer,
            GetApiClient = getApiClient,
            ILogger = logger
        };
    }
}
