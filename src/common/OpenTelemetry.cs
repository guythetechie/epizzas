using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace common;

public static class OpenTelemetryServices
{
    public static void Configure(IServiceCollection services, string activitySourceName)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        services.TryAddSingleton(new ActivitySource(activitySourceName));
#pragma warning restore CA2000 // Dispose objects before losing scope

        services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
                                               .AddHttpClientInstrumentation()
                                               .AddRuntimeInstrumentation()
                                               .AddMeter("*"))
                .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation()
                                               .AddHttpClientInstrumentation()
                                               .AddSource("*")
                                               .SetSampler<AlwaysOnSampler>());

        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        configuration.TryGetValue("OTEL_EXPORTER_OTLP_ENDPOINT")
                     .Iter(_ =>
                     {
                         services.AddLogging(builder => builder.AddOpenTelemetry());
                         services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter())
                                 .ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter())
                                 .ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
                     });

        configuration.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING")
                     .Iter(_ => services.AddOpenTelemetry()
                                        .UseAzureMonitor());
    }
}
